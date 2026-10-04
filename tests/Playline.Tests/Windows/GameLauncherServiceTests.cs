using Playline.Core.Models;
using Playline.Tests.TestSupport;
using Playline.Windows.Launching;

namespace Playline.Tests.Windows;

public sealed class GameLauncherServiceTests
{
    [Fact]
    public void CreateStartInfo_ForExecutable_PreservesArgumentsAndWorkingDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var executablePath = Path.Combine(temporaryDirectory.DirectoryPath, "Game.exe");
        File.WriteAllBytes(executablePath, []);
        var game = new Game
        {
            Name = "Game",
            ExecutablePath = executablePath,
            Arguments = "--safe",
            WorkingDirectory = temporaryDirectory.DirectoryPath
        };

        var startInfo = GameLauncherService.CreateStartInfo(game);

        Assert.Equal(Path.GetFullPath(executablePath), startInfo.FileName, ignoreCase: true);
        Assert.Equal("--safe", startInfo.Arguments);
        Assert.Equal(Path.GetFullPath(temporaryDirectory.DirectoryPath), startInfo.WorkingDirectory, ignoreCase: true);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void CreateStartInfo_ForSteamUri_UsesWindowsShell()
    {
        var game = new Game
        {
            Name = "Steam Game",
            LaunchUri = "steam://rungameid/730",
            Source = GameSource.Steam
        };

        var startInfo = GameLauncherService.CreateStartInfo(game);

        Assert.Equal("steam://rungameid/730", startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
    }

    [Fact]
    public void CreateStartInfo_ForEpicUri_UsesWindowsShell()
    {
        var game = new Game
        {
            Name = "Epic Game",
            LaunchUri = "com.epicgames.launcher://apps/ns%3Aitem%3Aartifact?action=launch&silent=true",
            Source = GameSource.Epic
        };

        var startInfo = GameLauncherService.CreateStartInfo(game);

        Assert.Equal(game.LaunchUri, startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
    }

    [Fact]
    public void CreateStartInfo_WhenUriSchemeIsNotAllowed_RejectsIt()
    {
        var game = new Game
        {
            Name = "Unsafe URI",
            LaunchUri = "https://example.com/game"
        };

        Assert.Throws<ArgumentException>(() => GameLauncherService.CreateStartInfo(game));
    }

    [Fact]
    public async Task LaunchAsync_WhenExecutableIsMissing_ReturnsFailureAndLogs()
    {
        var logger = new RecordingLogger();
        var service = new GameLauncherService(logger);

        var result = await service.LaunchAsync(new Game
        {
            Name = "Missing",
            ExecutablePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe")
        });

        Assert.False(result.Success);
        Assert.Single(logger.Entries);
    }
}
