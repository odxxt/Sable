using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace SableLatch.Desktop;

// Capture exclusion is an OS mitigation, never a guarantee against every recorder.
public static class CaptureProtection
{
    private static bool registered;
    public static bool HasFailure { get; private set; }
    public static uint RequestedAffinity => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) ? 0x11u : 1u;
#if DEBUG
    internal static bool PreviewOpaque { get; set; }
#endif
    public static bool AllowTransparentWindows => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
#if DEBUG
        && !PreviewOpaque
#endif
        ;
    public static bool HasFallback { get; private set; }
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(CaptureProtection), new PropertyMetadata(false, Changed));
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not Popup popup) return;
        if ((bool)args.NewValue) { popup.AllowsTransparency = AllowTransparentWindows; popup.Opened += PopupOpened; } else popup.Opened -= PopupOpened;
    }
    private static void PopupOpened(object? sender, EventArgs args)
    {
        if (sender is Popup { Child: Visual child }) ProtectVisual(child);
    }
    public static void Register()
    {
        if (registered) return; registered = true;
        // WPF creates separate native windows for these surfaces. Protect each on opening;
        // do not retain popup handles or add a polling loop/background process.
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler((sender, _) => ProtectVisual((Visual)sender)), true);
        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, new RoutedEventHandler((sender, _) => ProtectVisual((Visual)sender)), true);
    }
    private static void ProtectVisual(Visual visual) { if (PresentationSource.FromVisual(visual) is HwndSource source) Protect(source.Handle); }
    public static bool Protect(IntPtr handle)
    {
        var ok = Apply(handle, RequestedAffinity);
        if (!ok && RequestedAffinity != 1 && Apply(handle, 1)) { HasFallback = true; ok = true; }
        if (!ok) HasFailure = true;
        return ok;
    }
    private static bool Apply(IntPtr handle, uint mode) => handle != IntPtr.Zero
        && Native.SetWindowDisplayAffinity(handle, mode) && Native.GetWindowDisplayAffinity(handle, out var actual) && actual == mode;
    public static void ConfigureWindow(Window window)
    {
        if (AllowTransparentWindows) return;
        window.AllowsTransparency = false;
        window.Background = new SolidColorBrush(Color.FromRgb(16, 16, 18));
        System.Windows.Shell.WindowChrome.SetWindowChrome(window, new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 0, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(20), UseAeroCaptionButtons = false
        });
    }
}
