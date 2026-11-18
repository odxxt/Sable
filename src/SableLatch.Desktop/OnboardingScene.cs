using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    // Native clocks run only while the welcome illustration is visible.
    private FrameworkElement OnboardingScene(bool loop = false)
    {
        var scene = new Grid { Width = 228, Height = 100, Margin = new Thickness(0, 0, 0, 14), HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
        var rows = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        scene.Children.Add(rows);
        foreach (var (title, kind, index) in new[] { ("Your secrets, close.", "secret", 0), ("Your shortcuts, ready.", "shortcut", 1) })
        {
            var content = new Grid(); content.ColumnDefinitions.Add(new() { Width = new GridLength(28) }); content.ColumnDefinitions.Add(new()); content.ColumnDefinitions.Add(new() { Width = new GridLength(20) });
            content.Children.Add(new EntryIdentity { Kind = kind, Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center });
            var label = new TextBlock { Text = title, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(label, 1); content.Children.Add(label);
            var check = new TextBlock { Text = "\uE73E", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(153, 195, 164)), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(check, 2); content.Children.Add(check);
            var row = new Border { Child = content, Height = 40, Padding = new Thickness(12, 0, 10, 0), Background = (Brush)FindResource("ControlSurface"), CornerRadius = new CornerRadius(9), Margin = new Thickness(index == 0 ? 0 : 12, 0, index == 0 ? 12 : 0, 8) };
            rows.Children.Add(row);
            var motion = new Storyboard { RepeatBehavior = loop ? RepeatBehavior.Forever : new RepeatBehavior(1), Duration = TimeSpan.FromMilliseconds(loop ? 4800 : 1100) };
            var move = new TranslateTransform(); row.RenderTransform = move;
            var scale = new ScaleTransform(); check.RenderTransform = scale; check.RenderTransformOrigin = new Point(.5, .5);
            void Track(DependencyObject target, DependencyProperty property, params (double milliseconds, double value)[] frames)
            {
                var animation = new DoubleAnimationUsingKeyFrames();
                foreach (var (time, value) in frames) animation.KeyFrames.Add(new EasingDoubleKeyFrame(value, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(time)), new CubicEase { EasingMode = EasingMode.EaseOut }));
                Storyboard.SetTarget(animation, target); Storyboard.SetTargetProperty(animation, new PropertyPath(property)); motion.Children.Add(animation);
            }
            var delay = index * 160;
            Track(row, OpacityProperty, (0, 0), (delay, 0), (delay + 380, 1), (3900, 1), (4350, loop ? 0 : 1), (4800, loop ? 0 : 1));
            Track(move, TranslateTransform.XProperty, (0, index == 0 ? -18 : 18), (delay + 600, 0), (4800, 0));
            Track(check, OpacityProperty, (0, 0), (500 + delay, 0), (720 + delay, 1), (3900, 1), (4300, loop ? 0 : 1));
            Track(scale, ScaleTransform.ScaleXProperty, (0, .75), (500 + delay, .75), (740 + delay, 1.08), (900 + delay, 1));
            Track(scale, ScaleTransform.ScaleYProperty, (0, .75), (500 + delay, .75), (740 + delay, 1.08), (900 + delay, 1));
            void Refresh() { motion.Remove(scene); if (scene.IsVisible && SystemParameters.ClientAreaAnimation) motion.Begin(scene, true); }
            scene.Loaded += (_, _) => Refresh(); scene.IsVisibleChanged += (_, _) => Refresh(); scene.Unloaded += (_, _) => motion.Remove(scene);
        }
        return scene;
    }
}
