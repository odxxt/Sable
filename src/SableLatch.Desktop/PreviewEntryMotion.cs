#if DEBUG
using System.Windows.Media.Imaging;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private async Task PreviewEntryMotion(Action<bool, string> check, string output)
    {
        var panel = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Rows = 4 };
        var icons = new List<EntryIdentity>(); var buttons = new List<Button>();
        foreach (var kind in new[] { "secret", "login", "shortcut", "terminal", "desktop", "keyboard", "run", "run-copy" })
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var icon = new EntryIdentity { Kind = kind, Interactive = true, Margin = new Thickness(0, 0, 10, 0) }; icons.Add(icon); line.Children.Add(icon);
            line.Children.Add(new TextBlock { Text = kind, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            var button = Button("", () => { }); button.Content = line; buttons.Add(button); panel.Children.Add(button);
        }
        SetBody(panel, "icon-motion-preview"); UpdateLayout(); await Task.Delay(50);
        void Hover(Button button, bool on) => button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = on ? Mouse.MouseEnterEvent : Mouse.MouseLeaveEvent });
        async Task Render(string name)
        {
            UpdateLayout(); var bitmap = new RenderTargetBitmap(342, 411, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, name + ".png")); png.Save(file); await Task.Yield();
        }
        check(icons.All(i => i.HasHoverOwner && i.MotionProgress == 0 && !i.HasAnimatedProperties), "All eight approved entry icons load at rest without an idle animation");
        await Render("07-approved-icons-rest");
        foreach (var button in buttons) Hover(button, true);
        await Task.Delay(100);
        if (SystemParameters.ClientAreaAnimation)
        {
            check(icons.All(i => i.MotionProgress > 0), "Hover advances each approved icon through its native motion");
            await Render("08-approved-icons-moving");
            var before = icons[0].MotionProgress; Hover(buttons[0], false);
            check(Math.Abs(icons[0].MotionProgress - before) < .02, $"Reversing hover keeps the current visual pose without a jump ({before:F3} to {icons[0].MotionProgress:F3})");
            await Task.Delay(80); check(icons[0].MotionProgress < before, "Interrupted hover moves smoothly back toward rest");
            Hover(buttons[0], true); await Task.Delay(600);
            check(icons.All(i => i.MotionProgress == 1 && !i.HasAnimatedProperties), "Held hover reaches its final pose and releases every animation clock");
            await Render("09-approved-icons-hover");
        }
        else check(icons.All(i => i.MotionProgress == 0 && !i.HasAnimatedProperties), "Windows reduced-motion preference prevents icon movement");
        foreach (var button in buttons) Hover(button, false);
        await Task.Delay(600);
        check(icons.All(i => i.MotionProgress == 0 && !i.HasAnimatedProperties), "Leaving entries restores all icons and leaves no idle clocks");
        Hover(buttons[0], true); await Task.Delay(60); buttons[0].Visibility = Visibility.Collapsed; UpdateLayout();
        check(icons[0].MotionProgress == 0 && !icons[0].HasAnimatedProperties, "Hidden entry feedback cancels its icon clock immediately");
        buttons[0].Visibility = Visibility.Visible; UpdateLayout();
        icons[0].Kind = "shortcut";
        check(icons[0].HasHoverOwner && icons[0].MotionProgress == 0, "Recycled entry identity resets its previous motion");
        var staticIcon = new EntryIdentity { Kind = "shortcut" }; var staticButton = Button("", () => { }); staticButton.Content = staticIcon;
        panel.Children.Add(staticButton); UpdateLayout(); Hover(staticButton, true); await Task.Delay(40);
        check(!staticIcon.HasHoverOwner && !staticIcon.HasAnimatedProperties && staticIcon.MotionProgress == 0, "Non-entry icon usage remains static");
        Hover(buttons[1], true); await Task.Delay(40); SetBody(new Grid(), "icon-motion-done"); UpdateLayout(); await Task.Delay(40);
        check(icons.All(i => !i.HasHoverOwner && i.MotionProgress == 0 && !i.HasAnimatedProperties), "Removing a view detaches hover owners and cancels all icon clocks");
        check(!Descendants<EntryIdentity>(Header).Any(i => i.Interactive), "Search, close, settings and window modes have no entry icon animation");
    }
}
#endif
