using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SableLatch.Desktop;

// One bounded foreground handoff for an explicitly clicked network diagnostic.
// Supports classic console hosts and Windows Terminal without a resident watcher.
internal static class DiagnosticTerminal
{
    internal static string SetTitle(ProcessStartInfo start)
    {
        var marker = "Sable network check " + Guid.NewGuid().ToString("N");
        var script = Encoding.Unicode.GetString(Convert.FromBase64String(start.ArgumentList[^1]));
        start.ArgumentList[^1] = Convert.ToBase64String(Encoding.Unicode.GetBytes("$Host.UI.RawUI.WindowTitle = '" + marker + "'; " + script));
        return marker;
    }

    internal static async Task<bool> Run(ProcessStartInfo start, IntPtr owner, CancellationToken cancellation)
    {
        var marker = SetTitle(start);
        var input = LastInput();
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The diagnostic terminal could not start.");
        _ = AllowSetForegroundWindow((uint)process.Id);
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            var window = Find(process.Id, marker);
            var foreground = GetForegroundWindow();
            if (window != IntPtr.Zero)
            {
                if (foreground != owner && foreground != window && LastInput() != input) return false;
                if (foreground == window) return true;
                _ = ShowWindowAsync(window, IsIconic(window) ? 9 : 5);
                _ = SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, 0x1 | 0x2 | 0x10 | 0x40);
                _ = SetForegroundWindow(window);
            }
            if (process.HasExited) return false;
            await Task.Delay(100, cancellation);
        }
        return false; // Windows policy may decline focus; the diagnostic remains open.
    }

    private static IntPtr Find(int process, string marker)
    {
        var found = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window)) return true;
            GetWindowThreadProcessId(window, out var owner);
            var type = new StringBuilder(128); GetClassName(window, type, type.Capacity);
            if (type.ToString() is not ("ConsoleWindowClass" or "CASCADIA_HOSTING_WINDOW_CLASS")) return true;
            var title = new StringBuilder(512); GetWindowText(window, title, title.Capacity);
            if (owner != process && !title.ToString().Contains(marker, StringComparison.Ordinal)) return true;
            found = window; return false;
        }, IntPtr.Zero);
        return found;
    }
    private static uint LastInput() { var state = new InputState { Size = (uint)Marshal.SizeOf<InputState>() }; return GetLastInputInfo(ref state) ? state.Tick : 0; }
    [StructLayout(LayoutKind.Sequential)] private struct InputState { internal uint Size, Tick; }
    private delegate bool Visit(IntPtr window, IntPtr data);
    [DllImport("user32")] private static extern bool EnumWindows(Visit callback, IntPtr data);
    [DllImport("user32")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int count);
    [DllImport("user32")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32")] private static extern bool GetLastInputInfo(ref InputState state);
    [DllImport("user32")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32")] private static extern bool AllowSetForegroundWindow(uint process);
    [DllImport("user32")] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
