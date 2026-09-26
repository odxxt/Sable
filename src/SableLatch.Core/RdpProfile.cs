using System.Security.Cryptography;

namespace SableLatch.Core;

// Public connection settings only. Never put passwords in profiles, approvals or arguments.
public sealed record RdpProfile(string Host, string Username, string Domain = "", int Port = 3389, bool Clipboard = false, bool CopyOnLaunch = false, bool FullScreen = true)
{
    public void Validate()
    {
        if (Host.Length is < 1 or > 253 || Host[0] == '-' || Uri.CheckHostName(Host) == UriHostNameType.Unknown) throw new InvalidDataException("Enter a host name or IP address without a port or URL.");
        if (Username.Length is < 1 or > 256 || Username.Any(char.IsControl) || Username != Username.Trim()) throw new InvalidDataException("Enter a valid Windows username, DOMAIN\\user or user@domain.");
        if (Domain.Length > 253 || Domain.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c is '/' or '\\')) throw new InvalidDataException("Enter a domain name without spaces or slashes.");
        if (Domain.Length > 0 && (Username.Contains('\\') || Username.Contains('@'))) throw new InvalidDataException("Leave Domain empty when the username already includes its domain.");
        if (Port is < 1 or > 65535) throw new InvalidDataException("Use a port from 1 to 65535.");
    }
    public static RdpProfile From(VaultItem item)
    {
        int Number(string key, int fallback) => item.Field(key) is not { Length: > 0 } value ? fallback : int.TryParse(value, out var n) ? n : throw new InvalidDataException("Invalid RDP " + key + ".");
        // Legacy width/height fields are deliberately ignored: mstsc chooses the display size.
        var profile = new RdpProfile(item.Field("host"), item.Field("username"), item.Field("domain"), Number("port", 3389), item.Field("clipboard") == "true", item.Field("copyOnLaunch") == "true", item.Field("fullScreen") != "false");
        profile.Validate(); return profile;
    }
    public string Fingerprint(string moduleFingerprint, string helperHash, Guid entryId) => Convert.ToHexString(SHA256.HashData(Codec.Encode(new { Profile = this, moduleFingerprint, helperHash, entryId })));
}
