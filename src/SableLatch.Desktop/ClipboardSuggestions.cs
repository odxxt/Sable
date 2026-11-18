using System.Runtime.InteropServices;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private StackPanel? suggestionHost;
    private bool suggestionPending;
    private uint suggestionSequence;
    private readonly HashSet<int> dismissedSuggestionSlots = [];
    [DllImport("user32.dll")] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
#if DEBUG
    private string? previewSuggestionText = null;
    private uint previewSuggestionSequence;
#endif
    private uint SuggestionSequence()
    {
#if DEBUG
        if (App.PreviewMode) return previewSuggestionSequence;
#endif
        return Native.GetClipboardSequenceNumber();
    }
    private void QueueSuggestions()
    {
        if (suggestionPending || !IsVisible || WindowState == WindowState.Minimized || !vault.IsUnlocked || screen != "list" || folder is not null) return;
        suggestionPending = true;
        Dispatcher.BeginInvoke(() => { suggestionPending = false; RefreshSuggestions(); });
    }
    private void RefreshSuggestions()
    {
        suggestionHost?.Children.Clear();
        if (suggestionHost is null || !IsVisible || WindowState == WindowState.Minimized || screen != "list" || folder is not null || !vault.IsUnlocked || !vault.Document!.Settings.ClipboardSuggestions) return;
        var sequence = SuggestionSequence();
        if (sequence != suggestionSequence) { suggestionSequence = sequence; dismissedSuggestionSlots.Clear(); }
        try
        {
            string? text;
#if DEBUG
            if (App.PreviewMode) text = previewSuggestionText;
            else
#endif
            {
                if (clipboard is ClipboardService service && service.OwnsCurrentClipboard) return;
                if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing") || Clipboard.ContainsData("CanIncludeInClipboardHistory")) return;
                text = ReadSmallClipboardText();
            }
            if (sequence != SuggestionSequence()) return;
            foreach (var (diagnostic, slot) in ClipboardDiagnostics.Extract(text).Select((d, index) => (d, index)))
            {
                if (dismissedSuggestionSlots.Contains(slot)) continue;
                var target = diagnostic.Target; var startEpoch = epoch;
                bool Active() => epoch == startEpoch && vault.IsUnlocked && IsVisible && WindowState != WindowState.Minimized && screen == "list" && folder is null && vault.Document!.Settings.ClipboardSuggestions && sequence == SuggestionSequence();
                var b = Button("", async () =>
                {
                    if (!Active()) return;
                    await LaunchEntryAction(new EntryAction { Kind = diagnostic.Kind, Target = target.Display, KeepTerminalOpen = true });
                }); b.Style = (Style)FindResource("SuggestionButton");
                b.Tag = "network-run";
                b.Padding = new Thickness(4, 0, 4, 0); b.Margin = new Thickness(0); b.Background = Brushes.Transparent; b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                b.ToolTip = "Run " + diagnostic.Label + " with " + target.Display + " from the clipboard";
                var body = new Grid(); body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new() { Width = new GridLength(28) });
                var caption = new StackPanel(); body.Children.Add(caption);
                caption.Children.Add(new TextBlock { Text = diagnostic.Label, FontSize = 11, Foreground = (Brush)FindResource("Muted"), TextTrimming = TextTrimming.CharacterEllipsis });
                var address = new TextBlock { Text = target.Display, FontSize = 13, FontWeight = FontWeights.Medium, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
                System.Windows.Documents.Typography.SetNumeralAlignment(address, FontNumeralAlignment.Tabular); caption.Children.Add(address);
                var run = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 3,2 L 13,8 3,14 Z"), Width = 16, Height = 16, Stroke = (Brush)FindResource("Accent"), StrokeThickness = 1.2, StrokeLineJoin = PenLineJoin.Round, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                Grid.SetColumn(run, 1); body.Children.Add(run); b.Content = body;
                System.Windows.Automation.AutomationProperties.SetName(b, "Suggested " + diagnostic.Label); System.Windows.Automation.AutomationProperties.SetHelpText(b, target.Display + " from clipboard");
                var card = new Border { CornerRadius = new CornerRadius(10), Background = (Brush)FindResource("ControlSurface"), Margin = new Thickness(0, 4, 0, 4), Height = 52, Padding = new Thickness(8) };
                var content = new Grid(); content.ColumnDefinitions.Add(new()); content.ColumnDefinitions.Add(new() { Width = new GridLength(32) }); card.Child = content; content.Children.Add(b);
                var dismiss = Button("", () =>
                {
                    if (!Active()) return;
                    if (vault.Document!.Settings.ClipboardDismissHintSeen) { dismissedSuggestionSlots.Add(slot); RefreshSuggestions(); return; }
                    if (!Change(d => d.Settings.ClipboardDismissHintSeen = true)) return;
                    content.Children.Clear(); content.ColumnDefinitions.Clear();
                    content.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); content.ColumnDefinitions.Add(new()); content.ColumnDefinitions.Add(new());
                    content.Children.Add(new TextBlock { Text = "Hide this?", FontSize = 11, Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0) });
                    var once = Button("Only this copy", () => { if (!Active()) return; dismissedSuggestionSlots.Add(slot); RefreshSuggestions(); });
                    var never = Button("Don't show again", () => { if (!Active()) return; if (Change(d => d.Settings.ClipboardSuggestions = false)) suggestionHost.Children.Clear(); });
                    var column = 1;
                    foreach (var choice in new[] { once, never }) { choice.Height = 32; choice.MinHeight = 0; choice.FontSize = 11; choice.Padding = new Thickness(4, 0, 4, 0); choice.Margin = new Thickness(2, 0, 0, 0); choice.HorizontalContentAlignment = HorizontalAlignment.Center; Grid.SetColumn(choice, column++); content.Children.Add(choice); }
                });
                dismiss.Style = (Style)FindResource("IconButton"); dismiss.Width = 32; dismiss.Height = 32; dismiss.Padding = new Thickness(0); dismiss.Margin = new Thickness(0); dismiss.Opacity = .6; dismiss.ToolTip = "Dismiss suggestion";
                dismiss.Content = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 2,2 L 10,10 M 10,2 L 2,10"), Width = 12, Height = 12, Stroke = (Brush)FindResource("Muted"), StrokeThickness = 1.25, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, IsHitTestVisible = false };
                System.Windows.Automation.AutomationProperties.SetName(dismiss, "Dismiss network suggestion"); Grid.SetColumn(dismiss, 1); content.Children.Add(dismiss);
                suggestionHost.Children.Add(card);
            }
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException) { /* Clipboard may belong to another process. Never retry in a loop. */ }
    }
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr handle);
    private static string? ReadSmallClipboardText()
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            var data = GetClipboardData(13); if (data == IntPtr.Zero) return null;
            var bytes = GlobalSize(data).ToUInt64(); if (bytes is < 2 or > (ClipboardDiagnostics.MaxCharacters + 1) * 2 || bytes % 2 != 0) return null;
            var p = GlobalLock(data); if (p == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUni(p, (int)bytes / 2)?.TrimEnd('\0'); }
            finally { GlobalUnlock(data); }
        }
        finally { CloseClipboard(); }
    }
}
