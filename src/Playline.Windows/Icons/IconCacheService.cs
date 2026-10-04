using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;
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
    private const string CacheVersionSuffix = "-hq2.png";
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
            image.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

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
