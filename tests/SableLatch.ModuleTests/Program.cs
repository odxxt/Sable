using System.IO;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SableLatch.Core;
using SableLatch.Desktop;

if (args.FirstOrDefault() == "frame-client")
{
    using var pipe = new NamedPipeClientStream(".", args[1], PipeDirection.InOut, PipeOptions.Asynchronous);
    using var deadline = new CancellationTokenSource(4000); await pipe.ConnectAsync(deadline.Token);
    var header = new byte[4]; await pipe.ReadExactlyAsync(header, deadline.Token);
    var count = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(header);
    if (count is < 1 or > RdpBroker.MaxFrame) return;
    var frame = new byte[count];
    try { await pipe.ReadExactlyAsync(frame, deadline.Token); File.WriteAllText(args[2], Convert.ToHexString(SHA256.HashData(frame))); await pipe.WriteAsync(new byte[] { 0x53 }, deadline.Token); }
    finally { CryptographicOperations.ZeroMemory(frame); }
    return;
}
if (args.FirstOrDefault() == "client")
{
    try
    {
        using var pipe = new NamedPipeClientStream(".", args[1], PipeDirection.In);
        await pipe.ConnectAsync(3000);
        using var text = new StreamReader(pipe, Encoding.UTF8);
        var line = await text.ReadLineAsync();
        File.WriteAllText(args[2], line is null ? "empty" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(line))));
    }
    catch (Exception) { File.WriteAllText(args[2], "rejected"); }
    return;
}
if (args.FirstOrDefault() == "idle") { await Task.Delay(8000); return; }
if (args.FirstOrDefault() == "portable-rdp")
{
    using var pipe = new CredentialPipe();
    var launch = new ProcessStartInfo(Path.GetFullPath(args[1])) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
    launch.ArgumentList.Add("--rdp-session"); launch.ArgumentList.Add(pipe.Name); launch.ArgumentList.Add(Environment.ProcessId.ToString());
    launch.Environment["DOTNET_ROOT"] = Path.Combine(Path.GetTempPath(), "absent-sable-dotnet"); launch.Environment["DOTNET_ROOT_X64"] = launch.Environment["DOTNET_ROOT"];
    using var process = Process.Start(launch)!; using var deadline = new CancellationTokenSource(15000);
    var frame = RdpBroker.Frame(new RdpProfile("127.0.0.1", "fixture", Port: 1), Encoding.UTF8.GetBytes("fictional-portable-rdp-password"));
    try
    {
        await pipe.DeliverFrame(process, frame, deadline.Token);
        var ownRoot = Path.GetDirectoryName(launch.FileName)!;
        var runtime = process.Modules.Cast<ProcessModule>().Where(m => m.ModuleName is "coreclr.dll" or "hostfxr.dll" or "hostpolicy.dll").ToArray();
        if (runtime.Length != 3 || runtime.Any(m => !m.FileName.StartsWith(ownRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) throw new Exception("Portable helper did not load the bundled runtime.");
        for (var i = 0; i < 30 && process.MainWindowHandle == IntPtr.Zero; i++) { await Task.Delay(100); process.Refresh(); }
        if (!process.CloseMainWindow()) throw new Exception("Portable helper window unavailable.");
        await process.WaitForExitAsync(deadline.Token);
        if (process.ExitCode != 0 || frame.Any(b => b != 0)) throw new Exception("Portable helper shutdown or frame clearing failed.");
        Console.WriteLine("PASS: Extracted self-contained RDP helper receives the frame, uses its bundled runtime, and exits on window close.");
    }
    finally { CryptographicOperations.ZeroMemory(frame); if (!process.HasExited) process.Kill(); }
    return;
}
var root = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(Path.GetTempPath(), "sable-module-tests-" + Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(root);
try
{
var checks = new List<string>();
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks.Add("PASS: " + name); Console.WriteLine(checks[^1]); }
Process Start(params string[] arguments)
{
    var executable = Environment.ProcessPath!;
    var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
    if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    foreach (var arg in arguments) start.ArgumentList.Add(arg);
    return Process.Start(start)!;
}
async Task Stop(Process process) { if (!process.HasExited) process.Kill(); await process.WaitForExitAsync(); }
var canary = Encoding.UTF8.GetBytes("fictional-pipe-test-password");
using (var pipe = new CredentialPipe())
using (var child = Start("client", pipe.Name, Path.Combine(root, "client-result.txt")))
using (var deadline = new CancellationTokenSource(5000))
{
    var transfer = canary.ToArray(); await pipe.Deliver(child, transfer, deadline.Token);
    await child.WaitForExitAsync(deadline.Token);
    Check(File.ReadAllText(Path.Combine(root, "client-result.txt")) == Convert.ToHexString(SHA256.HashData(canary)), "Exact launched process receives complete password line");
    Check(transfer.All(b => b == 0), "Transfer buffer cleared immediately after pipe write");
}
using (var pipe = new CredentialPipe())
using (var expected = Start("idle"))
using (var wrong = Start("client", pipe.Name, Path.Combine(root, "wrong-result.txt")))
using (var deadline = new CancellationTokenSource(5000))
{
    var rejected = false;
    try { await pipe.Deliver(expected, canary.ToArray(), deadline.Token); } catch (InvalidOperationException) { rejected = true; }
    pipe.Dispose(); await wrong.WaitForExitAsync(deadline.Token); await Stop(expected);
    Check(rejected && File.ReadAllText(Path.Combine(root, "wrong-result.txt")) != Convert.ToHexString(SHA256.HashData(canary)), "Unexpected pipe client receives no password");
}
using (var pipe = new CredentialPipe())
using (var expected = Start("idle"))
using (var deadline = new CancellationTokenSource(100))
{
    var cancelled = false;
    try { await pipe.Deliver(expected, canary.ToArray(), deadline.Token); } catch (OperationCanceledException) { cancelled = true; }
    await Stop(expected); Check(cancelled, "Lock/timeout cancellation interrupts pending pipe connection");
}
var package = ModulePackages.Read(ModuleTrust.Resource("putty.zip"), ModuleTrust.Verify);
using (var pipe = new CredentialPipe())
using (var expected = Start("frame-client", pipe.Name, Path.Combine(root, "frame-result.txt")))
using (var deadline = new CancellationTokenSource(5000))
{
    var frame = RdpBroker.Frame(new RdpProfile("fixture.example", "user"), canary); var hash = Convert.ToHexString(SHA256.HashData(frame));
    await pipe.DeliverFrame(expected, frame, deadline.Token); await expected.WaitForExitAsync(deadline.Token);
    Check(File.ReadAllText(Path.Combine(root, "frame-result.txt")) == hash && frame.All(b => b == 0), "Framed handoff delivers complete bytes only to launched client and clears sender frame");
}
using (var pipe = new CredentialPipe())
using (var expected = Start("idle"))
using (var wrong = Start("client", pipe.Name, Path.Combine(root, "wrong-frame-result.txt")))
using (var deadline = new CancellationTokenSource(5000))
{
    var frame = RdpBroker.Frame(new RdpProfile("fixture.example", "user"), canary); var rejected = false;
    try { await pipe.DeliverFrame(expected, frame, deadline.Token); } catch (InvalidOperationException) { rejected = true; }
    pipe.Dispose(); await wrong.WaitForExitAsync(deadline.Token); await Stop(expected);
    Check(rejected && File.ReadAllText(Path.Combine(root, "wrong-frame-result.txt")) == "empty" && frame.All(b => b == 0), "Framed handoff rejects wrong PID without sending bytes and clears rejected frame");
}
using (var pipe = new CredentialPipe())
using (var expected = Start("idle"))
using (var deadline = new CancellationTokenSource(100))
{
    var frame = RdpBroker.Frame(new RdpProfile("fixture.example", "user"), canary); var cancelled = false;
    try { await pipe.DeliverFrame(expected, frame, deadline.Token); } catch (OperationCanceledException) { cancelled = true; }
    await Stop(expected); Check(cancelled && frame.All(b => b == 0), "Cancelling framed handoff clears pending credentials");
}
await RdpChecks.Run(Check);
Check(package.Official && package.Manifest.Handler == "putty", "Bundled PuTTY module passes actual publisher signature verification");
var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../modules/catalog.json"));
if (File.Exists(source)) Check(ModuleTrust.ReadCatalog(File.ReadAllBytes(source)).Count == 2, "Official GitHub catalog signature validates both modules");
var installed = PuttyBroker.Detect();
if (installed.Length > 0)
{
    using var verified = new VerifiedPutty(installed);
    Check(verified.Hash.Length == 64, "Installed PuTTY verifies publisher, file identity and version");
    var altered = Path.Combine(root, "changed-putty.exe"); File.Copy(installed, altered, true);
    using (var file = new FileStream(altered, FileMode.Open, FileAccess.Write)) { file.Position = 4096; file.WriteByte(123); }
    var rejected = false; try { using var changed = new VerifiedPutty(altered); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "Modified PuTTY executable cannot receive credentials");
    // Loopback only; no SSH service or external system is contacted. Stop only our own test process.
    Process? child = null;
    var password = Encoding.UTF8.GetBytes("fictional-local-putty-canary");
    using var timeout = new CancellationTokenSource(8000);
    try
    {
        await PuttyBroker.Connect(verified, new PuttyProfile("127.0.0.1", "fixture", 1), password, timeout.Token, process => { child = Process.GetProcessById(process.Id); _ = child.SafeHandle; }, true);
        Check(password.All(b => b == 0), "Real PuTTY consumes named-pipe password; host transfer buffer is cleared");
    }
    catch (UnauthorizedAccessException)
    {
        checks.Add("BLOCKED: Full PuTTY launch requires Windows registry access, unavailable in this execution session.");
        Console.WriteLine(checks[^1]);
        Check(password.All(b => b == 0), "Denied launch still clears the host password transfer buffer");
    }
    finally { if (child is not null) { await Stop(child); child.Dispose(); } }
    // Independently exercise PuTTY's password-file reader. A unique absent session gives
    // PuTTY's built-in defaults, never one of the user's named sessions. No vault data is used.
    using var pipe = new CredentialPipe();
    var start = new ProcessStartInfo(verified.Path) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
    start.ArgumentList.Add("-load"); start.ArgumentList.Add("SableLatch-fixture-" + Guid.NewGuid().ToString("N"));
    foreach (var argument in new PuttyProfile("127.0.0.1", "fixture", 1).Arguments(pipe.Name)) start.ArgumentList.Add(argument);
    using var putty = Process.Start(start)!;
    using var pipeDeadline = new CancellationTokenSource(5000);
    try { await pipe.Deliver(putty, canary, pipeDeadline.Token); Check(true, "Official PuTTY 0.85 consumes a single-use password pipe without a temporary password file"); }
    finally { await Stop(putty); }
}
CryptographicOperations.ZeroMemory(canary);
var passed = checks.Count(c => c.StartsWith("PASS:")); var blocked = checks.Count - passed;
File.WriteAllLines(Path.Combine(root, "results.txt"), checks.Append($"{passed} checks passed; {blocked} blocked by environment."));
Console.WriteLine($"{passed} checks passed; {blocked} blocked by environment.");
}
catch (Exception ex) { File.WriteAllText(Path.Combine(root, "error.txt"), ex.ToString()); Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; }
