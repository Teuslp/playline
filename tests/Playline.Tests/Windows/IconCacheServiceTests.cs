using System.Buffers.Binary;
using Playline.Tests.TestSupport;
using Playline.Windows.Icons;

namespace Playline.Tests.Windows;

public sealed class IconCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_ReusesCachedPng()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var executablePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "notepad.exe");
        var service = new IconCacheService(temporaryDirectory.DirectoryPath);

        var firstPath = await service.GetOrCreateAsync("notepad", executablePath);
        var firstWriteTime = File.GetLastWriteTimeUtc(Assert.IsType<string>(firstPath));
        var secondPath = await service.GetOrCreateAsync("notepad", executablePath);

        Assert.Equal(firstPath, secondPath);
        Assert.True(File.Exists(firstPath));
        Assert.Equal(firstWriteTime, File.GetLastWriteTimeUtc(firstPath));
        Assert.Equal(".png", Path.GetExtension(firstPath), ignoreCase: true);
        Assert.EndsWith("-hq2.png", firstPath, StringComparison.OrdinalIgnoreCase);

        var png = await File.ReadAllBytesAsync(firstPath);
        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        Assert.True(width >= 128, $"Expected a high-resolution icon, got {width}x{height}.");
        Assert.True(height >= 128, $"Expected a high-resolution icon, got {width}x{height}.");
    }
}
