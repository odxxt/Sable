using System.Globalization;
using System.Net;

namespace SableLatch.Core;

public sealed record ClipboardDiagnostic(string Kind, ShortcutTarget Target)
{
    public string Label => Kind switch { "tcp" => "Test port", "lookup" => "DNS lookup", _ => "Ping" };
}

public static class ClipboardDiagnostics
{
    public const int MaxCharacters = 512;
    private sealed record AddressMatch(int Index, int Length, string Value);
    // At most 512 characters, one forward pass: no regex engine or backtracking work.
    private static IEnumerable<AddressMatch> Addresses(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsAsciiDigit(text[i])) continue;
            var start = i; while (i + 1 < text.Length && (char.IsAsciiDigit(text[i + 1]) || text[i + 1] == '.')) i++;
            var value = text[start..(i + 1)].TrimEnd('.');
            if (value.Count(c => c == '.') >= 3) yield return new(start, value.Length, value);
        }
    }
    private static bool PortWord(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
    private static int Spaces(string text, int cursor) { while (cursor < text.Length && char.IsWhiteSpace(text[cursor])) cursor++; return cursor; }
    private static bool Token(string text, int cursor, string value) => cursor + value.Length <= text.Length && text.AsSpan(cursor, value.Length).Equals(value, StringComparison.OrdinalIgnoreCase) && (cursor + value.Length == text.Length || !PortWord(text[cursor + value.Length]));
    private static HashSet<string> NamedPortValues(string text)
    {
        var values = new HashSet<string>();
        for (var i = 0; i + 4 <= text.Length; i++)
        {
            if (i > 0 && PortWord(text[i - 1]) || !Token(text, i, "port")) continue;
            var cursor = Spaces(text, i + 4);
            foreach (var word in new[] { "is", "number", "no" }) if (Token(text, cursor, word)) { cursor = Spaces(text, cursor + word.Length); if (word == "no" && cursor < text.Length && text[cursor] == '.') cursor = Spaces(text, cursor + 1); break; }
            if (cursor < text.Length && text[cursor] is ':' or '=' or '#') cursor = Spaces(text, cursor + 1);
            var start = cursor; while (cursor < text.Length && char.IsAsciiDigit(text[cursor])) cursor++;
            if (cursor > start && (cursor == text.Length || !PortWord(text[cursor]))) values.Add(text[start..cursor]);
        }
        return values;
    }
    private static bool Word(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '.';
    private static bool TryPort(string text, out int value) { value = 0; return text.Length is > 0 and <= 5 && text[0] != '0' && text.All(char.IsAsciiDigit) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= 65535; }
    public static IReadOnlyList<ClipboardDiagnostic> Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxCharacters || text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))) return [];
        var found = new List<(ShortcutTarget Target, bool Explicit)>();
        var hasAddressCandidates = false;
        foreach (var match in Addresses(text))
        {
            hasAddressCandidates = true;
            if (match.Index > 0 && (Word(text[match.Index - 1]) || text[match.Index - 1] == ':') || match.Index + match.Length < text.Length && PortWord(text[match.Index + match.Length])) continue;
            var parts = match.Value.Split('.');
            if (parts.Any(p => p.Length > 1 && p[0] == '0') || !IPAddress.TryParse(match.Value, out var address)) continue;
            var end = match.Index + match.Length; var cursor = end;
            while (cursor < text.Length && text[cursor] is ' ' or '\t') cursor++;
            int? port = null; var explicitPort = cursor < text.Length && text[cursor] == ':';
            if (explicitPort)
            {
                cursor++; while (cursor < text.Length && text[cursor] is ' ' or '\t') cursor++;
                var start = cursor; while (cursor < text.Length && char.IsAsciiDigit(text[cursor])) cursor++;
                if ((cursor == text.Length || !PortWord(text[cursor])) && TryPort(text[start..cursor], out var number)) port = number;
            }
            found.Add((new(address.ToString(), port, true), explicitPort));
        }
        // A separate port needs exactly one distinct address and one unambiguous labelled value.
        if (found.Select(p => p.Target.Host).Distinct().Count() == 1 && found.All(p => !p.Explicit))
        {
            var ports = NamedPortValues(text);
            if (ports.Count == 1 && TryPort(ports.First(), out var port)) found = found.Select(p => (p.Target with { Port = port }, p.Explicit)).ToList();
        }
        if (found.Count > 0) return found.Select(p => new ClipboardDiagnostic(p.Target.Port is null ? "ping" : "tcp", p.Target)).Distinct().Take(3).ToList();
        // Retain whole-target IPv6 and hostname/URL support; do not guess hostnames in prose.
        var whole = ShortcutTargets.Parse(text);
        if (hasAddressCandidates && (whole is null || !whole.IsAddress || !whole.Host.Contains(':'))) return [];
        if (whole is null || whole.Port is int p && !ValidWholePort(text, p)) return [];
        if (!whole.IsAddress && whole.Port is null && !whole.Host.Contains('.')) return [];
        return [new(whole.Port is not null ? "tcp" : whole.IsAddress ? "ping" : "lookup", whole)];
    }
    private static bool ValidWholePort(string text, int port)
    {
        var tail = text.Trim()[(text.Trim().LastIndexOf(':') + 1)..];
        return TryPort(tail, out var parsed) && parsed == port;
    }
}
