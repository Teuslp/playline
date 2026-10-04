using Playline.Core.Models;
using Playline.Discovery.Steam;
using Playline.Tests.TestSupport;

namespace Playline.Tests.Discovery;

public sealed class SteamGameScannerTests
{
    [Fact]
    public async Task ScanAsync_WhenSteamIsMissing_ReturnsEmptyList()
    {
        var scanner = new SteamGameScanner(new FakeSteamLocator([]));

        var games = await scanner.ScanAsync();

        Assert.Empty(games);
    }

    [Fact]
    public async Task ScanAsync_ReadsManifestsFromMultipleLibrariesAndSkipsInvalidFiles()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var steamRoot = Path.Combine(temporaryDirectory.DirectoryPath, "Steam");
        var secondLibrary = Path.Combine(temporaryDirectory.DirectoryPath, "Second Library");
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        Directory.CreateDirectory(Path.Combine(secondLibrary, "steamapps"));
        await File.WriteAllTextAsync(
            Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
            CreateLibraryFile(steamRoot, secondLibrary));

        await CreateInstalledGameAsync(steamRoot, "730", "Counter-Strike 2", "CounterStrike");
        await CreateInstalledGameAsync(secondLibrary, "1245620", "ELDEN RING", "ELDEN RING");
        await CreateInstalledGameAsync(secondLibrary, "999", "Uninstalled Game", "Missing", stateFlags: "2");
        await CreateInstalledGameAsync(secondLibrary, "not-numeric", "Invalid App", "Invalid");
        await File.WriteAllTextAsync(
            Path.Combine(secondLibrary, "steamapps", "appmanifest_invalid.acf"),
            "{ invalid");

        var scanner = new SteamGameScanner(new FakeSteamLocator([steamRoot]));
        var games = await scanner.ScanAsync();

        Assert.Equal(2, games.Count);
        var counterStrike = Assert.Single(games, game => game.Id == "steam-730");
        Assert.Equal("Counter-Strike 2", counterStrike.Name);
        Assert.Equal("steam://rungameid/730", counterStrike.LaunchUri);
        Assert.Equal(GameSource.Steam, counterStrike.Source);
        Assert.Contains(games, game => game.Id == "steam-1245620");
        Assert.DoesNotContain(games, game => game.Name == "Uninstalled Game");
        Assert.DoesNotContain(games, game => game.Name == "Invalid App");
    }

    private static async Task CreateInstalledGameAsync(
        string library,
        string appId,
        string name,
        string installDirectory,
        string stateFlags = "4")
    {
        Directory.CreateDirectory(Path.Combine(library, "steamapps", "common", installDirectory));
        var manifest = $"\"AppState\"\n{{\n"
            + $"  \"appid\" \"{appId}\"\n"
            + $"  \"name\" \"{name}\"\n"
            + $"  \"installdir\" \"{installDirectory}\"\n"
            + $"  \"StateFlags\" \"{stateFlags}\"\n}}";
        await File.WriteAllTextAsync(
            Path.Combine(library, "steamapps", $"appmanifest_{appId}.acf"),
            manifest);
    }

    private static string CreateLibraryFile(params string[] libraries)
    {
        var entries = libraries.Select((library, index) =>
            $"  \"{index}\"\n  {{\n    \"path\" \"{Escape(library)}\"\n  }}");
        return "\"libraryfolders\"\n{\n" + string.Join("\n", entries) + "\n}";
    }

    private static string Escape(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal);

    private sealed class FakeSteamLocator(IReadOnlyList<string> installations) : ISteamInstallationLocator
    {
        public IReadOnlyList<string> LocateInstallations() => installations;
    }
}
