using System.Runtime.InteropServices;

namespace Playline.Tests.TestSupport;

internal static class WindowsShortcutTestHelper
{
    public static void Create(
        string shortcutPath,
        string targetPath,
        string? arguments = null,
        string? workingDirectory = null)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows Script Host is unavailable.");
        object? shell = null;
        object? shortcut = null;

        try
        {
            shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Windows Script Host could not be created.");
            dynamic dynamicShell = shell;
            shortcut = dynamicShell.CreateShortcut(shortcutPath);
            dynamic dynamicShortcut = shortcut;
            dynamicShortcut.TargetPath = targetPath;
            dynamicShortcut.Arguments = arguments ?? string.Empty;
            dynamicShortcut.WorkingDirectory = workingDirectory ?? string.Empty;
            dynamicShortcut.Save();
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                Marshal.FinalReleaseComObject(shortcut);
            }

            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}

