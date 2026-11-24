using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Data;

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
                suggestionHost.Children.Add(BuildSuggestionCard(suggestionHost, diagnostic, slot, sequence));
            }
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException) { /* Clipboard may belong to another process. Never retry in a loop. */ }
    }
    private Border BuildSuggestionCard(StackPanel host, ClipboardDiagnostic diagnostic, int slot, uint sequence)
    {
        var target = diagnostic.Target.Display;
        var startEpoch = epoch;
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new());
        layout.ColumnDefinitions.Add(new() { Width = new GridLength(30) });
        var card = new Border
        {
            Child = layout, CornerRadius = new CornerRadius(10), Height = 52,
            Margin = new Thickness(0, 4, 0, 4), Padding = new Thickness(10, 6, 8, 6)
        };
        card.SetResourceReference(Border.BackgroundProperty, "OptionGroupSurface");

        bool Active() => epoch == startEpoch && vault.IsUnlocked && IsVisible && WindowState != WindowState.Minimized
            && screen == "list" && folder is null && vault.Document!.Settings.ClipboardSuggestions && sequence == SuggestionSequence()
            && ReferenceEquals(host, suggestionHost) && host.Children.Contains(card);

        var launch = SuggestionAction("", async () =>
        {
            if (!Active()) return;
            await LaunchEntryAction(new EntryAction { Kind = diagnostic.Kind, Target = target, KeepTerminalOpen = true });
        });
        launch.Tag = "network-run";
        launch.Padding = new Thickness(0);
        launch.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        launch.ToolTip = $"Run {diagnostic.Label} with {target} from the clipboard";
        AutomationProperties.SetName(launch, "Suggested " + diagnostic.Label);
        AutomationProperties.SetHelpText(launch, target + " from clipboard");

        var body = new Grid();
        body.ColumnDefinitions.Add(new());
        body.ColumnDefinitions.Add(new() { Width = new GridLength(28) });
        var caption = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(1, 0, 8, 0) };
        caption.Children.Add(new TextBlock { Text = diagnostic.Label, FontSize = 11, Foreground = (Brush)FindResource("Muted"), TextTrimming = TextTrimming.CharacterEllipsis });
        var address = new TextBlock
        {
            Text = target, FontSize = 13, FontWeight = FontWeights.Medium,
            Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis
        };
        System.Windows.Documents.Typography.SetNumeralAlignment(address, FontNumeralAlignment.Tabular);
        caption.Children.Add(address);
        body.Children.Add(caption);
        var runIcon = SuggestionGlyph(launch, "M 3,2 L 13,8 3,14 Z", 16);
        Grid.SetColumn(runIcon, 1);
        body.Children.Add(runIcon);
        launch.Content = body;
        layout.Children.Add(launch);

        var dismiss = SuggestionAction("", () => { });
        dismiss.Click += (_, _) =>
        {
            if (!Active()) return;
            if (vault.Document!.Settings.ClipboardDismissHintSeen) { DismissCopy(); return; }
            if (!Change(d => d.Settings.ClipboardDismissHintSeen = true)) return;
            ShowDismissChoices();
        };
        dismiss.Height = 32;
        dismiss.Padding = new Thickness(0);
        dismiss.VerticalAlignment = VerticalAlignment.Center;
        dismiss.ToolTip = "Dismiss suggestion";
        dismiss.Content = SuggestionGlyph(dismiss, "M 2,2 L 10,10 M 10,2 L 2,10", 12);
        AutomationProperties.SetName(dismiss, "Dismiss network suggestion");
        Grid.SetColumn(dismiss, 1);
        layout.Children.Add(dismiss);
        return card;

        void DismissCopy()
        {
            if (!Active()) return;
            dismissedSuggestionSlots.Add(slot);
            RefreshSuggestions();
        }

        void ShowDismissChoices()
        {
            var choices = new DockPanel { LastChildFill = true };
            var never = SuggestionAction("Don't show again", () =>
            {
                if (Active() && Change(d => d.Settings.ClipboardSuggestions = false)) host.Children.Clear();
            });
            var once = SuggestionAction("Only this copy", DismissCopy);
            foreach (var choice in new[] { never, once })
            {
                choice.Height = 32;
                choice.FontSize = 11;
                choice.Padding = new Thickness(6, 0, 6, 0);
                choice.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(choice, Dock.Right);
                choices.Children.Add(choice);
            }
            var prompt = new TextBlock
            {
                Text = "Hide this?", FontSize = 11, Foreground = (Brush)FindResource("Muted"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            choices.Children.Add(prompt);
            var keepKeyboardFocus = dismiss.IsKeyboardFocused;
            card.Child = choices;
            if (keepKeyboardFocus) once.Focus();
        }
    }

    private Button SuggestionAction(string text, Action click)
    {
        var button = Button(text, click);
        button.Style = (Style)FindResource("SuggestionButton");
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.Margin = new Thickness(0);
        return button;
    }

    private static System.Windows.Shapes.Path SuggestionGlyph(Button button, string geometry, double size)
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(geometry), Width = size, Height = size, StrokeThickness = 1.2,
            StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false
        };
        path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, new Binding(nameof(Control.Foreground)) { Source = button });
        return path;
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
