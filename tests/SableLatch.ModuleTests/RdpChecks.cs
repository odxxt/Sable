using System.IO;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using SableLatch.Core;
using SableLatch.Desktop;

internal static class RdpChecks
{
    public static async Task Run(Action<bool, string> check, Action<string> blocked)
    {
        var package = ModulePackages.Read(ModuleTrust.Resource("rdp.zip"), ModuleTrust.Verify);
        check(package.Official && package.Manifest.Handler == "rdp", "Bundled RDP module retains its valid publisher signature");
        var directory = Path.Combine(Path.GetTempPath(), "sable-native-rdp-test-" + Guid.NewGuid().ToString("N"));
        var profile = new RdpProfile("fixture.example", "DOMAIN\\fixture");
        const string canary = "fictional-秘密-password";
        byte[] Secret() => Encoding.UTF8.GetBytes(canary);
        // Deterministic protector exercises file lifecycle without claiming DPAPI succeeds in a sandbox.
        byte[] FixtureProtect(byte[] unicode)
        {
            check(Encoding.Unicode.GetString(unicode) == canary, "RDP protection receives exact Unicode password bytes");
            return SHA256.HashData(unicode);
        }
        try
        {
            using var executable = new NativeRdpExecutable();
            var info = executable.StartInfo(Path.Combine(directory, "fixture.rdp"));
            check(info.FileName == Path.Combine(Environment.SystemDirectory, "mstsc.exe") && !info.UseShellExecute && info.ArgumentList.Count == 1 && info.Arguments.Length == 0, "Native RDP launch targets system mstsc with only the file path argument");
            var transfer = Secret(); string file;
            using (var settings = new NativeRdpSettings(directory, profile, transfer, FixtureProtect))
            {
                file = settings.Path; var text = File.ReadAllText(file, Encoding.Unicode);
                check(transfer.All(b => b == 0) && !text.Contains(canary) && text.Contains("password 51:b:") && text.Contains("enablecredsspsupport:i:1"), "RDP file contains only the protected blob and clears the transfer buffer");
                var acl = new DirectoryInfo(directory).GetAccessControl();
                var sid = WindowsIdentity.GetCurrent().User!;
                check(acl.AreAccessRulesProtected && acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().All(r => r.IdentityReference.Equals(sid)), "Launch directory disables inherited access and grants only the current user");
                var noWrite = false; try { using var write = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); } catch (IOException) { noWrite = true; }
                check(noWrite, "Prepared file cannot be replaced or written while handed to the client");
            }
            check(!File.Exists(file), "Disposal removes the temporary launch file");
            using (var pending = new NativeRdpSettings(directory, profile, Secret(), FixtureProtect))
            {
                NativeRdpSettings.ReleaseActive();
                check(!File.Exists(pending.Path), "Application exit releases and deletes files still awaiting client startup");
            }
            transfer = Secret(); var launched = false;
            await NativeRdpLauncher.Connect(executable, directory, profile, transfer, CancellationToken.None, (start, token) =>
            {
                launched = true; check(File.Exists(start.ArgumentList.Single()), "Launch file exists until simulated client startup finishes"); return Task.CompletedTask;
            }, FixtureProtect);
            check(launched && transfer.All(b => b == 0) && !Directory.EnumerateFiles(directory).Any(), "Successful launch cleanup leaves no RDP file or password buffer");
            foreach (var cancel in new[] { false, true })
            {
                transfer = Secret(); var rejected = false;
                try
                {
                    await NativeRdpLauncher.Connect(executable, directory, profile, transfer, CancellationToken.None,
                        (_, _) => cancel ? Task.FromCanceled(new CancellationToken(true)) : Task.FromException(new InvalidOperationException("Fixture start failed")), FixtureProtect);
                }
                catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException) { rejected = true; }
                check(rejected && transfer.All(b => b == 0) && !Directory.EnumerateFiles(directory).Any(), cancel ? "Cancelled launch removes the file and clears the secret" : "Failed process launch removes the file and clears the secret");
            }
            transfer = Secret(); var preCancelled = false;
            try { await NativeRdpLauncher.Connect(executable, directory, profile, transfer, new CancellationToken(true)); }
            catch (OperationCanceledException) { preCancelled = true; }
            check(preCancelled && transfer.All(b => b == 0) && !Directory.EnumerateFiles(directory).Any(), "Already locked/cancelled launch creates no file and clears password bytes");
            transfer = Secret(); var protectionFailed = false;
            try { using var settings = new NativeRdpSettings(directory, profile, transfer, _ => throw new CryptographicException("Fixture protection failure")); }
            catch (CryptographicException) { protectionFailed = true; }
            check(protectionFailed && transfer.All(b => b == 0) && !Directory.EnumerateFiles(directory).Any(), "Protection failure is closed: no plaintext fallback or residual secret");
            var stale = Path.Combine(directory, "rdp-" + Guid.NewGuid().ToString("N") + ".rdp");
            var unrelated = Path.Combine(directory, "keep.rdp"); File.WriteAllText(stale, "fixture"); File.WriteAllText(unrelated, "fixture");
            NativeRdpSettings.Cleanup(directory);
            check(!File.Exists(stale) && File.Exists(unrelated), "Startup cleanup removes only host-named launch files"); File.Delete(unrelated);
            byte[]? opened = null;
            try
            {
                using var real = new NativeRdpSettings(directory, profile, Secret());
                var line = File.ReadAllLines(real.Path, Encoding.Unicode).Single(l => l.StartsWith("password 51:b:", StringComparison.Ordinal));
                opened = ProtectedData.Unprotect(Convert.FromHexString(line[14..]), null, DataProtectionScope.CurrentUser);
                check(Encoding.Unicode.GetString(opened) == canary, "Actual CurrentUser DPAPI round-trip preserves the RDP password");
            }
            catch (CryptographicException)
            {
                blocked("Actual CurrentUser DPAPI is unavailable in this execution account; real credential-file login must be tested in a normal Windows session.");
                check(!Directory.EnumerateFiles(directory).Any(), "Unavailable DPAPI leaves no launch file");
            }
            finally { if (opened is not null) CryptographicOperations.ZeroMemory(opened); }
            check(!Directory.EnumerateFiles(directory).Any(), "RDP checks leave no temporary credential files");
        }
        finally { if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory); }
    }
}
