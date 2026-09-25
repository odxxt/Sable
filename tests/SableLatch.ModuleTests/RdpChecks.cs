using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SableLatch.Core;
using SableLatch.Desktop;

internal static class RdpChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var package = ModulePackages.Read(ModuleTrust.Resource("rdp.zip"), ModuleTrust.Verify);
        check(package.Official && package.Manifest.Handler == "rdp", "Bundled RDP module has a valid publisher signature");
        var profile = new RdpProfile("127.0.0.1", "fixture", Port: 1);
        var password = Encoding.UTF8.GetBytes("fictional-秘密-password"); var frame = RdpBroker.Frame(profile, password);
        var parsed = RdpBroker.Parse(frame);
        check(parsed.Profile == profile && frame.AsSpan(parsed.PasswordOffset, parsed.PasswordLength).SequenceEqual(password), "RDP binary frame preserves Unicode password separately from public settings");
        void Reject(Action action, string name) { try { action(); } catch { check(true, name); return; } throw new Exception(name); }
        Reject(() => RdpBroker.Parse(frame[..^1]), "Truncated RDP frames are rejected");
        Reject(() => RdpBroker.Parse(new byte[RdpBroker.MaxFrame + 1]), "Oversized RDP frames are rejected");
        Reject(() => RdpBroker.Frame(profile, new byte[4097]), "Oversized RDP password is rejected");
        Reject(() => RdpBroker.Frame(profile, [0]), "Null RDP password is rejected");
        using (var executable = new RdpExecutable())
        {
            var info = executable.StartInfo("SableLatch-" + new string('A', 64));
            check(info.ArgumentList.Contains("--rdp-session") && !info.ArgumentList.Any(a => a.Contains("fixture") || a.Contains("password") || a.Contains("127.0.0.1")), "Helper arguments contain only application identity, random channel and parent PID");
        }
        CryptographicOperations.ZeroMemory(frame);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new System.Windows.Forms.Form { ShowInTaskbar = false, Left = -20000, Top = -20000, Width = 800, Height = 600 };
                using var rdp = new RdpControl(); form.Controls.Add(rdp); form.CreateControl(); rdp.CreateControl();
                // Accessing Handle activates the native control without opening a network connection.
                _ = form.Handle; _ = rdp.Handle;
                rdp.Configure(profile);
                var settings = RdpControl.Get(rdp.Client, "AdvancedSettings9");
                check(Convert.ToUInt32(RdpControl.Get(settings, "AuthenticationLevel")) == 1 && Convert.ToBoolean(RdpControl.Get(settings, "EnableCredSspSupport")), "Actual Microsoft RDP control requires server authentication and enables CredSSP");
                check(!Convert.ToBoolean(RdpControl.Get(settings, "RedirectClipboard")) && !Convert.ToBoolean(RdpControl.Get(settings, "RedirectDrives")) && !Convert.ToBoolean(RdpControl.Get(settings, "RedirectPrinters")) && !Convert.ToBoolean(RdpControl.Get(settings, "RedirectPorts")) && !Convert.ToBoolean(RdpControl.Get(settings, "RedirectSmartCards")), "Native control disables clipboard, drives, printers, ports and smart cards by default");
                check(!Convert.ToBoolean(RdpControl.Get(settings, "EnableAutoReconnect")) && Convert.ToInt32(RdpControl.Get(settings, "BitmapPersistence")) == 0, "Native control disables reconnection and persistent bitmap caching");
                check(!GetBool(rdp.Client, 20) && !GetBool(rdp.Client, 46) && !GetBool(rdp.Client, 64), "Native control disables credential prompts and credential saving");
                check(!GetBool(rdp.Client, 26) && !GetBool(rdp.Client, 28), "Native control disables dynamically attached devices and drives");
                RdpSecurity.SetPassword(rdp.Client, password);
                check(true, "Microsoft write-only password interface accepts bounded Unicode BSTR without managed password string");
                rdp.Listen(3, new Action(() => { })); rdp.Listen(4, new Action<int>(_ => { })); rdp.Listen(10, new Action<int>(_ => { }));
                check(true, "Native login, disconnect and error event subscriptions attach");
                rdp.Configure(profile with { Clipboard = true });
                check(Convert.ToBoolean(RdpControl.Get(settings, "RedirectClipboard")), "Explicit per-entry clipboard opt-in reaches the native control");
                done.SetResult();
            }
            catch (Exception ex) { done.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        try { await done.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
        finally { CryptographicOperations.ZeroMemory(password); }
        // Full packaged-host startup and pipe handshake, using only loopback port 1 and fictional data.
        // This is not a successful remote authentication test.
        System.Diagnostics.Process? child = null;
        var transfer = Encoding.UTF8.GetBytes("fictional-rdp-handoff");
        try
        {
            using var executable = new RdpExecutable(); using var deadline = new CancellationTokenSource(15000);
            await RdpBroker.Connect(executable, profile, transfer, deadline.Token, process => { child = System.Diagnostics.Process.GetProcessById(process.Id); _ = child.SafeHandle; }, hidden: true);
            check(transfer.All(b => b == 0), "Real RDP helper accepts the one-use frame and sender clears its password buffer");
            child!.Refresh();
            check(!child.HasExited, "Failed loopback connection leaves a visible error window rather than opening a vault");
            for (var i = 0; i < 30 && child.MainWindowHandle == IntPtr.Zero; i++) { await Task.Delay(100); child.Refresh(); }
            check(child.MainWindowHandle != IntPtr.Zero, "RDP helper owns a session window");
            PostMessage(child.MainWindowHandle, 0x10, IntPtr.Zero, IntPtr.Zero);
            await child.WaitForExitAsync(deadline.Token);
            check(child.ExitCode == 0, "Closing the RDP window cleanly exits its helper process");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(transfer);
            if (child is not null) { if (!child.HasExited) child.Kill(); child.Dispose(); }
        }
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetBoolean(IntPtr self, out short value);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    private static bool GetBool(object control, int slot)
    {
        var unknown = Marshal.GetIUnknownForObject(control); var iid = new Guid("4F6996D5-D7B1-412C-B0FF-063718566907"); IntPtr pointer = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in iid, out pointer));
            Marshal.ThrowExceptionForHR(Marshal.GetDelegateForFunctionPointer<GetBoolean>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer), slot * IntPtr.Size))(pointer, out var value)); return value != 0;
        }
        finally { if (pointer != IntPtr.Zero) Marshal.Release(pointer); Marshal.Release(unknown); }
    }
}
