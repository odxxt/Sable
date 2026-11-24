using System.Globalization;
using System.Net;
using System.Windows.Automation;

namespace SableLatch.Desktop;

internal sealed class Ipv4EntryField : StackPanel
{
    private const string OctetError = "Enter four numbers from 0 to 255.";
    private static readonly Brush ErrorBrush = CreateErrorBrush();
    private readonly TextBox[] parts = new TextBox[4];
    private readonly Border outline;
    private readonly TextBlock error;
    private bool loading;

    internal Ipv4EntryField(string label, string stored, bool subnetMask = false, Button? remove = null)
    {
        Margin = new Thickness(0, 0, 0, 7);
        var caption = new TextBlock { Text = label, FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        Children.Add(caption);
        AutomationProperties.SetName(this, label);

        var row = new DockPanel();
        if (remove is not null)
        {
            remove.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
        }
        outline = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(3, 2, 3, 2) };
        outline.SetResourceReference(Border.BackgroundProperty, "ControlSurface");
        outline.SetResourceReference(Border.BorderBrushProperty, "Line");
        row.Children.Add(outline);
        Children.Add(row);

        var grid = new Grid();
        outline.Child = grid;
        for (var index = 0; index < parts.Length; index++)
        {
            if (index > 0)
            {
                grid.ColumnDefinitions.Add(new() { Width = new GridLength(10) });
                var dot = new TextBlock { Text = ".", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
                dot.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
                Grid.SetColumn(dot, grid.ColumnDefinitions.Count - 1);
                grid.Children.Add(dot);
            }
            grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            var part = new TextBox { MaxLength = 3, Padding = new Thickness(0), MinHeight = 25, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Background = Brushes.Transparent };
            AutomationProperties.SetName(part, $"{label}, part {index + 1}");
            InputMethod.SetIsInputMethodEnabled(part, false);
            parts[index] = part;
            var current = index;
            part.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsAsciiDigit);
            part.PreviewKeyDown += (_, e) => HandleNavigation(current, e);
            part.TextChanged += (_, _) => PartChanged(current);
            part.IsKeyboardFocusedChanged += (_, _) =>
            {
                if (part.IsKeyboardFocused) part.SetResourceReference(Control.BackgroundProperty, "HoverSurface");
                else part.Background = Brushes.Transparent;
            };
            DataObject.AddPastingHandler(part, (_, e) => Paste(e, subnetMask));
            Grid.SetColumn(part, grid.ColumnDefinitions.Count - 1);
            grid.Children.Add(part);
        }
        error = new TextBlock { Foreground = ErrorBrush, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
        AutomationProperties.SetLiveSetting(error, AutomationLiveSetting.Polite);
        Children.Add(error);
        IsKeyboardFocusWithinChanged += (_, _) =>
        {
            if (!IsKeyboardFocusWithin && HasValue && !HasValidOctets) ShowError(OctetError, focus: false);
        };
        var value = subnetMask ? FromPrefix(stored, requireSlash: false) ?? stored : stored;
        var segments = value.Split('.');
        // Keep malformed legacy values invalid instead of dropping extra pieces.
        WriteParts(segments.Length <= 4 ? segments : [value]);
    }

    internal bool HasValue => parts.Any(part => part.Text.Length != 0);
    private bool HasValidOctets => parts.All(part => TryOctet(part.Text, out _));
    internal string Value => !HasValue ? "" : string.Join(".", parts.Select(part =>
        TryOctet(part.Text, out var octet) ? octet.ToString(CultureInfo.InvariantCulture) : part.Text));

    internal void Clear() => Reset("");

    internal void Reset(string value)
    {
        WriteParts(value.Split('.'));
        DismissError();
    }

    internal void ShowError(string message, bool focus = true)
    {
        error.Text = message;
        error.Visibility = Visibility.Visible;
        outline.BorderBrush = ErrorBrush;
        AutomationProperties.SetHelpText(this, message);
        if (!focus) return;
        var firstInvalid = Array.FindIndex(parts, part => !TryOctet(part.Text, out _));
        parts[Math.Max(0, firstInvalid)].Focus();
        BringIntoView();
    }

    internal void DismissError()
    {
        error.Visibility = Visibility.Collapsed;
        error.Text = "";
        outline.SetResourceReference(Border.BorderBrushProperty, "Line");
        AutomationProperties.SetHelpText(this, "");
    }

    internal void FocusFirst()
    {
        parts[0].Focus();
        parts[0].SelectAll();
        BringIntoView();
    }

    internal static string FormatMask(int prefix)
    {
        // The parser accepts prefix lengths from 1 to 32.
        var mask = uint.MaxValue << (32 - prefix);
        return new IPAddress([(byte)(mask >> 24), (byte)(mask >> 16), (byte)(mask >> 8), (byte)mask]).ToString();
    }

    private static string? FromPrefix(string value, bool requireSlash)
    {
        if (value.StartsWith('/')) value = value[1..];
        else if (requireSlash) return null;
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var prefix) && prefix is >= 1 and <= 32
            ? FormatMask(prefix) : null;
    }

    private static bool TryOctet(string value, out byte octet)
    {
        octet = 0;
        return value.Length is >= 1 and <= 3 && value.All(char.IsAsciiDigit)
            && byte.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out octet);
    }

    private void WriteParts(string[] values)
    {
        loading = true;
        try
        {
            for (var i = 0; i < parts.Length; i++) parts[i].Text = i < values.Length ? values[i] : "";
        }
        finally { loading = false; }
    }

    private void PartChanged(int index)
    {
        if (loading) return;
        var box = parts[index];
        var valid = TryOctet(box.Text, out _);
        if (box.Text.Length > 0 && !valid) ShowError(OctetError, focus: false);
        else if (HasValidOctets || !HasValue) DismissError();
        if (valid && box.Text.Length == 3 && box.IsKeyboardFocused && index + 1 < parts.Length) FocusPart(index + 1);
    }

    private void HandleNavigation(int index, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.Key is Key.OemPeriod or Key.Decimal)
        {
            e.Handled = true;
            if (index + 1 < parts.Length) FocusPart(index + 1);
        }
        else if (e.Key == Key.Back && parts[index].Text.Length == 0 && index > 0)
        {
            e.Handled = true;
            parts[index - 1].Focus();
            parts[index - 1].CaretIndex = parts[index - 1].Text.Length;
        }
    }

    private void FocusPart(int index)
    {
        parts[index].Focus();
        parts[index].SelectAll();
    }

    private void Paste(DataObjectPastingEventArgs e, bool subnetMask)
    {
        if (e.SourceDataObject.GetData(DataFormats.UnicodeText) is not string text)
        {
            e.CancelCommand();
            return;
        }
        text = text.Trim();
        if (TryOctet(text, out _)) return;
        e.CancelCommand();
        if (text.Length > 15) { ShowError(OctetError, focus: false); return; }
        if (subnetMask && FromPrefix(text, requireSlash: true) is { } mask) text = mask;
        var values = text.Split('.');
        if (values.Length != 4 || !values.All(part => TryOctet(part, out _)))
        {
            ShowError(subnetMask ? "Paste a subnet mask or prefix such as /24." : OctetError, focus: false);
            return;
        }
        WriteParts(values);
        DismissError();
    }

    private static Brush CreateErrorBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(255, 166, 173));
        brush.Freeze();
        return brush;
    }
}
