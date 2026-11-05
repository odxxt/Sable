using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

// Transform-only press feedback on realized buttons; no layout animation or idle work.
public static class EntryPress
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(EntryPress), new PropertyMetadata(false, Changed));
    public static bool GetEnabled(DependencyObject value) => (bool)value.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject value, bool enabled) => value.SetValue(EnabledProperty, enabled);
    private static void Changed(DependencyObject value, DependencyPropertyChangedEventArgs e)
    {
        if (value is not Button button || e.NewValue is not true) return;
        var scale = new ScaleTransform(1, 1); button.RenderTransform = scale; button.RenderTransformOrigin = new Point(.5, .5);
        void Move(bool pressed)
        {
            if (!SystemParameters.ClientAreaAnimation) return;
            var animation = new DoubleAnimation(pressed ? .985 : 1, TimeSpan.FromMilliseconds(pressed ? 65 : 120)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation); scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
        button.PreviewMouseLeftButtonDown += (_, _) => Move(true);
        button.PreviewMouseLeftButtonUp += (_, _) => Move(false);
        button.LostMouseCapture += (_, _) => Move(false);
        button.MouseLeave += (_, _) => Move(false);
        button.PreviewKeyDown += (_, key) => { if (!key.IsRepeat && key.Key is Key.Enter or Key.Space) Move(true); };
        button.PreviewKeyUp += (_, key) => { if (key.Key is Key.Enter or Key.Space) Move(false); };
    }
}
