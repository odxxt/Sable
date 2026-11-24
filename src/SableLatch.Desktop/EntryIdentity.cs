using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

// Approved artwork rendered natively. Realized entry controls own their hover
// handlers; completed clocks are removed and unloading releases the owner.
public sealed class EntryIdentity : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(EntryIdentity), new FrameworkPropertyMetadata("note", FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public static readonly DependencyProperty InteractiveProperty = DependencyProperty.Register(nameof(Interactive), typeof(bool), typeof(EntryIdentity), new PropertyMetadata(false, Changed));
    private static readonly DependencyProperty ProgressProperty = DependencyProperty.Register("Progress", typeof(double), typeof(EntryIdentity), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public bool Interactive { get => (bool)GetValue(InteractiveProperty); set => SetValue(InteractiveProperty, value); }
    private double Progress => (double)GetValue(ProgressProperty);
    internal double MotionProgress => Progress;
    internal bool HasHoverOwner => owner is not null;
    private Button? owner;
    private int generation;
    private static readonly Pen Outline = MakePen(new SolidColorBrush(Color.FromRgb(192, 192, 199)));
    private static readonly Pen ActionOutline = MakePen(Brushes.White);
    private static Pen MakePen(Brush brush) { var pen = new Pen(brush, 1.25) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
    private sealed record Icon(Geometry? Back, Geometry? Moving, double X, double Y, double Angle, Point Pivot);
    private static Geometry Path(string value) => Geometry.Parse(value);
    private static Geometry Box(double x, double y, double width, double height) => new RectangleGeometry(new Rect(x, y, width, height), 2, 2);
    private static Geometry Circle(double x, double y, double radius) => new EllipseGeometry(new Point(x, y), radius, radius);
    private static Geometry Group(params Geometry[] parts) { var geometry = new GeometryGroup(); foreach (var part in parts) geometry.Children.Add(part); return geometry; }
    private static readonly IReadOnlyDictionary<string, Icon> Shapes = new Dictionary<string, Icon>
    {
        ["secret"] = Art(null, Group(Circle(10.5, 8, 5.5), Path("M14.5 12L25 22M20 17L23 14M22.5 19.5L25.5 16.5")), 18, 17.5, angle: -12),
        ["login"] = Art(Path("M5 22V20A10.5 8 0 0 1 26 20V22"), Circle(15.5, 7.5, 4.8), 17, 17, dy: -1.5),
        ["shortcut"] = Art(Path("M15 6H8.5Q4.5 6 4.5 10V19Q4.5 23 8.5 23H20Q24 23 24 19V15"), Path("M13 16Q14 9 27 2M20.5 2H27V8.5"), 18, 17.5, dx: .7, dy: -.4),
        ["terminal"] = Art(Box(4.5, 3.5, 22, 18), Path("M9 9L13 12.5L9 16M17 16H22"), 18, 15, dx: 1.8),
        ["desktop"] = Art(Group(Box(4.5, 4, 22, 14), Path("M15.5 18V22M10.5 22H20.5")), Path("M10 9L14 12L10 15M17 12H22"), 18, 17, dx: 1.4, dy: -.8),
        ["keyboard"] = Art(Group(Box(4.5, 5.5, 22, 14), Path("M9 10H9.1M13 10H13.1M17 10H17.1M21 10H21.1")), Path("M9 15H22"), 18, 13.5, dy: 1.6),
        ["network"] = Art(Path("M7 3V9M7 15V21M14 3V13M14 19V21M21 3V7M21 13V21"), Path("M4 9H10V15H4Z M11 13H17V19H11Z M18 7H24V13H18Z"), 17, 17, dy: -.7),
        ["run"] = Art(null, Path("M9 2.5L25 12.5L9 22.5Z"), 15, 17.5, dx: 1.9),
        ["run-copy"] = Art(Path("M6.5 5L20.5 13.5L6.5 22Z"), Path("M20 3H26V9M17 6H23V12H17Z"), 18, 17.5, dx: 1.6, dy: -1.4),
        ["lock"] = Art(Path("M4 9H16V19H4Z M6 9V6C6 0 14 0 14 6V9 M10 12V16"), null, 13, 17.5),
        ["mail"] = Art(Path("M1 3H19V17H1Z M1 4L10 11L19 4"), null, 18, 14.5),
        ["note"] = Art(Path("M4 3H16V19H4Z M7 1V5 M10 1V5 M13 1V5 M7 9H13 M7 12H13 M7 15H11"), null, 15, 17.5)
    };
    private static Icon Art(Geometry? back, Geometry? moving, double width, double height, double dx = 0, double dy = 0, double angle = 0)
    {
        var bounds = (back ?? moving!).Bounds; if (back is not null && moving is not null) bounds.Union(moving.Bounds);
        var x = width / bounds.Width; var y = height / bounds.Height;
        var matrix = new Matrix(x, 0, 0, y, (20 - width) / 2 - bounds.X * x, (20 - height) / 2 - bounds.Y * y);
        Geometry? Normalize(Geometry? original) { if (original is null) return null; var shape = original.Clone(); shape.Transform = new MatrixTransform(matrix); shape.Freeze(); return shape; }
        return new(Normalize(back), Normalize(moving), dx * x, dy * y, angle, matrix.Transform(new Point(15.5, 12.5)));
    }
    static EntryIdentity()
    {
        // Template dimensions must override the default, including smaller Run icons.
        WidthProperty.OverrideMetadata(typeof(EntryIdentity), new FrameworkPropertyMetadata(20d));
        HeightProperty.OverrideMetadata(typeof(EntryIdentity), new FrameworkPropertyMetadata(20d));
    }
    public EntryIdentity()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => { Connect(); Move(owner?.IsMouseOver == true); };
        Unloaded += (_, _) => { Disconnect(); Reset(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) Reset(); else Move(owner?.IsMouseOver == true); };
    }
    private static void Changed(DependencyObject value, DependencyPropertyChangedEventArgs e)
    { var icon = (EntryIdentity)value; icon.Reset(); if (icon.IsLoaded) { icon.Connect(); icon.Move(icon.owner?.IsMouseOver == true); } }
    private void Connect()
    {
        Disconnect(); if (!Interactive) return;
        DependencyObject? current = VisualTreeHelper.GetParent(this);
        while (current is not null && current is not Button) current = VisualTreeHelper.GetParent(current);
        owner = current as Button;
        if (owner is null) return;
        owner.MouseEnter += Enter; owner.MouseLeave += Leave;
    }
    private void Disconnect() { if (owner is null) return; owner.MouseEnter -= Enter; owner.MouseLeave -= Leave; owner = null; }
    private void Enter(object sender, MouseEventArgs e) => Move(true);
    private void Leave(object sender, MouseEventArgs e) => Move(false);
    private void Reset() { generation++; BeginAnimation(ProgressProperty, null); SetCurrentValue(ProgressProperty, 0d); }
    private void Move(bool hover)
    {
        if (!Interactive || !IsVisible || owner?.IsEnabled == false || !SystemParameters.ClientAreaAnimation || Shapes.GetValueOrDefault(Kind, Shapes["note"]).Moving is null) { Reset(); return; }
        var target = hover ? 1d : 0d; var from = Progress; var token = ++generation;
        if (Math.Abs(from - target) < .001) { BeginAnimation(ProgressProperty, null); SetCurrentValue(ProgressProperty, target); return; }
        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(460))
        { EasingFunction = new SplineEase(), FillBehavior = FillBehavior.Stop };
        animation.Completed += (_, _) => { if (generation != token) return; BeginAnimation(ProgressProperty, null); SetCurrentValue(ProgressProperty, target); };
        BeginAnimation(ProgressProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
    private sealed class SplineEase : EasingFunctionBase
    {
        private static readonly KeySpline Curve = MakeCurve();
        private static KeySpline MakeCurve() { var curve = new KeySpline(.22, 1, .36, 1); curve.Freeze(); return curve; }
        protected override double EaseInCore(double time) => Curve.GetSplineProgress(time);
        protected override Freezable CreateInstanceCore() => new SplineEase();
        public SplineEase() { EasingMode = EasingMode.EaseIn; }
    }
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing); var icon = Shapes.GetValueOrDefault(Kind, Shapes["note"]); var pen = Kind is "run" or "run-copy" ? ActionOutline : Outline;
        drawing.PushTransform(new ScaleTransform(Math.Min(RenderSize.Width, RenderSize.Height) / 20, Math.Min(RenderSize.Width, RenderSize.Height) / 20));
        if (icon.Back is not null) drawing.DrawGeometry(null, pen, icon.Back);
        if (icon.Moving is not null)
        {
            drawing.PushTransform(new TranslateTransform(icon.X * Progress, icon.Y * Progress));
            drawing.PushTransform(new RotateTransform(icon.Angle * Progress, icon.Pivot.X, icon.Pivot.Y));
            drawing.DrawGeometry(null, pen, icon.Moving); drawing.Pop(); drawing.Pop();
        }
        drawing.Pop();
    }
}
