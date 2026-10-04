using Playline.Core.Models;
using Playline.Discovery.Steam;
using Playline.Tests.TestSupport;

namespace Playline.Tests.Discovery;

public sealed class SteamArtworkLocatorTests
{
    [Fact]
    public void Locate_WhenHashedSteamIconExists_ReturnsIt()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var iconDirectory = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "appcache",
            "librarycache",
            "730");
        Directory.CreateDirectory(iconDirectory);
        var iconPath = Path.Combine(iconDirectory, new string('a', 40) + ".jpg");
        File.WriteAllBytes(iconPath, [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(iconDirectory, "header.jpg"), [4, 5, 6]);
        var locator = new SteamArtworkLocator(new FakeSteamLocator([temporaryDirectory.DirectoryPath]));

        var result = locator.Locate(new Game
        {
            Id = "steam-730",
            Name = "Counter-Strike 2",
            Source = GameSource.Steam,
            LaunchUri = "steam://rungameid/730"
        });

        Assert.Equal(iconPath, result);
    }

    [Fact]
    public void Locate_WhenIdIsInvalid_DoesNotInspectCache()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var locator = new SteamArtworkLocator(new FakeSteamLocator([temporaryDirectory.DirectoryPath]));

        var result = locator.Locate(new Game
        {
            Id = "steam-invalid",
            Name = "Invalid",
            Source = GameSource.Steam
        });

        Assert.Null(result);
    }

    private sealed class FakeSteamLocator(IReadOnlyList<string> installations) : ISteamInstallationLocator
    {
        public IReadOnlyList<string> LocateInstallations() => installations;
    }
}
