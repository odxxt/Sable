using SableLatch.Core;

internal static class ShortcutChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        check(ShortcutTargets.Parse("192.168.1.20") is { Host: "192.168.1.20", IsAddress: true, Port: null }, "Clipboard parses a complete IPv4 address");
        check(ShortcutTargets.Parse("10.0.0.1:443") is { Port: 443 }, "Clipboard parses an IP and port");
        check(ShortcutTargets.Parse("[2001:db8::1]:22") is { Host: "2001:db8::1", Port: 22 }, "Bracketed IPv6 port parses safely");
        check(ShortcutTargets.Parse("2001:db8::1") is { IsAddress: true, Port: null }, "Bare IPv6 stays an address rather than a port pair");
        check(ShortcutTargets.Parse("https://Example.com/private?q=secret#part") is { Host: "example.com", Port: null }, "URL suggestions keep only the host");
        foreach (var input in new[] { "127.1", "0x7f000001", "999.1.1.1", "example.com;whoami", "-n 100", "a'$(whoami)", "a\nb", "host:0", "host:65536", "host:+443", "https://user:password@example.com", "https://", "[::1]", "fe80::1%2", new string('a', 513) })
            check(ShortcutTargets.Parse(input) is null, "Reject unsafe or ambiguous clipboard input: " + input.Replace('\n',' '));
        check(ShortcutTargets.Matches("ping", ShortcutTargets.Parse("10.0.0.1")!) && !ShortcutTargets.Matches("tcp", ShortcutTargets.Parse("10.0.0.1")!), "Only matching configured action kinds receive an IP suggestion");
        check(ShortcutTargets.Matches("lookup", ShortcutTargets.Parse("server.example")!) && !ShortcutTargets.Matches("cmd", ShortcutTargets.Parse("server.example")!), "Hostname matching never interpolates into arbitrary scripts");
        reject(() => ShortcutTargets.Require("tcp", "server.example"), "TCP action requires a port");
        var action = new EntryAction { Kind = "ping", Target = "10.0.0.1", KeepTerminalOpen = true };
        var fingerprint = ActionApprovals.Fingerprint(action); action.SuggestFromClipboard = true;
        check(fingerprint != ActionApprovals.Fingerprint(action), "Enabling clipboard substitution requires new action approval");
        check(Codec.Clone(action).SuggestFromClipboard && Codec.Clone(new Preferences { Layout = "grid" }).Layout == "grid", "Suggestion and layout preferences round trip");
        var provider = new ShortcutProvider();
        check(provider.CopyActions.Count == 0 && ((IContentProvider)provider).EditorFields.Count == 0, "Standalone shortcuts require no dummy secret or copy value");
        reject(() => provider.Copy(new(), "Copy"), "Shortcuts do not accidentally expose a clipboard value");
    }
}
