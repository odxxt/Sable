using System.Runtime.InteropServices;

namespace SableLatch.Desktop;

internal static class MonitorPlacement
{
    internal static bool Place(IntPtr window, bool dock)
    {
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(window, 2), ref info) || !GetWindowRect(window, out var rect)) return false;
        var area = new ScreenBounds(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom);
        uint dpi = 96; try { dpi = GetDpiForWindow(window); } catch (EntryPointNotFoundException) { }
        var margin = (int)Math.Round(18 * Math.Max(96, dpi) / 96d);
        var point = dock ? WindowPlacement.BottomRight(area, rect.Right - rect.Left, rect.Bottom - rect.Top, margin) : WindowPlacement.Clamp(area, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return SetWindowPos(window, IntPtr.Zero, point.X, point.Y, 0, 0, 0x1 | 0x4 | 0x10);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
