using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace SableLatch.Desktop;

// The fixed Windows system client owns the session. No shell or module-provided executable.
internal sealed class NativeRdpExecutable : IDisposable
{
    private readonly FileStream file;
    public string Path { get; } = System.IO.Path.Combine(Environment.SystemDirectory, "mstsc.exe");
    public string Hash { get; }
    public NativeRdpExecutable()
    {
        file = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var version = FileVersionInfo.GetVersionInfo(Path);
            if (!string.Equals(version.InternalName, "mstsc.exe", StringComparison.OrdinalIgnoreCase) || !(string.Equals(version.OriginalFilename, "mstsc.exe", StringComparison.OrdinalIgnoreCase) || string.Equals(version.OriginalFilename, "mstsc.exe.mui", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Windows Remote Desktop Connection is unavailable.");
            Hash = Convert.ToHexString(SHA256.HashData(file));
        }
        catch { file.Dispose(); throw; }
    }
    public ProcessStartInfo StartInfo(string settingsPath)
    {
        if (!System.IO.Path.IsPathFullyQualified(settingsPath) || !settingsPath.EndsWith(".rdp", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid Remote Desktop settings file.");
        var launch = new ProcessStartInfo(Path) { UseShellExecute = false, WorkingDirectory = Environment.SystemDirectory };
        launch.ArgumentList.Add(settingsPath); return launch;
    }
    public void Dispose() => file.Dispose();
}

internal sealed class NativeRdpSettings : IDisposable
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, NativeRdpSettings> active = new(StringComparer.OrdinalIgnoreCase);
    public string Path { get; }
    private readonly FileStream file;
    public NativeRdpSettings(string directory, RdpProfile profile, byte[] password, Func<byte[], byte[]>? protectForTest = null)
    {
        byte[]? unicode = null; byte[]? encrypted = null;
        try
        {
            var settings = RdpConnectionFile.Create(profile, false);
            if (password.Length is < 1 or > 4096 || password.Contains((byte)0)) throw new InvalidDataException("Invalid RDP password.");
            // Strict UTF-8 -> UTF-16LE without creating an immutable plaintext string.
            unicode = Encoding.Convert(new UTF8Encoding(false, true), Encoding.Unicode, password);
            encrypted = protectForTest is null ? ProtectedData.Protect(unicode, null, DataProtectionScope.CurrentUser) : protectForTest(unicode);
            EnsureDirectory(directory);
            Path = System.IO.Path.Combine(directory, "rdp-" + Guid.NewGuid().ToString("N") + ".rdp");
            using (var output = new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var encoding = new UnicodeEncoding(false, true);
                // mstsc understands this DPAPI blob. No vault key or plaintext password reaches disk.
                output.Write(encoding.GetPreamble());
                output.Write(encoding.GetBytes(settings + "password 51:b:" + Convert.ToHexString(encrypted) + "\r\n"));
                output.Flush(true);
            }
            file = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            active[Path] = this;
        }
        catch
        {
            if (file is not null) file.Dispose();
            if (Path is not null) TryDelete(Path);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
            if (unicode is not null) CryptographicOperations.ZeroMemory(unicode);
            if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
        }
    }
    public void Dispose()
    {
        file.Dispose(); active.TryRemove(Path, out _); TryDelete(Path);
    }
    internal static void ReleaseActive()
    {
        foreach (var settings in active.Values) settings.Dispose();
    }
    internal static void EnsureDirectory(string directory)
    {
        directory = System.IO.Path.GetFullPath(directory);
        if (directory.StartsWith(@"\\", StringComparison.Ordinal) || new DriveInfo(System.IO.Path.GetPathRoot(directory)!).DriveType == DriveType.Network)
            throw new IOException("Remote Desktop launch files require a local folder.");
        // Do not follow junctions/symlinks when creating or cleaning a credential-bearing directory.
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Remote Desktop launch folders cannot use links.");
        var sid = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Windows identity unavailable.");
        var security = new DirectorySecurity(); security.SetOwner(sid); security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        var folder = new DirectoryInfo(directory);
        folder.Create(security); folder.SetAccessControl(security);
    }
    internal static void Cleanup(string directory)
    {
        if (!Directory.Exists(directory)) return;
        EnsureDirectory(directory);
        foreach (var path in Directory.EnumerateFiles(directory, "rdp-*.rdp", SearchOption.TopDirectoryOnly))
            if (System.Text.RegularExpressions.Regex.IsMatch(System.IO.Path.GetFileName(path), "^rdp-[a-f0-9]{32}\\.rdp$") && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0) TryDelete(path);
    }
    internal static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

internal static class NativeRdpLauncher
{
    // Local per-vault scratch space, outside portable exports/backups and network vault folders.
    internal static string DirectoryFor(string vaultDirectory) => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SableLatch", "RdpLaunch",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(vaultDirectory).ToUpperInvariant())))[..24]);

    internal static async Task Connect(NativeRdpExecutable executable, string directory, RdpProfile profile, byte[] password,
        CancellationToken cancellation, Func<ProcessStartInfo, CancellationToken, Task>? startAndWait = null, Func<byte[], byte[]>? protectForTest = null)
    {
        NativeRdpSettings? settings = null;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            settings = new NativeRdpSettings(directory, profile, password, protectForTest);
            cancellation.ThrowIfCancellationRequested();
            await (startAndWait ?? StartAndWait)(executable.StartInfo(settings.Path), cancellation).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
            if (settings is not null)
            {
                settings.Dispose();
                // A short retry handles scanners/client handles; a crash is cleaned on next startup.
                for (var attempt = 0; attempt < 10 && File.Exists(settings.Path); attempt++)
                {
                    await Task.Delay(200).ConfigureAwait(false); NativeRdpSettings.TryDelete(settings.Path);
                }
                if (File.Exists(settings.Path)) throw new InvalidOperationException("The temporary encrypted RDP file could not be removed. SableLatch will retry cleanup when it exits or next starts.");
            }
        }
    }

    private static async Task StartAndWait(ProcessStartInfo start, CancellationToken cancellation)
    {
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Windows Remote Desktop could not start.");
        // mstsc offers no file-consumption acknowledgment. GUI initialization + a short grace
        // period is best-effort cleanup timing, NOT confirmation of authentication/file consumption.
        // A lock cancels this wait and removes the file, but never kills an established session.
        var deadline = Stopwatch.StartNew();
        while (!process.HasExited && deadline.Elapsed < TimeSpan.FromSeconds(15))
        {
            cancellation.ThrowIfCancellationRequested();
            bool idle;
            try { idle = await Task.Run(() => process.WaitForInputIdle(200), cancellation).ConfigureAwait(false); }
            catch (InvalidOperationException) { break; }
            if (idle)
            {
                await Task.Delay(1500, cancellation).ConfigureAwait(false);
                if (process.HasExited && process.ExitCode != 0) throw new InvalidOperationException("Windows Remote Desktop exited before completing startup.");
                return;
            }
            await Task.Delay(100, cancellation).ConfigureAwait(false);
        }
        if (!process.HasExited) throw new InvalidOperationException("Windows Remote Desktop did not finish starting. Its temporary connection file was removed; try again.");
        if (process.ExitCode != 0) throw new InvalidOperationException("Windows Remote Desktop exited before completing startup.");
    }
}
