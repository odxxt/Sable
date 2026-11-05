using System.Net;
using System.Net.Sockets;

namespace SableLatch.Core;

// Bounded, purely local parsing. Clipboard text is never interpolated into user scripts.
public sealed record ShortcutTarget(string Host, int? Port, bool IsAddress)
{
    public string Display => Port is int p ? (Host.Contains(':') ? $"[{Host}]:{p}" : $"{Host}:{p}") : Host;
}
public static class ShortcutTargets
{
    public static bool IsDiagnostic(string kind) => kind is "ping" or "tcp" or "lookup";
    public static ShortcutTarget? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 512) return null;
        text = text.Trim();
        if (text.Any(char.IsControl)) return null;
        string host = text; int? port = null;
        if (text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0) return null;
            host = uri.IdnHost; // Do not retain URL paths, queries, fragments or credentials.
        }
        else if (text.StartsWith('['))
        {
            var end = text.IndexOf(']');
            if (end < 0 || end + 2 >= text.Length || text[end + 1] != ':' || !Port(text[(end + 2)..], out var p)) return null;
            host = text[1..end]; port = p;
        }
        else if (text.Count(c => c == ':') == 1)
        {
            var end = text.IndexOf(':'); if (!Port(text[(end + 1)..], out var p)) return null;
            host = text[..end]; port = p;
        }
        if (IPAddress.TryParse(host, out var address))
        {
            if (address.AddressFamily == AddressFamily.InterNetwork && (host.Split('.').Length != 4 || host.Any(c => !char.IsAsciiDigit(c) && c != '.'))) return null;
            if (host.Contains('%')) return null;
            return new(address.ToString(), port, true);
        }
        host = host.TrimEnd('.');
        if (host.Length is 0 or > 253 || host.All(c => char.IsAsciiDigit(c) || c == '.')) return null;
        if (host.Split('.').Any(s => s.Length is 0 or > 63 || !char.IsAsciiLetterOrDigit(s[0]) || !char.IsAsciiLetterOrDigit(s[^1]) || s.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))) return null;
        return new(host.ToLowerInvariant(), port, false);
    }
    private static bool Port(string s, out int p) => int.TryParse(s, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out p) && p is > 0 and <= 65535;
    public static bool Matches(string kind, ShortcutTarget target) => kind switch
    { "ping" => target.IsAddress && target.Port is null, "tcp" => target.Port is not null, "lookup" => !target.IsAddress && target.Port is null, _ => false };
    public static ShortcutTarget Require(string kind, string text)
    {
        var target = Parse(text) ?? throw new InvalidDataException("Enter a valid IP address or hostname; use host:port for a port test.");
        if (kind == "tcp" && target.Port is null) throw new InvalidDataException("Include a port, such as server.example:443.");
        if (kind != "tcp" && target.Port is not null) throw new InvalidDataException("Use a host without a port for this action.");
        return target;
    }
}
public sealed class ShortcutProvider : IContentProvider
{
    public ContentDescriptor Descriptor => new("sable.shortcut", 1, "Shortcut", "\uE768", false);
    public IReadOnlyList<string> CopyActions => [];
    public CopyValue Copy(VaultItem item, string action) => throw new InvalidOperationException("Shortcuts run actions; they do not copy a value.");
}
