using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Playline.Windows.Icons;

public sealed class IconCacheService(string iconsDirectory)
{
    private const uint ShellGetFileInfoIcon = 0x000000100;
    private const uint ShellGetFileInfoSystemIconIndex = 0x000004000;
    private const uint ShellGetFileInfoLargeIcon = 0x000000000;
    private const int ShellImageListJumbo = 0x4;
    private const uint ImageListDrawTransparent = 0x1;
    private const uint CoinitMultithreaded = 0x0;
    private const int RpcChangedMode = unchecked((int)0x80010106);
    private const string CacheVersionSuffix = "-hq3.png";
    private static readonly Guid ImageListInterfaceId = new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    public Task<string?> GetOrCreateAsync(
        string cacheKey,
        string? executablePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        return Task.Run(
            () => GetOrCreate(cacheKey, executablePath, cancellationToken),
            cancellationToken);
    }

    public bool IsManagedCachePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var candidateDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
            var cacheDirectory = Path.GetFullPath(iconsDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(candidateDirectory, cacheDirectory, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private string? GetOrCreate(
        string cacheKey,
        string? executablePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        Directory.CreateDirectory(iconsDirectory);
        var cachePath = Path.Combine(iconsDirectory, $"{Hash(cacheKey)}{CacheVersionSuffix}");
        if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0)
        {
            return cachePath;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var comResult = CoInitializeEx(IntPtr.Zero, CoinitMultithreaded);
        if (comResult < 0 && comResult != RpcChangedMode)
        {
            return null;
        }

        var shouldUninitializeCom = comResult >= 0;
        var fileInfo = new ShellFileInfo();
        var imageList = IntPtr.Zero;
        var temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            var result = ShellGetFileInfo(
                Path.GetFullPath(executablePath),
                0,
                ref fileInfo,
                (uint)Marshal.SizeOf<ShellFileInfo>(),
                ShellGetFileInfoSystemIconIndex);

            if (result != IntPtr.Zero)
            {
                var interfaceId = ImageListInterfaceId;
                var imageListResult = SHGetImageList(
                    ShellImageListJumbo,
                    ref interfaceId,
                    out imageList);
                if (imageListResult >= 0 && imageList != IntPtr.Zero)
                {
                    fileInfo.IconHandle = ImageListGetIcon(
                        imageList,
                        fileInfo.IconIndex,
                        ImageListDrawTransparent);
                }
            }

            if (fileInfo.IconHandle == IntPtr.Zero)
            {
                result = ShellGetFileInfo(
                    Path.GetFullPath(executablePath),
                    0,
                    ref fileInfo,
                    (uint)Marshal.SizeOf<ShellFileInfo>(),
                    ShellGetFileInfoIcon | ShellGetFileInfoLargeIcon);
            }

            if (result == IntPtr.Zero || fileInfo.IconHandle == IntPtr.Zero)
            {
                return null;
            }

            var image = Imaging.CreateBitmapSourceFromHIcon(
                fileInfo.IconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            var normalizedImage = CropTransparentPadding(image);
            normalizedImage.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(normalizedImage));

            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                encoder.Save(stream);
            }

            File.Move(temporaryPath, cachePath, overwrite: true);
            return cachePath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
        finally
        {
            if (fileInfo.IconHandle != IntPtr.Zero)
            {
                _ = DestroyIcon(fileInfo.IconHandle);
            }

            if (imageList != IntPtr.Zero)
            {
                _ = Marshal.Release(imageList);
            }

            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }

            if (shouldUninitializeCom)
            {
                CoUninitialize();
            }
        }
    }

    private static string Hash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static BitmapSource CropTransparentPadding(BitmapSource source)
    {
        BitmapSource readableSource = source;
        if (source.Format != PixelFormats.Bgra32)
        {
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            converted.Freeze();
            readableSource = converted;
        }

        var stride = checked(readableSource.PixelWidth * 4);
        var pixels = new byte[checked(stride * readableSource.PixelHeight)];
        readableSource.CopyPixels(pixels, stride, 0);
        var bounds = FindVisibleBounds(
            pixels,
            readableSource.PixelWidth,
            readableSource.PixelHeight,
            stride);
        if (bounds is not { } visibleBounds)
        {
            return readableSource;
        }

        var left = Math.Max(0, visibleBounds.X - 1);
        var top = Math.Max(0, visibleBounds.Y - 1);
        var right = Math.Min(readableSource.PixelWidth, visibleBounds.X + visibleBounds.Width + 1);
        var bottom = Math.Min(readableSource.PixelHeight, visibleBounds.Y + visibleBounds.Height + 1);
        if (left == 0
            && top == 0
            && right == readableSource.PixelWidth
            && bottom == readableSource.PixelHeight)
        {
            return readableSource;
        }

        return new CroppedBitmap(
            readableSource,
            new Int32Rect(left, top, right - left, bottom - top));
    }

    internal static (int X, int Y, int Width, int Height)? FindVisibleBounds(
        byte[] pixels,
        int width,
        int height,
        int stride)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0 || stride < width * 4 || pixels.Length < stride * height)
        {
            throw new ArgumentOutOfRangeException(nameof(pixels));
        }

        var minimumX = width;
        var minimumY = height;
        var maximumX = -1;
        var maximumY = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                const byte minimumVisibleAlpha = 8;
                if (pixels[(y * stride) + (x * 4) + 3] <= minimumVisibleAlpha)
                {
                    continue;
                }

                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
            }
        }

        return maximumX < 0
            ? null
            : (minimumX, minimumY, maximumX - minimumX + 1, maximumY - minimumY + 1);
    }

    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)]
    private static extern IntPtr ShellGetFileInfo(
        string path,
        uint fileAttributes,
        ref ShellFileInfo fileInfo,
        uint fileInfoSize,
        uint flags);

    [DllImport("shell32.dll")]
    private static extern int SHGetImageList(
        int imageList,
        ref Guid interfaceId,
        out IntPtr imageListHandle);

    [DllImport("comctl32.dll", EntryPoint = "ImageList_GetIcon")]
    private static extern IntPtr ImageListGetIcon(
        IntPtr imageListHandle,
        int iconIndex,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint concurrencyModel);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr IconHandle;
        public int IconIndex;
        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }
}
