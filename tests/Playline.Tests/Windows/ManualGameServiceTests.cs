using Playline.Core.Models;
using Playline.Core.Services;
using Playline.Tests.TestSupport;
using Playline.Windows.Games;
using Playline.Windows.Icons;
using Playline.Windows.Shortcuts;

namespace Playline.Tests.Windows;

public sealed class ManualGameServiceTests
{
    [Fact]
    public async Task CreateAsync_FromExecutable_UsesFileNameWhenMetadataIsUnavailable()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var executablePath = Path.Combine(temporaryDirectory.DirectoryPath, "Hades.exe");
        File.WriteAllBytes(executablePath, []);
        var service = CreateService(temporaryDirectory);

        var game = await service.CreateAsync(executablePath);

        Assert.Equal("Hades", game.Name);
        Assert.Equal(GameSource.Manual, game.Source);
        Assert.Equal(Path.GetFullPath(executablePath), game.ExecutablePath, ignoreCase: true);
        Assert.Equal(GameIdentity.CreateManualId(executablePath), game.Id);
    }

    [Fact]
    public async Task CreateAsync_FromShortcut_PreservesResolvedLaunchInformation()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var executablePath = Path.Combine(temporaryDirectory.DirectoryPath, "Game.exe");
        var shortcutPath = Path.Combine(temporaryDirectory.DirectoryPath, "Configured Game.lnk");
        File.WriteAllBytes(executablePath, []);
        WindowsShortcutTestHelper.Create(
            shortcutPath,
            executablePath,
            "--language pt-BR",
            temporaryDirectory.DirectoryPath);
        var service = CreateService(temporaryDirectory);

        var game = await service.CreateAsync(shortcutPath);

        Assert.Equal("Configured Game", game.Name);
        Assert.Equal("--language pt-BR", game.Arguments);
        Assert.Equal(Path.GetFullPath(temporaryDirectory.DirectoryPath), game.WorkingDirectory, ignoreCase: true);
        Assert.Equal(Path.GetFullPath(executablePath), game.ExecutablePath, ignoreCase: true);
    }

    [Fact]
    public async Task CreateAsync_AcceptsSpacesAndUnicodeInExecutablePath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var gameDirectory = Path.Combine(temporaryDirectory.DirectoryPath, "Program Files", "Ação Especial");
        Directory.CreateDirectory(gameDirectory);
        var executablePath = Path.Combine(gameDirectory, "Jogo São João.exe");
        File.WriteAllBytes(executablePath, []);
        var service = CreateService(temporaryDirectory);

        var game = await service.CreateAsync(executablePath);

        Assert.Equal(Path.GetFullPath(executablePath), game.ExecutablePath, ignoreCase: true);
        Assert.Equal("Jogo São João", game.Name);
    }

    [Fact]
    public async Task CreateAsync_WhenShortcutTargetWasRemoved_ThrowsFileNotFound()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var executablePath = Path.Combine(temporaryDirectory.DirectoryPath, "Removed.exe");
        var shortcutPath = Path.Combine(temporaryDirectory.DirectoryPath, "Removed Game.lnk");
        File.WriteAllBytes(executablePath, []);
        WindowsShortcutTestHelper.Create(shortcutPath, executablePath, null, temporaryDirectory.DirectoryPath);
        File.Delete(executablePath);
        var service = CreateService(temporaryDirectory);

        await Assert.ThrowsAsync<FileNotFoundException>(() => service.CreateAsync(shortcutPath));
    }

    private static ManualGameService CreateService(TemporaryDirectory temporaryDirectory)
    {
        return new ManualGameService(
            new ShellLinkShortcutResolver(),
            new IconCacheService(Path.Combine(temporaryDirectory.DirectoryPath, "icons")));
    }
}
