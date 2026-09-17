using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace SableLatch.Desktop;

public sealed class WindowsDeviceProtector : IDeviceProtector
{
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob input, string? description, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr ptr);
    public byte[] Protect(byte[] bytes) => Transform(bytes, true);
    public byte[] Unprotect(byte[] bytes) => Transform(bytes, false);
    private static byte[] Transform(byte[] bytes, bool protect)
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        var id = machine?.GetValue("MachineGuid") as string ?? throw new CryptographicException("Windows machine identity is unavailable.");
        var entropyBytes = SHA256.HashData(Encoding.UTF8.GetBytes("SableLatch/1/device/" + id));
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        var entropy = new Blob { Length = entropyBytes.Length, Data = Marshal.AllocHGlobal(entropyBytes.Length) };
        Blob output = default;
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length); Marshal.Copy(entropyBytes, 0, entropy.Data, entropyBytes.Length);
            var ok = protect ? CryptProtectData(ref input, "SableLatch", ref entropy, IntPtr.Zero, IntPtr.Zero, 1, out output) : CryptUnprotectData(ref input, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new CryptographicException($"Windows device protection is unavailable (code {Marshal.GetLastWin32Error()}). Run SableLatch in your normal Windows user session. Use recovery after moving computers.");
            var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally
        {
            if (output.Data != IntPtr.Zero) { Marshal.Copy(new byte[output.Length], 0, output.Data, output.Length); LocalFree(output.Data); }
            Marshal.Copy(new byte[input.Length], 0, input.Data, input.Length); Marshal.FreeHGlobal(input.Data); Marshal.FreeHGlobal(entropy.Data);
        }
    }
}

public static class Native
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    public static void SetAltTabVisibility(IntPtr hwnd, bool visible)
    {
        if (hwnd == IntPtr.Zero) return;
        const long toolWindow = 0x80, appWindow = 0x40000;
        var style = GetWindowLongPtr(hwnd, -20).ToInt64();
        style = visible ? (style & ~toolWindow) | appWindow : (style & ~appWindow) | toolWindow;
        SetWindowLongPtr(hwnd, -20, new IntPtr(style));
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x1 | 0x2 | 0x4 | 0x10 | 0x20);
    }
    [DllImport("user32.dll", SetLastError=true)] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetWindowDisplayAffinity(IntPtr hWnd, out uint affinity);
    [DllImport("wtsapi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int flags);
    [DllImport("wtsapi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);
    public static (uint Modifiers, uint Key) ParseShortcut(string text)
    {
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) throw new ArgumentException("Use a shortcut such as Ctrl+Alt+Space.");
        uint mods = 0x4000;
        foreach (var part in parts[..^1]) mods |= part.ToLowerInvariant() switch { "ctrl" => 2u, "alt" => 1u, "shift" => 4u, _ => throw new ArgumentException("Use Ctrl, Alt, or Shift modifiers.") };
        if (!Enum.TryParse<Key>(parts[^1], true, out var key) || key == Key.None) throw new ArgumentException("The shortcut key is not recognized.");
        return (mods, (uint)KeyInterop.VirtualKeyFromKey(key));
    }
    public static void SetStartup(bool enabled, string executable, string dataDirectory)
    {
        using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) run.SetValue("SableLatch", "\"" + executable + "\" --tray --data-dir \"" + dataDirectory + "\""); else run.DeleteValue("SableLatch", false);
    }
}

public interface IClipboardService : IDisposable
{
    void Copy(CopyValue value, Preferences settings);
    void ClearOwned();
}
public sealed class ClipboardService : IClipboardService
{
    private readonly Dictionary<string, IClipboardWriter> writers;
    private readonly System.Windows.Threading.DispatcherTimer timer = new();
    private uint ownedSequence;
    private byte[]? digest;
    public ClipboardService(DesktopExtensions? extensions = null)
    {
        writers = extensions?.ClipboardWriters ?? [];
        timer.Tick += (_, _) => ClearOwned();
        Register(new TextClipboardWriter()); Register(new PngClipboardWriter());
    }
    public void Register(IClipboardWriter writer) => writers.Add(writer.MediaType, writer);
    public void Copy(CopyValue value, Preferences settings)
    {
        timer.Stop(); digest = null;
        if (!writers.TryGetValue(value.MediaType, out var writer)) throw new NotSupportedException("This clipboard format needs a registered desktop adapter.");
        var data = new DataObject(); writer.Write(data, value.Data);
        if (value.Sensitive)
        {
            data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[] { 1 }));
            data.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[4]));
            data.SetData("CanUploadToCloudClipboard", new MemoryStream(new byte[4]));
        }
        Clipboard.SetDataObject(data, true);
        ownedSequence = Native.GetClipboardSequenceNumber();
        if (value.Sensitive && settings.ClearSecretClipboard)
        {
            digest = SHA256.HashData(value.Data); ownedMediaType = value.MediaType; timer.Interval = TimeSpan.FromSeconds(settings.ClipboardSeconds); timer.Start();
        }
    }
    private string ownedMediaType = "text/plain";
    public void ClearOwned()
    {
        timer.Stop();
        if (digest is null) return;
        try
        {
            if (Native.GetClipboardSequenceNumber() == ownedSequence)
            {
                if (ownedMediaType != "text/plain") Clipboard.Clear();
                else if (Clipboard.ContainsText())
                {
                    var bytes = Encoding.UTF8.GetBytes(Clipboard.GetText());
                    try { if (CryptographicOperations.FixedTimeEquals(digest, SHA256.HashData(bytes))) Clipboard.Clear(); }
                    finally { CryptographicOperations.ZeroMemory(bytes); }
                }
            }
            digest = null;
        }
        catch (ExternalException) { timer.Interval = TimeSpan.FromSeconds(1); timer.Start(); }
    }
    public void Dispose() { ClearOwned(); timer.Stop(); }
}
