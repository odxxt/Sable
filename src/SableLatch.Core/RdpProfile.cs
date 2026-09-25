using System.Security.Cryptography;

namespace SableLatch.Core;

// Public connection settings only. Never put passwords in profiles, approvals or arguments.
public sealed record RdpProfile(string Host, string Username, string Domain = "", int Port = 3389, int Width = 1280, int Height = 800, bool Clipboard = false)
{
    public void Validate()
    {
        if (Host.Length is < 1 or > 253 || Host[0] == '-' || Uri.CheckHostName(Host) == UriHostNameType.Unknown) throw new InvalidDataException("Enter a host name or IP address without a port or URL.");
        if (Username.Length is < 1 or > 256 || Username.Any(char.IsControl) || Username != Username.Trim()) throw new InvalidDataException("Enter a valid Windows username, DOMAIN\\user or user@domain.");
        if (Domain.Length > 253 || Domain.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c is '/' or '\\')) throw new InvalidDataException("Enter a domain name without spaces or slashes.");
        if (Domain.Length > 0 && (Username.Contains('\\') || Username.Contains('@'))) throw new InvalidDataException("Leave Domain empty when the username already includes its domain.");
        if (Port is < 1 or > 65535) throw new InvalidDataException("Use a port from 1 to 65535.");
        if (Width is < 640 or > 3840 || Height is < 480 or > 2160) throw new InvalidDataException("Use a desktop size between 640 × 480 and 3840 × 2160.");
    }
    public static RdpProfile From(VaultItem item)
    {
        int Number(string key, int fallback) => item.Field(key) is not { Length: > 0 } value ? fallback : int.TryParse(value, out var n) ? n : throw new InvalidDataException("Invalid RDP " + key + ".");
        var profile = new RdpProfile(item.Field("host"), item.Field("username"), item.Field("domain"), Number("port", 3389), Number("width", 1280), Number("height", 800), item.Field("clipboard") == "true");
        profile.Validate(); return profile;
    }
    public string Fingerprint(string moduleFingerprint, string helperHash, Guid entryId) => Convert.ToHexString(SHA256.HashData(Codec.Encode(new { Profile = this, moduleFingerprint, helperHash, entryId })));
}
