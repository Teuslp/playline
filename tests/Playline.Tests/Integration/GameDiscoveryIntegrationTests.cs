using System.Text.Json;
using Playline.Discovery;
using Playline.Discovery.Contracts;
using Playline.Discovery.Epic;
using Playline.Discovery.Steam;
using Playline.Tests.TestSupport;

namespace Playline.Tests.Integration;

public sealed class GameDiscoveryIntegrationTests
{
    [Fact]
    public async Task ScanAsync_AggregatesSimulatedSteamAndEpicInstallations()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var steamRoot = Path.Combine(temporaryDirectory.DirectoryPath, "Steam");
        var steamInstall = Path.Combine(steamRoot, "steamapps", "common", "Hades");
        Directory.CreateDirectory(steamInstall);
        await File.WriteAllTextAsync(
            Path.Combine(steamRoot, "steamapps", "appmanifest_1145360.acf"),
            "\"AppState\"\n{\n  \"appid\" \"1145360\"\n  \"name\" \"Hades\"\n  \"installdir\" \"Hades\"\n}");

        var epicManifests = Path.Combine(temporaryDirectory.DirectoryPath, "EpicManifests");
        var epicInstall = Path.Combine(temporaryDirectory.DirectoryPath, "EpicGame");
        var epicExecutable = Path.Combine(epicInstall, "Game.exe");
        Directory.CreateDirectory(epicManifests);
        Directory.CreateDirectory(epicInstall);
        File.WriteAllBytes(epicExecutable, []);
        await File.WriteAllTextAsync(
            Path.Combine(epicManifests, "game.item"),
            JsonSerializer.Serialize(new
            {
                DisplayName = "Epic Test Game",
                InstallLocation = epicInstall,
                LaunchExecutable = "Game.exe",
                AppName = "EpicArtifact",
                CatalogItemId = "EpicCatalog",
                CatalogNamespace = "test",
                bIsIncompleteInstall = false
            }));

        IGameScanner[] scanners =
        [
            new SteamGameScanner(new SteamLocator([steamRoot])),
            new EpicGameScanner(new EpicLocator([epicManifests]))
        ];
        var logger = new RecordingLogger();
        var service = new GameDiscoveryService(scanners, logger);

        var games = await service.ScanAsync();

        Assert.Equal(2, games.Count);
        Assert.Contains(games, game => game.Id == "steam-1145360");
        Assert.Contains(games, game => game.Id == "epic-epiccatalog");
        Assert.Empty(logger.Entries);
    }

    private sealed class SteamLocator(IReadOnlyList<string> paths) : ISteamInstallationLocator
    {
        public IReadOnlyList<string> LocateInstallations() => paths;
    }

    private sealed class EpicLocator(IReadOnlyList<string> paths) : IEpicManifestLocator
    {
        public IReadOnlyList<string> LocateManifestDirectories() => paths;
    }
}
