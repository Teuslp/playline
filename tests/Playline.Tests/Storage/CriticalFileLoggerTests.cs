using Playline.Storage.Logging;
using Playline.Tests.TestSupport;

namespace Playline.Tests.Storage;

public sealed class CriticalFileLoggerTests
{
    [Fact]
    public async Task LogAsync_RotatesAndBoundsRetainedFiles()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var logger = new CriticalFileLogger(
            temporaryDirectory.DirectoryPath,
            maximumFileBytes: 512,
            retainedFileCount: 3);

        for (var index = 0; index < 30; index++)
        {
            await logger.LogAsync($"Failure {index}: {new string('x', 80)}");
        }

        var files = Directory.GetFiles(temporaryDirectory.DirectoryPath, "*.log");
        Assert.InRange(files.Length, 2, 3);
        Assert.All(files, file => Assert.InRange(new FileInfo(file).Length, 1, 700));
        Assert.Contains("Failure 29", await File.ReadAllTextAsync(files.OrderByDescending(File.GetLastWriteTimeUtc).First()));
    }

    [Fact]
    public async Task LogAsync_SerializesConcurrentWrites()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var logger = new CriticalFileLogger(temporaryDirectory.DirectoryPath);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(index => logger.LogAsync($"Concurrent {index}")));

        var content = string.Join(
            Environment.NewLine,
            Directory.GetFiles(temporaryDirectory.DirectoryPath, "*.log").Select(File.ReadAllText));
        for (var index = 0; index < 20; index++)
        {
            Assert.Contains($"Concurrent {index}", content, StringComparison.Ordinal);
        }
    }
}
