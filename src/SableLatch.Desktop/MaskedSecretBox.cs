using System.Windows.Automation.Peers;

namespace SableLatch.Desktop;

// The TextBox document and automation tree contain only bullets and line breaks.
// Plaintext never enters its editing/undo buffers; revealing uses a separate opt-in editor.
internal sealed class MaskedSecretBox : TextBox
{
    private string value = "";
    private bool updating;
    public string Password
    {
        get => value;
        set { this.value = value ?? ""; RefreshMask(Math.Min(SelectionStart, this.value.Length)); }
    }
    public MaskedSecretBox()
    {
        Style = (Style)Application.Current.FindResource(typeof(TextBox));
        AcceptsReturn = true; TextWrapping = TextWrapping.Wrap; VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        IsUndoEnabled = false; MaxLength = 100_000; AllowDrop = false; ContextMenu = new ContextMenu();
        InputMethod.SetIsInputMethodEnabled(this, false); SpellCheck.SetIsEnabled(this, false);
        foreach (var command in new[] { ApplicationCommands.Copy, ApplicationCommands.Cut, ApplicationCommands.Undo, ApplicationCommands.Redo })
            CommandBindings.Add(new CommandBinding(command, (_, e) => e.Handled = true, (_, e) => { e.CanExecute = false; e.Handled = true; }));
        DataObject.AddPastingHandler(this, (_, e) =>
        {
            if (e.DataObject.GetDataPresent(DataFormats.UnicodeText) && e.DataObject.GetData(DataFormats.UnicodeText) is string text) ReplaceSelection(text);
            e.CancelCommand();
        });
    }
    public new void Clear() => Password = "";
    internal void ReplaceSelection(string text)
    {
        var start = Math.Min(SelectionStart, value.Length); var length = Math.Min(SelectionLength, value.Length - start);
        if (value.Length - length + text.Length > MaxLength) return;
        value = value.Remove(start, length).Insert(start, text); RefreshMask(start + text.Length);
    }
    private void RefreshMask(int caret)
    {
        updating = true;
        try { Text = string.Create(value.Length, value, (span, source) => { for (var i = 0; i < span.Length; i++) span[i] = source[i] is '\r' or '\n' ? source[i] : '●'; }); Select(caret, 0); }
        finally { updating = false; }
    }
    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text)) ReplaceSelection(e.Text);
        e.Handled = true; base.OnPreviewTextInput(e);
    }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { ReplaceSelection(Environment.NewLine); e.Handled = true; }
        else if (e.Key is Key.Back or Key.Delete)
        {
            if (SelectionLength == 0)
            {
                var start = SelectionStart;
                if (e.Key == Key.Back && start > 0)
                {
                    var count = start > 1 && (value[start - 2] == '\r' && value[start - 1] == '\n' || char.IsSurrogatePair(value, start - 2)) ? 2 : 1;
                    Select(start - count, count);
                }
                else if (e.Key == Key.Delete && start < value.Length)
                {
                    var count = start + 1 < value.Length && (value[start] == '\r' && value[start + 1] == '\n' || char.IsSurrogatePair(value, start)) ? 2 : 1;
                    Select(start, count);
                }
            }
            ReplaceSelection(""); e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        // Ignore edits from unsupported commands rather than exposing or corrupting the value.
        if (!updating) RefreshMask(Math.Min(SelectionStart, value.Length));
        base.OnTextChanged(e);
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new SecretPeer(this);
    private sealed class SecretPeer(MaskedSecretBox owner) : TextBoxAutomationPeer(owner)
    {
        protected override bool IsPasswordCore() => true;
        public override object? GetPattern(PatternInterface patternInterface) => patternInterface is PatternInterface.Value or PatternInterface.Text ? null : base.GetPattern(patternInterface);
    }
}
