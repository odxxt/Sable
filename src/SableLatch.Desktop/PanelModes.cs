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
        // Distinct static symbols in the same drawing area and fixed toolbar slot.
        var drawing = new Grid { Width = 16, Height = 16, IsHitTestVisible = false };
        var pin = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(session.Mode switch
            {
                PanelMode.AlwaysOnTop => "M 5,2 L 11,2 M 6,2 L 6,6 4,9 12,9 10,6 10,2 M 8,9 L 8,14",
                PanelMode.Window => "M 2,2 L 14,2 14,14 2,14 Z M 2,5 L 14,5",
                _ => "M 2,2 L 14,2 14,14 2,14 Z M 5,8 L 11,8"
            }),
            Stroke = session.Mode == PanelMode.Tray ? new SolidColorBrush(Color.FromRgb(166, 166, 173)) : Brushes.White,
            StrokeThickness = 1.35, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
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
