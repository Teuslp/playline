using Playline.Core.Contracts;
using Playline.Core.Models;
using Playline.Tests.TestSupport;
using Playline.Windows.Icons;

namespace Playline.Tests.Windows;

public sealed class GameArtworkServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_WhenCustomImageExists_PreservesIt()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var imagePath = Path.Combine(temporaryDirectory.DirectoryPath, "custom.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
        var service = new GameArtworkService(
            new IconCacheService(Path.Combine(temporaryDirectory.DirectoryPath, "cache")),
            []);

        var result = await service.GetOrCreateAsync(new Game
        {
            Id = "custom",
            Name = "Custom",
            IconPath = imagePath
        });

        Assert.Equal(imagePath, result);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenLauncherArtworkExists_UsesLocator()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var imagePath = Path.Combine(temporaryDirectory.DirectoryPath, "launcher.jpg");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);
        var service = new GameArtworkService(
            new IconCacheService(Path.Combine(temporaryDirectory.DirectoryPath, "cache")),
            [new FixedArtworkLocator(imagePath)]);

        var executablePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "notepad.exe");
        var result = await service.GetOrCreateAsync(new Game
        {
            Id = "launcher",
            Name = "Launcher",
            ExecutablePath = executablePath
        });

        Assert.Equal(imagePath, result);
    }

    private sealed class FixedArtworkLocator(string path) : IGameArtworkLocator
    {
        public string? Locate(Game game) => path;
    }
}
