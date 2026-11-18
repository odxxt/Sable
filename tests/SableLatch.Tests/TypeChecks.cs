using SableLatch.Core;

internal static class TypeChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        check(TypeClipboardText.Prepare("first\r\nsecond\r\n\t") == "first\r\nsecond", "Typing strips trailing Enter/Tab while preserving internal line breaks");
        check(TypeClipboardText.Prepare("spaces  ") == "spaces  ", "Typing retains ordinary trailing spaces");
        check(TypeClipboardText.Units("a\r\nb\tc😀").SequenceEqual(new[] { "a", "\n", "b", "\t", "c", "😀" }), "Typing normalizes CRLF to one Enter and keeps Unicode scalars together");
        reject(() => TypeClipboardText.Prepare("\n\r\t"), "Empty navigation-only clipboard cannot type");
        reject(() => TypeClipboardText.Prepare("text\0hidden"), "Typing rejects embedded control commands");
        reject(() => TypeClipboardText.Prepare("\ud800"), "Typing rejects unmatched Unicode surrogates");
        reject(() => TypeClipboardText.Prepare(new string('x', 100001)), "Typing bounds clipboard length");
        var defaults = new TypeClipboardOptions();
        check(defaults.InputMode == "auto", "Automatic keyboard delivery is the default for local and remote control fields");
        var legacy = new VaultItem { Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { custom = "true", startDelay = "500", characterDelay = "25", inputMode = "unicode" }) };
        check(TypeClipboardOptions.From(legacy, defaults).InputMode == "auto", "Existing default-Unicode entry overrides adopt automatic delivery");
        legacy.Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { custom = "true", startDelay = "500", characterDelay = "25", inputMode = "unicode", typingVersion = "2" });
        check(TypeClipboardOptions.From(legacy, defaults).InputMode == "unicode", "Explicit Unicode selection in the current editor is preserved");
        check(defaults.StartDelayMs == 500 && defaults.CharacterDelayMs == 25 && defaults.ConfirmMultiline, "Typing defaults match agreed timing and multiline confirmation");
        reject(() => new TypeClipboardOptions { CharacterDelayMs = 0 }.Validate(), "Typing pacing cannot create an unbounded zero-delay queue");
        reject(() => new TypeClipboardOptions { InputMode = "shell" }.Validate(), "Only supported native typing modes accepted");
        check(TypeClipboardOptions.From(new VaultItem(), defaults).StartDelayMs == 500, "Entries inherit current module defaults");
        var sent = new List<string>(); var options = defaults with { StartDelayMs = 0, CharacterDelayMs = 1 };
        TypeClipboardRunner.Run(TypeClipboardText.Prepare("ab\n"), options, () => true, sent.Add, CancellationToken.None).GetAwaiter().GetResult();
        check(sent.SequenceEqual(new[] { "a", "b" }), "Typing never appends or sends a trailing submit key");
        using var cancel = new CancellationTokenSource(); sent.Clear();
        try { TypeClipboardRunner.Run("abc", options, () => true, unit => { sent.Add(unit); cancel.Cancel(); }, cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        check(sent.Count == 1, "Cancellation stops before queuing another character");
        sent.Clear(); var matches = true;
        try { TypeClipboardRunner.Run("abc", options, () => matches, unit => { sent.Add(unit); matches = false; }, CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidOperationException) { }
        check(sent.Count == 1, "Destination change aborts before further input");
        var manifest = new ModuleManifest { Id = "example.type", Name = "Type", Author = "Example", Handler = "type", Icon = "keyboard", Category = "extras" }; manifest.Validate();
        check(manifest.EffectiveCategory == "extras", "Type capability belongs to Extras without executable package code");
        manifest.Fields.Add(new() { Key = "password", Label = "Password", Kind = "secret" });
        reject(() => manifest.Validate(), "Type package cannot declare vault secret fields");
        var field = new ModuleManifest { Id = "example.note", Name = "Note", Author = "Example", Fields = [new() { Key = "body", Label = "Body" }] };
        var schema = field.SchemaFingerprint(); field.Category = "extras"; field.Validate();
        check(field.SchemaFingerprint() == schema && field.EffectiveCategory == "extras", "Optional module categorization preserves existing field security fingerprints");
        field.Category = "arbitrary"; reject(() => field.Validate(), "Unknown category rejected");
    }
}
