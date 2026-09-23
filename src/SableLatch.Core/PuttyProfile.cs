using System.Net;
using System.Security.Cryptography;

namespace SableLatch.Core;

// No password in this object: it is safe to use for connection review and fingerprints.
public sealed record PuttyProfile(string Host, string Username, int Port = 22, string HostKey = "", string Session = "", bool Compression = false, string AddressFamily = "auto", int KeepaliveSeconds = 30, string Font = "Consolas", int FontSize = 11)
{
    public void Validate()
    {
        if (Host.Length is < 1 or > 253 || Host[0] == '-' || (Uri.CheckHostName(Host) == UriHostNameType.Unknown && !IPAddress.TryParse(Host, out _))) throw new InvalidDataException("Enter a valid SSH host name or IP address.");
        if (Username.Length is < 1 or > 128 || Username.Any(c => char.IsControl(c) || char.IsWhiteSpace(c))) throw new InvalidDataException("Enter a username without spaces or control characters.");
        if (Port is < 1 or > 65535) throw new InvalidDataException("Use a port from 1 to 65535.");
        if (HostKey.Length > 300 || HostKey.Any(c => char.IsControl(c) || c == ';')) throw new InvalidDataException("Use one expected server fingerprint.");
        if (Session.Length > 200 || Session.Any(char.IsControl)) throw new InvalidDataException("Invalid saved session name.");
        if (AddressFamily is not ("auto" or "ipv4" or "ipv6")) throw new InvalidDataException("Invalid address family.");
        if (KeepaliveSeconds is < 0 or > 600 || FontSize is < 8 or > 24 || Font is not ("Consolas" or "Cascadia Mono" or "Courier New")) throw new InvalidDataException("Invalid keepalive or terminal appearance setting.");
    }
    public static PuttyProfile From(VaultItem item)
    {
        var profile = new PuttyProfile(item.Field("host"), item.Field("username"), int.TryParse(item.Field("port"), out var port) ? port : 22, item.Field("hostKey"), item.Field("session"), item.Field("compression") == "true", item.Field("addressFamily") is { Length: > 0 } family ? family : "auto", int.TryParse(item.Field("keepalive"), out var keepalive) ? keepalive : 30, item.Field("font") is { Length: > 0 } font ? font : "Consolas", int.TryParse(item.Field("fontSize"), out var size) ? size : 11);
        profile.Validate(); return profile;
    }
    public string Fingerprint(string moduleFingerprint, string executableHash, Guid entryId) => Convert.ToHexString(SHA256.HashData(Codec.Encode(new { Profile = this, moduleFingerprint, executableHash, entryId })));
    public IReadOnlyList<string> Arguments(string pipeName)
    {
        Validate();
        if (!System.Text.RegularExpressions.Regex.IsMatch(pipeName, "^SableLatch-[A-F0-9]{64}$")) throw new InvalidDataException("Invalid credential pipe.");
        List<string> args = [];
        if (Session.Length > 0) { args.Add("-load"); args.Add(Session); }
        args.AddRange(["-ssh", "-2", "-noagent", "-a", "-x", "-restrict-acl", "-l", Username, "-P", Port.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        if (HostKey.Length > 0) args.AddRange(["-hostkey", HostKey]);
        if (Compression) args.Add("-C");
        if (AddressFamily != "auto") args.Add(AddressFamily == "ipv4" ? "-4" : "-6");
        args.AddRange(["-pwfile", @"\\.\pipe\" + pipeName, Host]); return args;
    }
}
