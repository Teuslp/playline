using System.Runtime.InteropServices;
using System.Text;

namespace Playline.Windows.Windowing;

public static class DesktopWindowLevelService
{
    private const uint GetWindowNext = 2;
    private const uint SetWindowPositionNoSize = 0x0001;
    private const uint SetWindowPositionNoMove = 0x0002;
    private const uint SetWindowPositionNoActivate = 0x0010;
    private const uint SetWindowPositionNoOwnerZOrder = 0x0200;

    public static bool PlaceImmediatelyAboveDesktop(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        var desktopHandle = FindDesktopWindow(windowHandle);
        return desktopHandle != IntPtr.Zero
            && SetWindowPos(
                windowHandle,
                desktopHandle,
                0,
                0,
                0,
                0,
                SetWindowPositionNoSize
                | SetWindowPositionNoMove
                | SetWindowPositionNoActivate
                | SetWindowPositionNoOwnerZOrder);
    }

    private static IntPtr FindDesktopWindow(IntPtr excludedWindow)
    {
        var window = GetTopWindow(IntPtr.Zero);
        var className = new StringBuilder(64);
        while (window != IntPtr.Zero)
        {
            if (window != excludedWindow && IsWindowVisible(window))
            {
                className.Clear();
                _ = GetClassName(window, className, className.Capacity);
                if (className.ToString() is "WorkerW" or "Progman")
                {
                    return window;
                }
            }

            window = GetWindow(window, GetWindowNext);
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetTopWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr windowHandle, uint command);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(
        IntPtr windowHandle,
        StringBuilder className,
        int maximumCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
