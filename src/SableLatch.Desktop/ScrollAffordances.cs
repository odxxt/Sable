using System.Windows.Documents;

namespace SableLatch.Desktop;

// Each visible scroller owns one lightweight overlay. No polling or animation timer.
internal static class ScrollAffordances
{
    private static readonly DependencyProperty OverlayProperty = DependencyProperty.RegisterAttached("Overlay", typeof(ScrollEdgeOverlay), typeof(ScrollAffordances));
    public static void Register() => EventManager.RegisterClassHandler(typeof(ScrollViewer), FrameworkElement.LoadedEvent, new RoutedEventHandler(Loaded), true);
    private static void Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ScrollViewer viewer) return;
        // Templated controls can retarget OriginalSource and finish their adorner tree after Loaded.
        Attach(viewer);
        viewer.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => { if (viewer.IsLoaded) Attach(viewer); }));
    }
    internal static void Attach(ScrollViewer viewer)
    {
        var layer = AdornerLayer.GetAdornerLayer(viewer); if (layer is null) return;
        if (viewer.GetValue(OverlayProperty) is ScrollEdgeOverlay previous)
        {
            if (layer.GetAdorners(viewer)?.Contains(previous) == true) return;
            previous.Remove(); viewer.ScrollChanged -= Changed; viewer.Unloaded -= Unloaded; viewer.IsVisibleChanged -= VisibilityChanged;
        }
        var overlay = new ScrollEdgeOverlay(viewer, layer); viewer.SetValue(OverlayProperty, overlay); layer.Add(overlay);
        viewer.ScrollChanged += Changed; viewer.Unloaded += Unloaded; viewer.IsVisibleChanged += VisibilityChanged;
    }
    private static void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is ScrollViewer viewer && viewer.GetValue(OverlayProperty) is ScrollEdgeOverlay overlay) overlay.InvalidateVisual();
    }
    private static void Changed(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer viewer && e.OriginalSource == viewer && viewer.GetValue(OverlayProperty) is ScrollEdgeOverlay overlay) overlay.InvalidateVisual();
    }
    private static void Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ScrollViewer viewer) return;
        if (viewer.GetValue(OverlayProperty) is ScrollEdgeOverlay overlay) overlay.Remove();
        viewer.ClearValue(OverlayProperty); viewer.ScrollChanged -= Changed; viewer.Unloaded -= Unloaded; viewer.IsVisibleChanged -= VisibilityChanged;
    }
}
internal sealed class ScrollEdgeOverlay : Adorner
{
    private readonly ScrollViewer viewer;
    private readonly AdornerLayer layer;
    public bool HasAbove => viewer.IsVisible && viewer.ScrollableHeight > 0 && viewer.VerticalOffset > .5;
    public bool HasBelow => viewer.IsVisible && viewer.ScrollableHeight > 0 && viewer.VerticalOffset < viewer.ScrollableHeight - .5;
    public ScrollEdgeOverlay(ScrollViewer viewer, AdornerLayer layer) : base(viewer) { this.viewer = viewer; this.layer = layer; IsHitTestVisible = false; }
    public void Remove() => layer.Remove(this);
    protected override void OnRender(DrawingContext context)
    {
        if (!HasAbove && !HasBelow) return;
        var width = Math.Max(0, viewer.ActualWidth - 7);
        var height = Math.Min(64, viewer.ActualHeight / 2);
        if (width <= 0 || height <= 0) return;
        // Smooth on all four sides: a broad vertical falloff multiplied by a horizontal mask.
        var sides = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
        foreach (var stop in new[] { (0d, (byte)0), (.08, (byte)90), (.23, (byte)230), (.5, (byte)255), (.77, (byte)230), (.92, (byte)90), (1d, (byte)0) })
            sides.GradientStops.Add(new GradientStop(Color.FromArgb(stop.Item2, 255, 255, 255), stop.Item1));
        var fade = new LinearGradientBrush { StartPoint = new Point(.5, 0), EndPoint = new Point(.5, 1) };
        foreach (var stop in new[] { (0d, (byte)0), (.18, (byte)3), (.35, (byte)14), (.5, (byte)30), (.65, (byte)14), (.82, (byte)3), (1d, (byte)0) })
            fade.GradientStops.Add(new GradientStop(Color.FromArgb(stop.Item2, 225, 225, 235), stop.Item1));
        context.PushClip(new RectangleGeometry(new Rect(0, 0, width, viewer.ActualHeight)));
        context.PushOpacityMask(sides);
        if (HasAbove) context.DrawRectangle(fade, null, new Rect(0, -height / 2, width, height));
        if (HasBelow)
        {
            context.PushTransform(new ScaleTransform(1, -1, width / 2, viewer.ActualHeight / 2));
            context.DrawRectangle(fade, null, new Rect(0, -height / 2, width, height)); context.Pop();
        }
        context.Pop(); context.Pop();
    }
}
