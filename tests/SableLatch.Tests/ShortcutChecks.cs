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
        var secret = new VaultItem { Label = "Fixture name", Description = "Optional description", ClickBehavior = "run-copy", Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { text = "'; $(throw 'not-code')\nfixture" }), ScriptValues = new() { ["value2"] = "second" } };
        var bindings = ScriptBindings.For(secret);
        check(bindings["name"] == secret.Label && bindings["value1"] == secret.Field("text") && bindings["value2"] == "second", "Script fields remain exact data including shell syntax and newlines");
        secret.ScriptValues["value1"] = "collision";
        reject(() => ScriptBindings.For(secret), "Custom fields cannot override the primary secret"); secret.ScriptValues.Remove("value1");
        foreach (var invalid in new[] { "name", "HOME", "value0", "value01", "value21", "value1;whoami" }) check(!ScriptBindings.IsValueName(invalid), "Reject reserved or invalid variable " + invalid);
        var roundTrip = Codec.Clone(secret);
        check(roundTrip.Description == secret.Description && roundTrip.ClickBehavior == "run-copy" && roundTrip.ScriptValues["value2"] == "second", "Entry description, behavior and custom values round trip");
        var document = new VaultDocument { Items = [secret], Folders = [new() { Name = "Grid", Layout = "grid" }, new() { Name = "Rows", Layout = "rows" }] };
        document.Validate(); check(Codec.Clone(document).Folders.Select(f => f.Layout).SequenceEqual(new[] { "grid", "rows" }), "Folder views are independently persisted");
        action = new EntryAction { Kind = "powershell", Target = "$value1", KeepTerminalOpen = true };
        fingerprint = ActionApprovals.Fingerprint(action); action.UseEntryVariables = true;
        check(fingerprint != ActionApprovals.Fingerprint(action), "Passing entry fields invalidates prior execution consent");
        fingerprint = ActionApprovals.Fingerprint(action); action.RunAsAdministrator = true;
        check(fingerprint != ActionApprovals.Fingerprint(action), "Elevation invalidates prior execution consent");
        var root = new VaultFolder { Name = "Delete me" }; var nested = new VaultFolder { Name = "Nested", ParentId = root.Id }; var keep = new VaultFolder { Name = "Keep" };
        var inside = new VaultItem { FolderId = root.Id }; var trashed = new VaultItem { FolderId = nested.Id, DeletedAt = DateTimeOffset.UtcNow }; var outside = new VaultItem { FolderId = keep.Id };
        var tree = new VaultDocument { Folders = [root, nested, keep], Items = [inside, trashed, outside] };
        check(FolderOperations.Subtree(tree, root.Id).SetEquals([root.Id, nested.Id]), "Folder deletion collects only its own nested subtree");
        var snapshot = Codec.Encode(tree); reject(() => FolderOperations.DeleteTree(tree, Guid.NewGuid()), "Missing-folder deletion is rejected");
        check(snapshot.SequenceEqual(Codec.Encode(tree)), "Rejected folder deletion leaves all contents unchanged");
        FolderOperations.DeleteTree(tree, root.Id); tree.Validate();
        check(tree.Folders.Count == 1 && tree.Folders.Single().Id == keep.Id && tree.Items.Count == 1 && tree.Items.Single().Id == outside.Id, "Recursive deletion removes nested folders and their active/trashed entries while preserving siblings");
        tree.Folders.Add(new VaultFolder { Name = "Empty" }); FolderOperations.DeleteTree(tree, tree.Folders.Last().Id); tree.Validate();
        check(tree.Folders.Count == 1 && tree.Items.Count == 1, "Empty folder deletion leaves other entries untouched");
    }
}
