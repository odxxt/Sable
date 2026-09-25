using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;

namespace SableLatch.Desktop;

// Reuses the running application as an isolated, single-session helper. No module-supplied executable.
internal sealed class RdpExecutable : IDisposable
{
    private readonly List<FileStream> files = [];
    public string Path { get; }
    public string Hash { get; }
    private readonly string assembly = typeof(App).Assembly.Location;
    public RdpExecutable()
    {
        Path = Environment.ProcessPath ?? throw new InvalidOperationException("Application path unavailable.");
        try
        {
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var path in new[] { Path, assembly }.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); files.Add(file);
                digest.AppendData(SHA256.HashData(file));
            }
            Hash = Convert.ToHexString(digest.GetHashAndReset());
        }
        catch { Dispose(); throw; }
    }
    public ProcessStartInfo StartInfo(string pipe)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(pipe, "^SableLatch-[A-F0-9]{64}$")) throw new InvalidDataException("Invalid session channel.");
        var info = new ProcessStartInfo(Path) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
        if (System.IO.Path.GetFileNameWithoutExtension(Path).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(assembly);
        info.ArgumentList.Add("--rdp-session"); info.ArgumentList.Add(pipe); info.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        // The user explicitly opens this interactive session window; unlike a background helper it is visible.
        return info;
    }
    public void Dispose() { foreach (var file in files) file.Dispose(); }
}

internal static class RdpBroker
{
    public const int MaxFrame = 16384;
    public static byte[] Frame(RdpProfile profile, byte[] password)
    {
        profile.Validate();
        if (password.Length is < 1 or > 4096 || password.Contains((byte)0)) throw new InvalidDataException("Enter a password of up to 4,096 UTF-8 bytes without null characters.");
        var settings = Codec.Encode(profile);
        var bytes = new byte[8 + settings.Length + password.Length];
        if (bytes.Length > MaxFrame) throw new InvalidDataException("RDP connection settings are too large.");
        BinaryPrimitives.WriteInt32LittleEndian(bytes, settings.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), password.Length);
        settings.CopyTo(bytes, 8); password.CopyTo(bytes, 8 + settings.Length); return bytes;
    }
    public static (RdpProfile Profile, int PasswordOffset, int PasswordLength) Parse(byte[] bytes)
    {
        if (bytes.Length is < 10 or > MaxFrame) throw new InvalidDataException("Invalid RDP session message.");
        var publicLength = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        var secretLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4));
        if (publicLength < 2 || secretLength is < 1 or > 4096 || (long)publicLength + secretLength + 8 != bytes.Length) throw new InvalidDataException("Invalid RDP session lengths.");
        var profile = Codec.Decode<RdpProfile>(bytes.AsSpan(8, publicLength).ToArray()); profile.Validate();
        return (profile, publicLength + 8, secretLength);
    }
    public static async Task Connect(RdpExecutable executable, RdpProfile profile, byte[] password, CancellationToken cancellation, Action<Process>? started = null, bool hidden = false)
    {
        Process? child = null; byte[]? frame = null;
        try
        {
            frame = Frame(profile, password);
            using var channel = new CredentialPipe();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(20));
            cancellation.ThrowIfCancellationRequested();
            var start = executable.StartInfo(channel.Name); if (hidden) start.WindowStyle = ProcessWindowStyle.Hidden;
            child = Process.Start(start) ?? throw new InvalidOperationException("The RDP window could not start.");
            started?.Invoke(child);
            await channel.DeliverFrame(child, frame, timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            if (child is not null) try { if (!child.HasExited) child.Kill(); } catch (InvalidOperationException) { } catch (Win32Exception) { }
            throw;
        }
        finally { if (frame is not null) CryptographicOperations.ZeroMemory(frame); CryptographicOperations.ZeroMemory(password); child?.Dispose(); }
    }
}
