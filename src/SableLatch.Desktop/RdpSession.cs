using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms.Integration;
using System.Windows.Interop;

namespace SableLatch.Desktop;

internal sealed class RdpControl : System.Windows.Forms.AxHost
{
    // Microsoft's version 9 native control is available on Windows 10 and 11.
    public RdpControl() : base("8B918B82-7985-4C24-89DF-C33AD2BBFBCD") { Dock = System.Windows.Forms.DockStyle.Fill; }
    public object Client => GetOcx() ?? throw new InvalidOperationException("Remote Desktop control is unavailable.");
    private readonly List<(int Id, Delegate Callback)> events = [];
    private static readonly Guid EventId = new("336D5562-EFA8-482E-8CB3-C5C0FC7A7DB6");
    public static object Get(object target, string name) => target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null)!;
    public static void Set(object target, string name, object value) => target.GetType().InvokeMember(name, BindingFlags.SetProperty, null, target, [value]);
    public void Call(string name) => Client.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, Client, null);
    public void Listen(int id, Delegate callback) { ComEventsHelper.Combine(Client, EventId, id, callback); events.Add((id, callback)); }
    public void Configure(RdpProfile profile)
    {
        profile.Validate();
        Set(Client, "Server", profile.Host); Set(Client, "UserName", profile.Username); Set(Client, "Domain", profile.Domain);
        Set(Client, "DesktopWidth", profile.Width); Set(Client, "DesktopHeight", profile.Height); Set(Client, "ColorDepth", 32);
        var settings = Get(Client, "AdvancedSettings9");
        Set(settings, "RDPPort", profile.Port); Set(settings, "SmartSizing", true);
        Set(settings, "EnableCredSspSupport", true); Set(settings, "AuthenticationLevel", 1u);
        Set(settings, "EnableAutoReconnect", false); Set(settings, "BitmapPersistence", 0);
        Set(settings, "RedirectClipboard", profile.Clipboard); Set(settings, "RedirectDrives", false);
        Set(settings, "RedirectPrinters", false); Set(settings, "RedirectPorts", false); Set(settings, "RedirectSmartCards", false);
        Set(settings, "AudioCaptureRedirectionMode", false); Set(settings, "AudioRedirectionMode", 2u);
        // These non-scriptable properties have no IDispatch interface. Slots are from Microsoft's
        // IMsRdpClientNonScriptable5 ABI (inherited through versions 1–4), checked against mstscax.tlb.
        RdpSecurity.SetBool(Client, 19, false); // PromptForCredentials
        RdpSecurity.SetBool(Client, 23, true);  // EnableCredSspSupport
        RdpSecurity.SetBool(Client, 25, false); // RedirectDynamicDrives
        RdpSecurity.SetBool(Client, 27, false); // RedirectDynamicDevices
        RdpSecurity.SetBool(Client, 45, false); // AllowCredentialSaving
        RdpSecurity.SetBool(Client, 63, false); // AllowPromptingForCredentials
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && events.Count > 0)
        {
            foreach (var (id, callback) in events) try { ComEventsHelper.Remove(Client, EventId, id, callback); } catch (COMException) { }
            events.Clear();
        }
        base.Dispose(disposing);
    }
}

internal static class RdpSecurity
{
    private static readonly Guid NonScriptable5 = new("4F6996D5-D7B1-412C-B0FF-063718566907");
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int PutBool(IntPtr self, short value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int PutPassword(IntPtr self, IntPtr bstr);
    private static void WithInterface(object control, Action<IntPtr> use)
    {
        var unknown = Marshal.GetIUnknownForObject(control); IntPtr pointer = IntPtr.Zero;
        try { var iid = NonScriptable5; Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in iid, out pointer)); use(pointer); }
        finally { if (pointer != IntPtr.Zero) Marshal.Release(pointer); Marshal.Release(unknown); }
    }
    public static void SetBool(object control, int slot, bool value) => WithInterface(control, pointer =>
        Marshal.ThrowExceptionForHR(Marshal.GetDelegateForFunctionPointer<PutBool>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer), slot * IntPtr.Size))(pointer, value ? (short)-1 : (short)0)));
    public static void SetPassword(object control, ReadOnlySpan<byte> utf8)
    {
        var encoding = new UTF8Encoding(false, true);
        var chars = new char[encoding.GetCharCount(utf8)]; IntPtr bstr = IntPtr.Zero;
        try
        {
            encoding.GetChars(utf8, chars);
            if (chars.Length == 0 || chars.Contains('\0')) throw new InvalidDataException("Invalid RDP password.");
            bstr = SysAllocStringLen(IntPtr.Zero, (uint)chars.Length);
            if (bstr == IntPtr.Zero) throw new OutOfMemoryException();
            Marshal.Copy(chars, 0, bstr, chars.Length);
            WithInterface(control, pointer => Marshal.ThrowExceptionForHR(Marshal.GetDelegateForFunctionPointer<PutPassword>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer), 3 * IntPtr.Size))(pointer, bstr)));
        }
        finally { CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(chars.AsSpan())); if (bstr != IntPtr.Zero) Marshal.ZeroFreeBSTR(bstr); }
        // The control owns its own copy. This does not erase the unlocked vault or Windows' copies.
    }
    [DllImport("oleaut32.dll")] private static extern IntPtr SysAllocStringLen(IntPtr source, uint length);
}

internal sealed class RdpSessionWindow : Window
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly RdpControl control = new();
    private readonly WindowsFormsHost host = new();
    private readonly TextBlock status = new() { Text = "Opening protected session…", VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private bool loggedIn, released;
    public RdpSessionWindow(string pipeName, int parentId)
    {
        Title = "SableLatch · Remote Desktop"; Width = 1100; Height = 740; MinWidth = 640; MinHeight = 480;
        Background = new SolidColorBrush(Color.FromRgb(20, 21, 23)); Foreground = Brushes.White;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var layout = new DockPanel(); Content = layout;
        var footer = new DockPanel { Margin = new Thickness(12, 8, 12, 8), LastChildFill = true }; DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        var close = new Button { Content = "Disconnect", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(10, 0, 0, 0) }; close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Right); footer.Children.Add(close);
        var fullscreen = new Button { Content = "Full screen", Padding = new Thickness(12, 6, 12, 6) }; DockPanel.SetDock(fullscreen, Dock.Right); footer.Children.Add(fullscreen);
        fullscreen.Click += (_, _) => { var full = WindowStyle != WindowStyle.None; WindowStyle = full ? WindowStyle.None : WindowStyle.SingleBorderWindow; WindowState = full ? WindowState.Maximized : WindowState.Normal; fullscreen.Content = full ? "Window" : "Full screen"; };
        footer.Children.Add(status); host.Child = control; layout.Children.Add(host);
        SourceInitialized += (_, _) => CaptureProtection.Protect(new WindowInteropHelper(this).Handle);
        Closed += (_, _) => { lifetime.Cancel(); ReleaseControl(); lifetime.Dispose(); };
        Loaded += async (_, _) => await Receive(pipeName, parentId);
    }
    private void ReleaseControl()
    {
        if (released) return; released = true;
        try { if (Convert.ToInt32(RdpControl.Get(control.Client, "Connected")) != 0) control.Call("Disconnect"); } catch (Exception ex) when (ex is COMException or InvalidOperationException or TargetInvocationException) { }
        host.Child = null; control.Dispose();
    }
    private void Failed(string message)
    {
        if (released) return;
        status.Text = message; ReleaseControl();
    }
    private async Task Receive(string pipeName, int parentId)
    {
        byte[]? frame = null;
        try
        {
            using var parent = Process.GetProcessById(parentId); _ = parent.SafeHandle;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Identification))
            {
                await pipe.ConnectAsync(timeout.Token);
                if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var server) || server != parentId || parent.HasExited) throw new InvalidDataException("Unexpected session sender.");
                var header = new byte[4]; await pipe.ReadExactlyAsync(header, timeout.Token);
                var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (length is < 10 or > RdpBroker.MaxFrame) throw new InvalidDataException("Invalid session size.");
                frame = new byte[length]; await pipe.ReadExactlyAsync(frame, timeout.Token);
                var message = RdpBroker.Parse(frame); control.CreateControl(); control.Configure(message.Profile);
                Title = "SableLatch · " + message.Profile.Host;
                control.Listen(3, new Action(() => { loggedIn = true; status.Text = "Connected · " + message.Profile.Host; }));
                control.Listen(4, new Action<int>(reason => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (released) return;
                    if (loggedIn) Close(); else Failed("Connection ended (" + reason + "). Check the address, account, and trusted server certificate, then launch again from SableLatch.");
                }))));
                control.Listen(10, new Action<int>(code => Dispatcher.BeginInvoke(new Action(() => Failed("Remote Desktop could not start (" + code + "). Close this window and try again.")))));
                timeout.Token.ThrowIfCancellationRequested();
                RdpSecurity.SetPassword(control.Client, frame.AsSpan(message.PasswordOffset, message.PasswordLength));
                CryptographicOperations.ZeroMemory(frame);
                control.Call("Connect"); status.Text = "Connecting · " + message.Profile.Host;
                await pipe.WriteAsync(new byte[] { 0x53 }, timeout.Token);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or COMException or InvalidOperationException or ArgumentException or TargetInvocationException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        { Failed("Unable to open this RDP session. Check that Windows Remote Desktop components are available, then try again."); }
        finally { if (frame is not null) CryptographicOperations.ZeroMemory(frame); }
    }
    [DllImport("kernel32", SetLastError = true)] private static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out int process);
}
