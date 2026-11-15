using System.Windows.Automation;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private void HandlePanelDeactivated()
    {
        if (screen == "list" && !session.KeepsPanelOpen && !holdForPassword && !busy && !contextOpen && modalDepth == 0) Hide();
    }

    private void UpdatePanelTopmost() => Topmost = session.NeedsTopmost(holdForPassword);

    private void UpdatePinButton()
    {
        // Rotate inside a fixed 16px drawing area; the header's 28px slot never changes.
        var drawing = new Grid { Width = 16, Height = 16, IsHitTestVisible = false };
        var pin = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 5,2 L 11,2 M 6,2 L 6,6 4,9 12,9 10,6 10,2 M 8,9 L 8,14"),
            Stroke = session.Mode == PanelMode.Tray ? new SolidColorBrush(Color.FromRgb(166, 166, 173)) : Brushes.White,
            StrokeThickness = 1.35, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            RenderTransform = new RotateTransform(session.Mode switch { PanelMode.AlwaysOnTop => 0, PanelMode.Window => 90, _ => 45 }, 8, 8)
        };
        drawing.Children.Add(pin); PinButton.Content = drawing;
        PinButton.Background = session.KeepsPanelOpen ? (Brush)FindResource("HoverSurface") : Brushes.Transparent;
        var current = session.Mode switch { PanelMode.AlwaysOnTop => "Always on top", PanelMode.Window => "Normal window", _ => "Tray mode" };
        PinButton.ToolTip = session.Mode switch
        {
            PanelMode.AlwaysOnTop => "Always on top · timer paused\nClick for normal window",
            PanelMode.Window => "Normal window · timer active\nClick for tray mode",
            _ => "Tray mode · timer active\nClick to keep on top"
        };
        AutomationProperties.SetName(PinButton, "Window mode: " + current);
        AutomationProperties.SetHelpText(PinButton, (string)PinButton.ToolTip);
    }
}
