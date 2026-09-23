using System.Security.Cryptography;
using System.Text.Json;

namespace SableLatch.Core;

// Machine-local approvals never travel with vault exports/backups.
public sealed class ModuleApprovals(string directory, IDeviceProtector protector)
{
    private string FilePath => Path.Combine(directory, "modules.sableconsent");
    private sealed record Consent(string Purpose, string Machine, List<string> Digests);
    private List<string> Read()
    {
        try
        {
            if (!File.Exists(FilePath) || new FileInfo(FilePath).Length > 300_000) return [];
            var plain = protector.Unprotect(File.ReadAllBytes(FilePath));
            try
            {
                var data = Codec.Decode<Consent>(plain);
                return data.Purpose == "module-consent-v1" && data.Machine == Environment.MachineName && data.Digests.Count <= 2048 && data.Digests.All(d => d.Length == 64 && d.All(Uri.IsHexDigit)) ? data.Digests : [];
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException or NullReferenceException or System.ComponentModel.Win32Exception) { return []; }
    }
    public bool Contains(string digest) => Read().Contains(digest);
    public void Remember(string digest)
    {
        if (digest.Length != 64 || !digest.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid approval fingerprint.");
        var values = Read(); values.Remove(digest);
        var bytes = Codec.Encode(new Consent("module-consent-v1", Environment.MachineName, values.TakeLast(2047).Append(digest).ToList()));
        try { VaultStore.AtomicWrite(FilePath, protector.Protect(bytes)); } finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public void Clear() { if (File.Exists(FilePath)) File.Delete(FilePath); }
}
