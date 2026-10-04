using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Playline.Windows.Shortcuts;

public sealed class ShellLinkShortcutResolver
{
    private const uint ResolveWithoutUserInterface = 0x0001;
    private const int BufferSize = 32768;

    public ShortcutTarget Resolve(string shortcutPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortcutPath);

        var fullPath = Path.GetFullPath(shortcutPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The shortcut file does not exist.", fullPath);
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".lnk", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The selected file is not a Windows shortcut.", nameof(shortcutPath));
        }

        var shellLink = (IShellLinkW)(object)new ShellLink();

        try
        {
            ((IPersistFile)shellLink).Load(fullPath, 0);
            ThrowIfFailed(shellLink.Resolve(IntPtr.Zero, ResolveWithoutUserInterface));

            var targetPath = new StringBuilder(BufferSize);
            ThrowIfFailed(shellLink.GetPath(targetPath, targetPath.Capacity, IntPtr.Zero, 0));

            var arguments = new StringBuilder(BufferSize);
            ThrowIfFailed(shellLink.GetArguments(arguments, arguments.Capacity));

            var workingDirectory = new StringBuilder(BufferSize);
            ThrowIfFailed(shellLink.GetWorkingDirectory(workingDirectory, workingDirectory.Capacity));

            var expandedTarget = Environment.ExpandEnvironmentVariables(targetPath.ToString());
            if (string.IsNullOrWhiteSpace(expandedTarget))
            {
                throw new InvalidDataException("The shortcut does not contain a target path.");
            }

            return new ShortcutTarget(
                Path.GetFullPath(expandedTarget),
                NullIfWhiteSpace(arguments.ToString()),
                NullIfWhiteSpace(Environment.ExpandEnvironmentVariables(workingDirectory.ToString())));
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLink);
        }
    }

    private static string? NullIfWhiteSpace(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ThrowIfFailed(int result)
    {
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink;

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        [PreserveSig]
        int GetPath(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file,
            int maximumPath,
            IntPtr findData,
            uint flags);

        void GetIDList(out IntPtr itemIdList);

        void SetIDList(IntPtr itemIdList);

        [PreserveSig]
        int GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maximumName);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

        [PreserveSig]
        int GetWorkingDirectory(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory,
            int maximumPath);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);

        [PreserveSig]
        int GetArguments(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments,
            int maximumArguments);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);

        void GetHotkey(out short hotkey);

        void SetHotkey(short hotkey);

        void GetShowCmd(out int showCommand);

        void SetShowCmd(int showCommand);

        void GetIconLocation(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath,
            int iconPathLength,
            out int iconIndex);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);

        [PreserveSig]
        int Resolve(IntPtr windowHandle, uint flags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
