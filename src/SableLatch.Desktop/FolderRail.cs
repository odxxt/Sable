using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

// A four-folder viewport. Animation exists only while the user scrolls; no idle timer.
internal sealed class FolderRail : ScrollViewer
{
    private static readonly DependencyProperty AnimatedOffsetProperty = DependencyProperty.Register(nameof(AnimatedOffset), typeof(double), typeof(FolderRail), new PropertyMetadata(0d, (d, e) => ((FolderRail)d).ScrollToHorizontalOffset((double)e.NewValue)));
    private double AnimatedOffset { get => (double)GetValue(AnimatedOffsetProperty); set => SetValue(AnimatedOffsetProperty, value); }
    private bool panning, animating;
    private double panX, panOffset, destination;
    internal double SlotWidth => Math.Max(1, ActualWidth / 4);
    internal bool IsPanning => panning;
#if DEBUG
    internal Func<MouseEventArgs, bool>? PreviewPressed;
    internal Func<MouseEventArgs, double>? PreviewPosition;
#endif
    internal static bool ShouldBrowse(bool overflow, long heldMilliseconds, Vector movement) => overflow && heldMilliseconds < 350 && Math.Abs(movement.X) > Math.Abs(movement.Y) * 1.25;

    public FolderRail()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        CanContentScroll = false; Focusable = false; ClipToBounds = true;
        Height = 36; Background = Brushes.Transparent;
        SizeChanged += (_, _) => ResizeChips();
        Loaded += (_, _) => ResizeChips();
        ScrollChanged += (_, _) => UpdateEdgeMask();
        PreviewMouseWheel += (_, e) => { if (ScrollableWidth <= 0) return; ScrollBy(-e.Delta / 120d * SlotWidth); e.Handled = true; };
        Unloaded += (_, _) => { EndPan(false); BeginAnimation(AnimatedOffsetProperty, null); };
    }
    private void ResizeChips()
    {
        if (Content is not System.Windows.Documents.AdornerDecorator { Child: StackPanel panel }) return;
        foreach (var chip in panel.Children.OfType<Button>()) chip.Width = Math.Max(1, SlotWidth - 4);
    }
    private void UpdateEdgeMask()
    {
        if (ScrollableWidth <= .5) { OpacityMask = null; return; }
        var left = HorizontalOffset > .5; var right = HorizontalOffset < ScrollableWidth - .5;
        OpacityMask = new LinearGradientBrush(new GradientStopCollection
        {
            new(left ? Colors.Transparent : Colors.Black, 0), new(Colors.Black, .035),
            new(Colors.Black, .965), new(right ? Colors.Transparent : Colors.Black, 1)
        }, new Point(0, .5), new Point(1, .5));
    }
    internal void StartPan(double initialX)
    {
        StopAnimation(); panning = true; panX = initialX; panOffset = HorizontalOffset;
        Cursor = Cursors.ScrollWE; CaptureMouse();
    }
    internal void PanTo(double x) => ScrollToHorizontalOffset(Math.Clamp(panOffset + panX - x, 0, ScrollableWidth));
    internal void EndPan(bool snap)
    {
        if (!panning) return;
        panning = false; ClearValue(CursorProperty); if (IsMouseCaptured) ReleaseMouseCapture();
        if (snap) ScrollTo(Math.Round(HorizontalOffset / SlotWidth) * SlotWidth);
    }
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        var pressed = e.LeftButton == MouseButtonState.Pressed; var x = e.GetPosition(this).X;
#if DEBUG
        if (PreviewPressed is not null) pressed = PreviewPressed(e);
        if (PreviewPosition is not null) x = PreviewPosition(e);
#endif
        if (panning) { if (pressed) PanTo(x); else EndPan(true); e.Handled = true; }
        base.OnPreviewMouseMove(e);
    }
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (panning) { EndPan(true); e.Handled = true; }
        base.OnPreviewMouseLeftButtonUp(e);
    }
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        // Starting a pan takes capture from a child button. Its bubbling lost-capture
        // event must not cancel the rail that has just acquired capture.
        if (ReferenceEquals(e.OriginalSource, this) && !IsMouseCaptured) EndPan(false);
        base.OnLostMouseCapture(e);
    }
    internal void ScrollBy(double delta) => ScrollTo((animating ? destination : HorizontalOffset) + delta);
    internal void ScrollTo(double value)
    {
        var current = HorizontalOffset; StopAnimation(); destination = Math.Clamp(value, 0, ScrollableWidth);
        if (!SystemParameters.ClientAreaAnimation) { ScrollToHorizontalOffset(destination); return; }
        AnimatedOffset = destination; animating = true;
        var animation = new DoubleAnimation(current, destination, TimeSpan.FromMilliseconds(160))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        animation.Completed += (_, _) => animating = false;
        BeginAnimation(AnimatedOffsetProperty, animation);
    }
    private void StopAnimation() { animating = false; var current = HorizontalOffset; BeginAnimation(AnimatedOffsetProperty, null); AnimatedOffset = current; }
}
