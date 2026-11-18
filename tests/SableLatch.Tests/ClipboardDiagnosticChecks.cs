using SableLatch.Core;

internal static class ClipboardDiagnosticChecks
{
    public static void Run(Action<bool, string> check)
    {
        void Target(string text, string kind, string display) { var result = ClipboardDiagnostics.Extract(text); check(result.Count == 1 && result[0].Kind == kind && result[0].Target.Display == display, "Clipboard diagnostic: " + text); }
        Target("the ip is 192.168.8.1", "ping", "192.168.8.1");
        Target("the ip and port is 192.168.8.1:53", "tcp", "192.168.8.1:53");
        Target("the ip is 192.168.8.1 and the port is 53", "tcp", "192.168.8.1:53");
        Target("PORT: 8181\nserver is 192.168.8.1", "tcp", "192.168.8.1:8181");
        Target("the ip is 192.168.8.1 and the port is 0212", "ping", "192.168.8.1");
        Target("192.168.8.1:0212", "ping", "192.168.8.1");
        foreach (var port in new[] { "0", "65536", "999999", "00053", "53abc" }) Target("the ip is 192.168.8.1 and port " + port, "ping", "192.168.8.1");
        Target("192.168.8.1 has ticket 8181", "ping", "192.168.8.1");
        Target("192.168.8.1 port 65535", "tcp", "192.168.8.1:65535");
        Target("192.168.8.1 port 1", "tcp", "192.168.8.1:1");
        Target("192.168.8.1 port 53 or port 80", "ping", "192.168.8.1");
        Target("192.168.8.1; Remove-Item C:\\", "ping", "192.168.8.1");
        check(ClipboardDiagnostics.Extract("192.168.8.1 and 192.168.8.2, port 53").All(d => d.Kind == "ping"), "Separate port is not guessed for multiple addresses");
        check(ClipboardDiagnostics.Extract("192.168.8.1 then 192.168.8.1").Count == 1, "Duplicate diagnostic targets are collapsed");
        check(ClipboardDiagnostics.Extract(string.Join(" ", Enumerable.Range(1, 6).Select(i => "192.168.8." + i))).Count == 3, "Built-in suggestions have a fixed maximum of three");
        foreach (var invalid in new[] { "999.168.8.1", "192.168.08.1", "1.2.3.4444", "v192.168.8.1suffix" }) check(ClipboardDiagnostics.Extract(invalid).Count == 0, "Malformed address not extracted: " + invalid);
        check(ClipboardDiagnostics.Extract("the ip is 192.168.8.1" + new string(' ', 512)).Count == 0, "Oversized clipboard text is skipped even with a valid address at the beginning");
        check(ClipboardDiagnostics.Extract(new string('9', 512)).Count == 0, "Bounded pathological numeric input does not match");
        Target("[2001:db8::1]:443", "tcp", "[2001:db8::1]:443");
        Target("server.example:443", "tcp", "server.example:443");
        Target("https://example.com/page", "lookup", "example.com");
        foreach (var word in new[] { "book", "Book", "book.", "localhost" }) check(ClipboardDiagnostics.Extract(word).Count == 0, "Ordinary single words do not suggest DNS: " + word);
        Target("alodat.me", "lookup", "alodat.me");
        check(ClipboardDiagnostics.Extract("host.example:0212").Count == 0, "Hostname port rejects leading zero notation too");
        check(!Codec.Decode<Preferences>(System.Text.Encoding.UTF8.GetBytes("{}")).ClipboardDismissHintSeen, "Older vault preferences show the one-time dismissal choice initially");
        check(Codec.Clone(new Preferences { ClipboardDismissHintSeen = true }).ClipboardDismissHintSeen, "One-time dismissal preference survives document serialization");
    }
}
