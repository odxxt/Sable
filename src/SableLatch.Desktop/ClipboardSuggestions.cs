using System.Runtime.InteropServices;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private StackPanel? suggestionHost;
    private bool suggestionPending;
    [DllImport("user32.dll")] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
#if DEBUG
    private string? previewSuggestionText = null;
#endif
    private void QueueSuggestions()
    {
        if (suggestionPending || !IsVisible || !vault.IsUnlocked || screen != "list") return;
        suggestionPending = true;
        Dispatcher.BeginInvoke(() => { suggestionPending = false; RefreshSuggestions(); });
    }
    private void RefreshSuggestions()
    {
        suggestionHost?.Children.Clear();
        if (suggestionHost is null || !IsVisible || screen != "list" || !vault.IsUnlocked || !vault.Document!.Settings.ClipboardSuggestions) return;
        var shortcuts = vault.Document.Items.Where(i => i.DeletedAt is null && i.ContentType == "sable.shortcut" && !vault.Document.Settings.DisabledModules.Contains(i.ContentType) && i.Advanced?.Action is { Enabled: true, SuggestFromClipboard: true } a && ShortcutTargets.IsDiagnostic(a.Kind)).OrderBy(i => i.Order).ToList();
        if (shortcuts.Count == 0) return;
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
            var target = ShortcutTargets.Parse(text); if (target is null) return;
            var matches = shortcuts.Where(i => ShortcutTargets.Matches(i.Advanced!.Action!.Kind, target)).Take(3).ToList();
            foreach (var item in matches)
            {
                var b = Button("", () => ReviewEntryAction(item, target)); b.Style = (Style)FindResource("SuggestionButton");
                b.Padding = new Thickness(12, 8, 12, 8); b.Margin = new Thickness(0, 4, 0, 4); b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                b.ToolTip = "Run " + item.Label + " with " + target.Display + " from the clipboard";
                var body = new Grid(); body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new() { Width = new GridLength(28) });
                var caption = new StackPanel(); body.Children.Add(caption);
                caption.Children.Add(new TextBlock { Text = item.Label, FontSize = 11, Foreground = (Brush)FindResource("Muted"), TextTrimming = TextTrimming.CharacterEllipsis });
                var address = new TextBlock { Text = target.Display, FontSize = 13, FontWeight = FontWeights.Medium, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
                System.Windows.Documents.Typography.SetNumeralAlignment(address, FontNumeralAlignment.Tabular); caption.Children.Add(address);
                var run = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 3,2 L 13,8 3,14 Z"), Width = 16, Height = 16, Stroke = (Brush)FindResource("Accent"), StrokeThickness = 1.2, StrokeLineJoin = PenLineJoin.Round, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(run, 1); body.Children.Add(run); b.Content = body;
                System.Windows.Automation.AutomationProperties.SetName(b, "Suggested " + item.Label); System.Windows.Automation.AutomationProperties.SetHelpText(b, target.Display + " from clipboard"); suggestionHost.Children.Add(b);
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
            var bytes = GlobalSize(data).ToUInt64(); if (bytes is < 2 or > 1026 || bytes % 2 != 0) return null;
            var p = GlobalLock(data); if (p == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUni(p, (int)bytes / 2)?.TrimEnd('\0'); }
            finally { GlobalUnlock(data); }
        }
        finally { CloseClipboard(); }
    }
}
