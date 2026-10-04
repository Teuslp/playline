using System.Text.Json;
using Playline.Core.Models;
using Playline.Discovery.Epic;
using Playline.Tests.TestSupport;

namespace Playline.Tests.Discovery;

public sealed class EpicGameScannerTests
{
    [Fact]
    public async Task ScanAsync_WhenEpicIsMissing_ReturnsEmptyList()
    {
        var scanner = new EpicGameScanner(new FakeEpicLocator([]));

        var games = await scanner.ScanAsync();

        Assert.Empty(games);
    }

    [Fact]
    public async Task ScanAsync_ReadsInstalledManifestAndSkipsMalformedOrIncompleteItems()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manifestDirectory = Path.Combine(temporaryDirectory.DirectoryPath, "Manifests");
        var installDirectory = Path.Combine(temporaryDirectory.DirectoryPath, "Fortnite");
        var executablePath = Path.Combine(installDirectory, "FortniteGame", "Binaries", "Win64", "Fortnite.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!);
        Directory.CreateDirectory(manifestDirectory);
        File.WriteAllBytes(executablePath, []);

        await WriteManifestAsync(
            Path.Combine(manifestDirectory, "fortnite.item"),
            installDirectory,
            isIncomplete: false);
        await WriteManifestAsync(
            Path.Combine(manifestDirectory, "incomplete.item"),
            installDirectory,
            isIncomplete: true);
        await File.WriteAllTextAsync(Path.Combine(manifestDirectory, "invalid.item"), "{ invalid");

        var scanner = new EpicGameScanner(new FakeEpicLocator([manifestDirectory]));
        var games = await scanner.ScanAsync();

        var game = Assert.Single(games);
        Assert.Equal("epic-catalog-item", game.Id);
        Assert.Equal("Fortnite", game.Name);
        Assert.Equal(GameSource.Epic, game.Source);
        Assert.Equal(Path.GetFullPath(executablePath), game.ExecutablePath, ignoreCase: true);
        Assert.Equal("-AUTH_LOGIN=unused", game.Arguments);
        Assert.StartsWith("com.epicgames.launcher://apps/", game.LaunchUri, StringComparison.Ordinal);
    }

    private static Task WriteManifestAsync(string path, string installDirectory, bool isIncomplete)
    {
        var manifest = new
        {
            DisplayName = "Fortnite",
            InstallLocation = installDirectory,
            LaunchExecutable = @"FortniteGame\Binaries\Win64\Fortnite.exe",
            LaunchCommand = "-AUTH_LOGIN=unused",
            AppName = "FortniteArtifact",
            CatalogItemId = "Catalog-Item",
            CatalogNamespace = "fn",
            bIsIncompleteInstall = isIncomplete
        };

        return File.WriteAllTextAsync(path, JsonSerializer.Serialize(manifest));
    }

    private sealed class FakeEpicLocator(IReadOnlyList<string> directories) : IEpicManifestLocator
    {
        public IReadOnlyList<string> LocateManifestDirectories() => directories;
    }
}

