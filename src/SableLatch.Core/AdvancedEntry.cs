namespace SableLatch.Core;

// Presentation and optional explicit actions are independent of the content provider.
public sealed class AdvancedEntry
{
    public string Color { get; set; } = "default";
    public EntryAction? Action { get; set; }
    public void Validate()
    {
        if (Color is null || Color.Length > 40) throw new InvalidDataException("Invalid entry color.");
        Action?.Validate();
    }
}

public sealed class EntryAction
{
    public string Kind { get; set; } = "application";
    public string Label { get; set; } = "Open";
    public string Target { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool VerifyEachTime { get; set; }
    public bool CopyOnRun { get; set; }
    public bool KeepTerminalOpen { get; set; }
    public void Validate()
    {
        // Unknown kinds survive vault transfers; only registered desktop handlers execute.
        if (string.IsNullOrWhiteSpace(Kind) || Kind.Length > 80 || string.IsNullOrWhiteSpace(Label) || Label.Length > 40 ||
            Target is null || Target.Length > 16000 || Arguments is null || Arguments.Length > 8000 ||
            WorkingDirectory is null || WorkingDirectory.Length > 2048 ||
            Target.Contains('\0') || Arguments.Contains('\0') || WorkingDirectory.Contains('\0'))
            throw new InvalidDataException("Invalid entry action.");
    }
}
