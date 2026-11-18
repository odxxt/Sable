#if DEBUG
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Media.Imaging;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private async Task PreviewNavigation()
    {
        var output = Path.Combine(App.DataDirectory, "renders"); Directory.CreateDirectory(output); var checks = new List<string>();
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks.Add("PASS: " + name); File.WriteAllLines(Path.Combine(output, "checks.txt"), checks); }
        Button ButtonNamed(string name) { UpdateLayout(); return Descendants<Button>(Body).Single(b => b.Content as string == name || AutomationProperties.GetName(b) == name); }
        void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, button)); UpdateLayout(); }
        void Copy(string text) { previewSuggestionText = text; previewSuggestionSequence++; RefreshSuggestions(); UpdateLayout(); }
        async Task Render(string name)
        {
            await Task.Delay(150); UpdateLayout(); var bitmap = new RenderTargetBitmap(342, 411, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(output, name + ".png")); png.Save(stream);
        }
        try
        {
            vault.Create("Navigation-fictional-passphrase", Crypto.NewRecoveryKey()); session.Unlock();
            vault.Mutate(d => { d.Settings.Startup = false; d.Settings.FirstEntryTourPending = false; }); session.Mode = PanelMode.Window;
            ShowList(); UpdatePanelTopmost(); ApplyTaskbarVisibility(); UpdatePinButton();
            Copy("book"); Check(suggestionHost!.Children.Count == 0, "Ordinary words do not create DNS suggestions");
            Copy("alodat.me"); Check(Descendants<Button>(suggestionHost).Any(b => AutomationProperties.GetName(b) == "Suggested DNS lookup"), "Dotted domain creates a DNS suggestion");
            var card = (Border)suggestionHost.Children[0]; var size = card.RenderSize; var windows = Application.Current.Windows.Count;
            await Render("01-domain-suggestion"); Click(ButtonNamed("Dismiss network suggestion"));
            Check(card.RenderSize == size && Application.Current.Windows.Count == windows && ButtonNamed("Don't show again").IsVisible, "Dismiss choice stays inside the same fixed-size suggestion without a dialog");
            await Render("02-inline-dismiss"); Click(ButtonNamed("Only this copy"));
            Check(suggestionHost.Children.Count == 0 && vault.Document!.Settings.ClipboardSuggestions, "Dismissing this copy does not disable the feature");
            RefreshSuggestions(); Check(suggestionHost.Children.Count == 0, "The dismissed item stays hidden while clipboard sequence is unchanged");
            Copy("alodat.me"); Check(suggestionHost.Children.Count == 1, "Copying again permits a new suggestion");
            Click(ButtonNamed("Dismiss network suggestion"));
            Check(suggestionHost.Children.Count == 0 && vault.Document!.Settings.ClipboardDismissHintSeen, "Subsequent X clicks dismiss immediately after the one-time choice");
            // Exercise the alternative first-use decision with a fresh preference state.
            vault.Mutate(d => d.Settings.ClipboardDismissHintSeen = false); Copy("alodat.me");
            Click(ButtonNamed("Dismiss network suggestion")); Click(ButtonNamed("Don't show again"));
            Check(!vault.Document!.Settings.ClipboardSuggestions && suggestionHost.Children.Count == 0, "Don't show again saves the global off preference immediately");
            Copy("192.168.8.1"); Check(suggestionHost.Children.Count == 0, "Further copies remain quiet after permanent dismissal");
            ShowClipboardSettings(); UpdateLayout(); var control = Descendants<CheckBox>(Body).Single(c => c.Content is TextBlock t && t.Text == "Suggest network checks from clipboard");
            Check(control.IsChecked == false, "Settings reflects the persistent disable choice");
            vault.Mutate(d => d.Settings.ClipboardSuggestions = true); ShowList(); Copy("192.168.8.1");
            Check(ResizeMode == ResizeMode.CanMinimize && (Native.GetWindowLongPtr(handle, -16).ToInt64() & 0x20000) != 0, "Visible normal window exposes the native minimize capability without resizing");
            var width = Width; var height = Height;
            NavigationSend(handle, 0x0112, new IntPtr(0xF020), IntPtr.Zero); await Task.Delay(180);
            Check(WindowState == WindowState.Minimized && NavigationIconic(handle) && ShowInTaskbar, "Native taskbar minimize command keeps the window registered in the taskbar");
            RefreshSuggestions(); Check(suggestionHost.Children.Count == 0, "Minimized window does not analyze or display clipboard suggestions");
            NavigationSend(handle, 0x0112, new IntPtr(0xF120), IntPtr.Zero); await Task.Delay(180);
            Check(WindowState == WindowState.Normal && IsVisible && !Topmost && Width == width && Height == height, "Native restore command returns the fixed-size normal window without topmost");
            WindowState = WindowState.Minimized; OpenPanel(); Check(WindowState == WindowState.Normal, "Opening from tray also restores a minimized panel");
            var monitor = NavigationMonitor(handle, 2); ResetWindowPosition(); await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Check(NavigationMonitor(handle, 2) == monitor, "Double-click docking stays on the current monitor");
            Check(Width == width && Height == height, "Docking does not resize the panel");
            var second = System.Windows.Forms.Screen.AllScreens.Skip(1).FirstOrDefault();
            if (second is not null)
            {
                FixtureRaise(handle, IntPtr.Zero, second.WorkingArea.Left + 50, second.WorkingArea.Top + 50, 0, 0, 0x1 | 0x4 | 0x10); monitor = NavigationMonitor(handle, 2); ResetWindowPosition();
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Check(NavigationMonitor(handle, 2) == monitor, "Actual secondary-monitor docking does not jump to primary");
            }
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Completed " + checks.Count + " navigation checks");
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "preview-error.txt"), ex.ToString()); }
        finally { Application.Current.Shutdown(); }
    }
    [DllImport("user32", EntryPoint = "SendMessageW")] private static extern IntPtr NavigationSend(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32", EntryPoint = "IsIconic")] private static extern bool NavigationIconic(IntPtr window);
    [DllImport("user32", EntryPoint = "MonitorFromWindow")] private static extern IntPtr NavigationMonitor(IntPtr window, uint flags);
}
#endif
