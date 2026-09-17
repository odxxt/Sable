using System.Security.Cryptography;
using System.Text.Json;

namespace SableLatch.Core;

// Local execution consent is never exported or put in vault backups.
// Only device-protected digests are stored, not commands, labels or credentials.
public sealed class ActionApprovals(string directory, IDeviceProtector protector)
{
    private const int Capacity = 2048;
    public string FilePath { get; } = Path.Combine(Path.GetFullPath(directory), "actions.sableconsent");
    private sealed record Consent(string Purpose, string Machine, List<string> Digests);
    public static string Fingerprint(EntryAction action)
    {
        // Keep existing approvals valid when the new option is off; opting in changes consent.
        var bytes = action.KeepTerminalOpen
            ? Codec.Encode(new { action.Kind, action.Target, action.Arguments, action.WorkingDirectory, action.CopyOnRun, action.KeepTerminalOpen })
            : Codec.Encode(new { action.Kind, action.Target, action.Arguments, action.WorkingDirectory, action.CopyOnRun });
        try { return Convert.ToHexString(SHA256.HashData(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private static string Key(Guid item, EntryAction action) => item.ToString("N") + Fingerprint(action);
    public bool Contains(Guid item, EntryAction action) => Read().Contains(Key(item, action));
    private List<string> Read()
    {
        try
        {
            if (!File.Exists(FilePath) || new FileInfo(FilePath).Length > 300_000) return [];
            var plain = protector.Unprotect(File.ReadAllBytes(FilePath));
            try
            {
                var value = Codec.Decode<Consent>(plain);
                return value is not null && value.Purpose == "sable-action-consent-v1" && value.Machine == Environment.MachineName &&
                    value.Digests is { Count: <= Capacity } && value.Digests.All(x => x is { Length: 96 } && x.All(Uri.IsHexDigit)) ? value.Digests : [];
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException or InvalidDataException or System.ComponentModel.Win32Exception) { return []; }
    }
    public void Remember(Guid item, EntryAction action)
    {
        var key = Key(item, action); var values = Read(); values.Remove(key);
        var plain = Codec.Encode(new Consent("sable-action-consent-v1", Environment.MachineName, values.TakeLast(Capacity - 1).Append(key).ToList()));
        try { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); VaultStore.AtomicWrite(FilePath, protector.Protect(plain)); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
}
