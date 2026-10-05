using Playline.Core.Models;
using Playline.Storage.Paths;
using Playline.Storage.Settings;
using Playline.Tests.TestSupport;

namespace Playline.Tests.Storage;

public sealed class JsonSettingsRepositoryTests
{
    [Fact]
    public async Task LoadAsync_WhenJsonIsCorrupt_QuarantinesItAndWritesDefaults()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppDataPaths(temporaryDirectory.DirectoryPath);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.SettingsFile, "{ corrupt");
        var logger = new RecordingLogger();
        var repository = new JsonSettingsRepository(paths, logger);

        var settings = await repository.LoadAsync();

        Assert.Equal(new AppSettings(), settings);
        Assert.Single(logger.Entries);
        Assert.Single(Directory.GetFiles(paths.RootDirectory, "settings.json.corrupt-*.bak"));
        var reloaded = await repository.LoadAsync();
        Assert.Equal(settings, reloaded);
        Assert.Single(logger.Entries);
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_CreatesDefaultSettings()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppDataPaths(temporaryDirectory.DirectoryPath);
        var repository = new JsonSettingsRepository(paths, new RecordingLogger());

        var settings = await repository.LoadAsync();

        Assert.Equal(new AppSettings(), settings);
        Assert.True(File.Exists(paths.SettingsFile));
        Assert.True(Directory.Exists(paths.CacheDirectory));
    }

    [Fact]
    public async Task SaveAndLoadAsync_PreservesSettings()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppDataPaths(temporaryDirectory.DirectoryPath);
        var repository = new JsonSettingsRepository(paths, new RecordingLogger());
        var expected = new AppSettings
        {
            StartWithWindows = true,
            DisplayMode = GameDisplayMode.Name,
            ItemSize = GameItemSize.Large,
            BarOrientation = BarOrientation.Vertical,
            BarTheme = BarTheme.IconsOnly,
            AfterLaunchAction = AfterLaunchAction.Hide,
            AutoHide = true,
            RestoreWindowPosition = true,
            LockWindowPosition = true,
            WindowX = 320,
            WindowY = 180
        };

        await repository.SaveAsync(expected);
        var actual = await repository.LoadAsync();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task LoadAsync_MigratesLegacySettingsAndPreservesKnownValues()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppDataPaths(temporaryDirectory.DirectoryPath);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(
            paths.SettingsFile,
            """
            {
              "startWithWindows": true,
              "alwaysOnTop": true,
              "closeAfterGameLaunch": true,
              "iconSize": 64,
              "barPosition": "Bottom",
              "theme": "System"
            }
            """);
        var repository = new JsonSettingsRepository(paths, new RecordingLogger());

        var settings = await repository.LoadAsync();

        Assert.True(settings.StartWithWindows);
        Assert.False(settings.AlwaysOnTop);
        Assert.Equal(GameItemSize.Large, settings.ItemSize);
        Assert.Equal(AfterLaunchAction.Exit, settings.AfterLaunchAction);
        Assert.False(settings.CloseAfterGameLaunch);
        Assert.Equal(0, settings.IconSize);
        var migratedJson = await File.ReadAllTextAsync(paths.SettingsFile);
        Assert.DoesNotContain("closeAfterGameLaunch", migratedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("iconSize", migratedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alwaysOnTop", migratedJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_UsesDefaultsForUnknownEnumsAndInvalidPosition()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = new AppDataPaths(temporaryDirectory.DirectoryPath);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(
            paths.SettingsFile,
            """
            {
              "displayMode": "FutureMode",
              "itemSize": "Enormous",
              "barOrientation": "Diagonal",
              "barTheme": "Invisible",
              "afterLaunchAction": "Teleport",
              "windowX": 42
            }
            """);
        var logger = new RecordingLogger();
        var repository = new JsonSettingsRepository(paths, logger);

        var settings = await repository.LoadAsync();

        Assert.Equal(GameDisplayMode.Compact, settings.DisplayMode);
        Assert.Equal(GameItemSize.Medium, settings.ItemSize);
        Assert.Equal(BarOrientation.Horizontal, settings.BarOrientation);
        Assert.Equal(BarTheme.Glass, settings.BarTheme);
        Assert.Equal(AfterLaunchAction.KeepOpen, settings.AfterLaunchAction);
        Assert.Null(settings.WindowX);
        Assert.Null(settings.WindowY);
        Assert.Empty(logger.Entries);
    }
}
