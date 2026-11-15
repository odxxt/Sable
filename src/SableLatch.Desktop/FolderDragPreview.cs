using System.Runtime.InteropServices;
using System.Windows.Documents;

namespace SableLatch.Desktop;

internal sealed class FolderDragPreview(UIElement host, Button chip, Point grab) : Adorner(host)
{
    private Point position;
    private readonly VisualBrush content = new(chip) { Stretch = Stretch.Fill };
    internal void Follow(Visual relative)
    {
        if (!GetCursorPos(out var point)) return;
        position = relative.PointFromScreen(new Point(point.X, point.Y)) - (Vector)grab;
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(position, chip.RenderSize);
        dc.PushOpacity(.94);
        dc.DrawRoundedRectangle((Brush)Application.Current.FindResource("HoverSurface"), null, bounds, 8, 8);
        dc.DrawRectangle(content, null, bounds);
        dc.Pop();
    }
    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X, Y; }
    [DllImport("user32")] private static extern bool GetCursorPos(out CursorPoint point);
}
