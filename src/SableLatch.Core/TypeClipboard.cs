namespace SableLatch.Core;

// Clipboard text is operation-scoped and never part of this settings model or a module payload.
public sealed record TypeClipboardOptions
{
    public int StartDelayMs { get; set; } = 500;
    public int CharacterDelayMs { get; set; } = 25;
    public string InputMode { get; set; } = "auto";
    public bool ConfirmMultiline { get; set; } = true;
    public void Validate()
    {
        if (StartDelayMs is < 0 or > 10000 || CharacterDelayMs is < 1 or > 1000 || InputMode is not ("auto" or "unicode" or "keyboard"))
            throw new InvalidDataException("Use a start delay of 0–10,000 ms and a character delay of 1–1,000 ms.");
    }
    public static TypeClipboardOptions From(VaultItem item, TypeClipboardOptions defaults)
    {
        if (item.Field("custom") != "true") return defaults with { };
        if (!int.TryParse(item.Field("startDelay"), out var start) || !int.TryParse(item.Field("characterDelay"), out var speed)) throw new InvalidDataException("Invalid typing timing.");
        var mode = item.Field("inputMode");
        if (item.Field("typingVersion") != "2" && mode == "unicode") mode = "auto";
        var result = new TypeClipboardOptions { StartDelayMs = start, CharacterDelayMs = speed, InputMode = mode, ConfirmMultiline = item.Field("confirmMultiline") != "false" };
        result.Validate(); return result;
    }
}

public static class TypeClipboardText
{
    public static string Prepare(string text)
    {
        if (text.Length > 100000) throw new InvalidDataException("Type supports up to 100,000 characters at a time.");
        // Never finish with a submit/navigation key. Ordinary spaces remain unchanged.
        text = text.TrimEnd('\r', '\n', '\t');
        if (text.Length == 0) throw new InvalidDataException("Copy some text before using Type clipboard.");
        if (text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))) throw new InvalidDataException("The clipboard contains unsupported control characters.");
        for (var i = 0; i < text.Length; i++)
            if (char.IsSurrogate(text[i]) && (!char.IsHighSurrogate(text[i]) || ++i >= text.Length || !char.IsLowSurrogate(text[i]))) throw new InvalidDataException("The clipboard contains invalid Unicode text.");
        return text;
    }
    public static IEnumerable<string> Units(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r') { if (i + 1 < text.Length && text[i + 1] == '\n') i++; yield return "\n"; }
            else if (char.IsHighSurrogate(text[i])) yield return text.Substring(i++, 2);
            else yield return text[i].ToString();
        }
    }
}

public static class TypeClipboardRunner
{
    // Single-character sends keep cancellation responsive; no whole-document input queue.
    public static async Task Run(string prepared, TypeClipboardOptions options, Func<bool> targetMatches, Action<string> send, CancellationToken cancellation)
    {
        options.Validate();
        async Task Wait(int milliseconds)
        {
            while (milliseconds > 0) { cancellation.ThrowIfCancellationRequested(); if (!targetMatches()) throw new InvalidOperationException("Typing stopped because the destination window changed."); var step = Math.Min(milliseconds, 10); await Task.Delay(step, cancellation); milliseconds -= step; }
        }
        await Wait(options.StartDelayMs);
        foreach (var unit in TypeClipboardText.Units(prepared))
        {
            cancellation.ThrowIfCancellationRequested();
            if (!targetMatches()) throw new InvalidOperationException("Typing stopped because the destination window changed.");
            send(unit); await Wait(options.CharacterDelayMs);
        }
    }
}
