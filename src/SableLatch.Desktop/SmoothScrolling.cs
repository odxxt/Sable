using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

// Animate only during wheel input. No background timer or work while the panel is idle.
internal sealed class SmoothScrolling : Animatable
{
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(SmoothScrolling), typeof(SmoothScrolling));
    private static readonly DependencyProperty ProgressProperty = DependencyProperty.Register("Progress", typeof(double), typeof(SmoothScrolling), new PropertyMetadata(0d, ProgressChanged));
    private ScrollViewer? viewer;
    private bool active;
    private double start, target;
    protected override Freezable CreateInstanceCore() => new SmoothScrolling();
    public static void Register()
    {
        EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnWheel));
        EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((sender, _) => Stop(sender)));
        EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.PreviewKeyDownEvent, new KeyEventHandler((sender, _) => Stop(sender)));
        EventManager.RegisterClassHandler(typeof(ScrollViewer), FrameworkElement.UnloadedEvent, new RoutedEventHandler((sender, _) => Stop(sender)));
    }
    private static DependencyObject? Parent(DependencyObject node) => node is FrameworkContentElement content ? content.Parent : VisualTreeHelper.GetParent(node);
    private static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0) return;
        // Let the ComboBox route wheel input to its open menu, never to its selection.
        for (var node = e.OriginalSource as DependencyObject; node is not null; node = Parent(node))
            if (node is ComboBox) return;
        // Prefer the innermost scrollable field, then its page when the field reaches an edge.
        for (var node = e.OriginalSource as DependencyObject; node is not null; node = Parent(node))
        {
            if (node is not ScrollViewer scroll || scroll.ScrollableHeight <= 0) continue;
            if (e.Delta > 0 ? scroll.VerticalOffset <= .5 : scroll.VerticalOffset >= scroll.ScrollableHeight - .5) continue;
            Wheel(scroll, e.Delta); e.Handled = true; return;
        }
    }
    internal static void Wheel(ScrollViewer scroll, int delta)
    {
        if (scroll.GetValue(StateProperty) is not SmoothScrolling state)
        {
            state = new SmoothScrolling { viewer = scroll }; scroll.SetValue(StateProperty, state);
        }
        var lines = SystemParameters.WheelScrollLines;
        var distance = lines < 0 ? scroll.ViewportHeight * .85 : lines * 16d;
        var origin = state.active && Math.Sign(state.target - scroll.VerticalOffset) == -Math.Sign(delta) ? state.target : scroll.VerticalOffset;
        var destination = Math.Clamp(origin - delta / 120d * distance, 0, scroll.ScrollableHeight);
        state.Cancel();
        if (!SystemParameters.ClientAreaAnimation) { scroll.ScrollToVerticalOffset(destination); return; }
        state.start = scroll.VerticalOffset; state.target = destination; state.active = true;
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        animation.Completed += (_, _) => { if (state.active) { scroll.ScrollToVerticalOffset(state.target); state.Cancel(); } };
        state.BeginAnimation(ProgressProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
    private static void ProgressChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var state = (SmoothScrolling)sender;
        if (state.active && state.viewer is { } scroll) scroll.ScrollToVerticalOffset(state.start + (state.target - state.start) * (double)e.NewValue);
    }
    private void Cancel() { active = false; BeginAnimation(ProgressProperty, null); }
    private static void Stop(object sender) { if (sender is ScrollViewer scroll && scroll.GetValue(StateProperty) is SmoothScrolling state) state.Cancel(); }
}
