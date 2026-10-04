using Playline.Tests.TestSupport;
using Playline.Windows.Shortcuts;

namespace Playline.Tests.Windows;

public sealed class ShellLinkShortcutResolverTests
{
    [Fact]
    public void Resolve_ReturnsTargetArgumentsAndWorkingDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var targetPath = Path.Combine(temporaryDirectory.DirectoryPath, "Game.exe");
        var shortcutPath = Path.Combine(temporaryDirectory.DirectoryPath, "My Game.lnk");
        File.WriteAllBytes(targetPath, []);
        WindowsShortcutTestHelper.Create(
            shortcutPath,
            targetPath,
            "--profile test",
            temporaryDirectory.DirectoryPath);

        var result = new ShellLinkShortcutResolver().Resolve(shortcutPath);

        Assert.Equal(Path.GetFullPath(targetPath), result.TargetPath, ignoreCase: true);
        Assert.Equal("--profile test", result.Arguments);
        Assert.Equal(Path.GetFullPath(temporaryDirectory.DirectoryPath), result.WorkingDirectory, ignoreCase: true);
    }

    [Fact]
    public void Resolve_WhenShortcutIsCorrupt_ThrowsWithoutRetainingTheFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var shortcutPath = Path.Combine(temporaryDirectory.DirectoryPath, "Corrupt.lnk");
        File.WriteAllText(shortcutPath, "not a shell link");

        Assert.ThrowsAny<Exception>(() => new ShellLinkShortcutResolver().Resolve(shortcutPath));

        File.Delete(shortcutPath);
        Assert.False(File.Exists(shortcutPath));
    }
}
