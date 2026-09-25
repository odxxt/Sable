using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace SableLatch.Desktop;

// Only trusted host code owns these handles. ZIP modules cannot call Win32 or supply launch arguments.
internal sealed class CredentialPipe : IDisposable
{
    public string Name { get; } = "SableLatch-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly NamedPipeServerStream stream;
    public CredentialPipe()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Windows identity unavailable.");
        if (!ConvertStringSecurityDescriptorToSecurityDescriptor("D:P(A;;GA;;;" + sid + ")", 1, out var descriptor, out _)) throw new Win32Exception();
        try
        {
            var attributes = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = descriptor };
            // Duplex permits an async EOF wait after delivery; only the host writes credentials.
            // Overlapped, first instance; byte mode, local clients only; one client.
            var handle = CreateNamedPipe(@"\\.\pipe\" + Name, 0x40080003, 8, 1, 4096, 1, 0, ref attributes);
            if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(); }
            try { stream = new NamedPipeServerStream(PipeDirection.InOut, true, false, handle); }
            catch { handle.Dispose(); throw; }
        }
        finally { LocalFree(descriptor); }
    }
    public async Task Deliver(Process launched, byte[] password, CancellationToken cancellation)
    {
        try
        {
        // Caller retains the live Process handle, preventing a recycled PID from satisfying this check.
        _ = launched.SafeHandle;
        await stream.WaitForConnectionAsync(cancellation).ConfigureAwait(false);
        if (!GetNamedPipeClientProcessId(stream.SafePipeHandle, out var client) || client != launched.Id || launched.HasExited)
            throw new InvalidOperationException("The credential pipe was contacted by an unexpected process. No password was sent.");
        cancellation.ThrowIfCancellationRequested();
        await stream.WriteAsync(password, cancellation).ConfigureAwait(false);
        CryptographicOperations.ZeroMemory(password);
        await stream.WriteAsync(new byte[] { 10 }, cancellation).ConfigureAwait(false);
        // Wait for the client to close its read handle before disposing the server: buffered bytes
        // must not be discarded before PuTTY's stdio reader has consumed the line.
        var eof = new byte[1];
        if (await stream.ReadAsync(eof, cancellation).ConfigureAwait(false) != 0)
            throw new InvalidOperationException("Unexpected response on the credential pipe.");
        }
        finally { CryptographicOperations.ZeroMemory(password); }
    }
    public void Dispose() => stream.Dispose();
    public async Task DeliverFrame(Process launched, byte[] frame, CancellationToken cancellation)
    {
        try
        {
            if (frame.Length is < 1 or > RdpBroker.MaxFrame) throw new InvalidDataException("Invalid session message size.");
            _ = launched.SafeHandle;
            await stream.WaitForConnectionAsync(cancellation).ConfigureAwait(false);
            if (!GetNamedPipeClientProcessId(stream.SafePipeHandle, out var client) || client != launched.Id || launched.HasExited)
                throw new InvalidOperationException("An unexpected process contacted the session channel. No credentials were sent.");
            cancellation.ThrowIfCancellationRequested();
            var length = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, frame.Length);
            await stream.WriteAsync(length, cancellation).ConfigureAwait(false);
            await stream.WriteAsync(frame, cancellation).ConfigureAwait(false);
            CryptographicOperations.ZeroMemory(frame);
            var ack = new byte[1]; await stream.ReadExactlyAsync(ack, cancellation).ConfigureAwait(false);
            if (ack[0] != 0x53) throw new InvalidDataException("The RDP window rejected the session.");
        }
        finally { CryptographicOperations.ZeroMemory(frame); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr Descriptor; public int Inherit; }
    [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl, uint revision, out IntPtr descriptor, out uint size);
    [DllImport("kernel32")] private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafePipeHandle CreateNamedPipe(string name, uint access, uint mode, uint instances, uint outSize, uint inSize, uint timeout, ref SecurityAttributes attributes);
    [DllImport("kernel32", SetLastError = true)] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out int process);
}

internal sealed class VerifiedPutty : IDisposable
{
    public string Path { get; }
    public string Hash { get; }
    private readonly FileStream file;
    public VerifiedPutty(string path)
    {
        if (!System.IO.Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || !File.Exists(path)) throw new InvalidOperationException("Choose a locally installed PuTTY executable.");
        Path = System.IO.Path.GetFullPath(path);
        file = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (file.Length > 32 * 1024 * 1024) throw new InvalidOperationException("This file is not a supported PuTTY executable.");
            Authenticode.Verify(Path);
            var version = FileVersionInfo.GetVersionInfo(Path);
            if (!string.Equals(version.OriginalFilename, "PuTTY", StringComparison.OrdinalIgnoreCase) && !string.Equals(version.OriginalFilename, "putty.exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Select putty.exe from the official PuTTY distribution.");
            if (version.FileMajorPart == 0 && version.FileMinorPart < 77) throw new InvalidOperationException("PuTTY 0.77 or newer is required for protected password delivery.");
            Hash = Convert.ToHexString(SHA256.HashData(file)); file.Position = 0;
        }
        catch { file.Dispose(); throw; }
    }
    public void Dispose() => file.Dispose();
}

internal static class Authenticode
{
    public static void Verify(string path)
    {
        var info = new FileInfoNative { Size = (uint)Marshal.SizeOf<FileInfoNative>(), Path = path };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<FileInfoNative>());
        Marshal.StructureToPtr(info, pointer, false);
        var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, Choice = 1, File = pointer, StateAction = 1, Flags = 0x1000 | 0x80 };
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        try
        {
            // Offline/cached chain validation. No certificate lookups triggered by opening an entry.
            if (WinVerifyTrust(new IntPtr(-1), ref action, ref data) != 0) throw new InvalidOperationException("PuTTY's Windows signature could not be verified. Install the official signed release.");
#pragma warning disable SYSLIB0057
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            if (cert.GetNameInfo(X509NameType.SimpleName, false) != "Simon Tatham") throw new InvalidOperationException("This executable is not signed by PuTTY's expected publisher.");
        }
        finally { data.StateAction = 2; WinVerifyTrust(new IntPtr(-1), ref action, ref data); Marshal.DestroyStructure<FileInfoNative>(pointer); Marshal.FreeHGlobal(pointer); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct FileInfoNative { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr Handle; public IntPtr Subject; }
    [StructLayout(LayoutKind.Sequential)] private struct TrustData { public uint Size; public IntPtr Policy; public IntPtr Sip; public uint Ui; public uint Revocation; public uint Choice; public IntPtr File; public uint StateAction; public IntPtr State; public IntPtr Url; public uint Flags; public uint Context; public IntPtr Signature; }
    [DllImport("wintrust", ExactSpelling = true)] private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
}

internal static class PuttyBroker
{
    public const string DownloadUrl = "https://www.chiark.greenend.org.uk/~sgtatham/putty/latest.html";
    public static string Detect()
    {
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), AppContext.BaseDirectory })
        {
            var path = System.IO.Path.Combine(root, "PuTTY", "putty.exe"); if (File.Exists(path)) return path;
        }
        return "";
    }
    public static async Task Connect(VerifiedPutty executable, PuttyProfile profile, byte[] password, CancellationToken cancellation, Action<Process>? started = null, bool hidden = false)
    {
        Process? child = null;
        var sessionName = "SableLatch-" + Guid.NewGuid().ToString("N");
        var registryPath = @"Software\SimonTatham\PuTTY\Sessions\" + sessionName;
        try
        {
            profile.Validate();
            if (password.Length is < 1 or > 4096 || password.Any(b => b is 0 or 10 or 13)) throw new InvalidOperationException("PuTTY passwords must be a single line, up to 4,096 UTF-8 bytes.");
            if (profile.Session.Length > 0) throw new InvalidOperationException("External PuTTY saved sessions are not enabled for protected connections yet.");
            // A fresh session prevents inherited Default Settings from enabling logging, a local
            // proxy command, agent forwarding, remote commands or connection sharing.
            using (var settings = Registry.CurrentUser.CreateSubKey(registryPath))
            {
                settings.SetValue("Present", 1); settings.SetValue("Protocol", "ssh");
                settings.SetValue("LogType", 0); settings.SetValue("ProxyMethod", 0);
                settings.SetValue("ConnectionSharing", 0); settings.SetValue("AgentFwd", 0); settings.SetValue("TryAgent", 0);
                settings.SetValue("AuthGSSAPI", 0); settings.SetValue("GSSAPIKEX", 0);
                settings.SetValue("RemoteCommand", ""); settings.SetValue("PortForwardings", "");
                settings.SetValue("ProxyTelnetCommand", ""); settings.SetValue("PublicKeyFile", "");
                settings.SetValue("PingInterval", profile.KeepaliveSeconds / 60); settings.SetValue("PingIntervalSecs", profile.KeepaliveSeconds % 60);
                settings.SetValue("Font", profile.Font); settings.SetValue("FontHeight", profile.FontSize); settings.SetValue("FontIsBold", 0); settings.SetValue("FontCharSet", 0);
            }
            using var pipe = new CredentialPipe();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var launch = new ProcessStartInfo(executable.Path) { UseShellExecute = false, WorkingDirectory = System.IO.Path.GetDirectoryName(executable.Path)!, WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal };
            launch.ArgumentList.Add("-load"); launch.ArgumentList.Add(sessionName);
            foreach (var argument in profile.Arguments(pipe.Name)) launch.ArgumentList.Add(argument);
            cancellation.ThrowIfCancellationRequested();
            child = Process.Start(launch) ?? throw new InvalidOperationException("PuTTY could not be started.");
            started?.Invoke(child);
            await pipe.Deliver(child, password, timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            // Only the process started for this pending handoff is stopped. Established sessions survive lock.
            if (child is not null) try { if (!child.HasExited) child.Kill(); } catch (InvalidOperationException) { } catch (Win32Exception) { }
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password); child?.Dispose();
            try { Registry.CurrentUser.DeleteSubKey(registryPath, false); } catch (UnauthorizedAccessException) { } catch (IOException) { }
        }
    }
}
