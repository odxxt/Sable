#if DEBUG
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Media.Imaging;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private async Task PreviewNetworkTyping()
    {
        var output = Path.Combine(App.DataDirectory, "renders"); Directory.CreateDirectory(output); var checks = new List<string>();
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks.Add("PASS: " + message); File.WriteAllLines(Path.Combine(output, "checks.txt"), checks); }
        try
        {
            vault.Create("Network-only-fictional-passphrase", Crypto.NewRecoveryKey()); session.Unlock();
            vault.Mutate(d => { d.Settings.Startup = false; d.Settings.FirstEntryTourPending = false; });
            session.Mode = PanelMode.AlwaysOnTop; ShowList(); int starts = 0; string command = "";
            previewActionLaunch = start => { starts++; command = Encoding.Unicode.GetString(Convert.FromBase64String(start.ArgumentList.Last())); };
            foreach (var (text, label, script) in new[] { ("the ip is 192.168.8.1", "Ping", "ping.exe"), ("the ip and port is 192.168.8.1:53", "Test port", "-Port 53"), ("the ip is 192.168.8.1 and the port is 53", "Test port", "-Port 53") })
            {
                previewSuggestionText = text; RefreshSuggestions(); UpdateLayout();
                var button = Descendants<Button>(suggestionHost!).Single(b => b.Tag as string == "network-run"); Check(AutomationProperties.GetName(button) == "Suggested " + label && vault.Document!.Items.Count == 0, label + " suggested from prose with no saved entries");
                var before = starts; button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, button)); await Task.Delay(20);
                Check(starts == before + 1 && command.Contains(script) && command.Contains("192.168.8.1") && !command.Contains("the ip"), "Only the normalized diagnostic target reaches the trusted launcher");
            }
            previewSuggestionText = "192.168.8.1 ; Remove-Item C:\\"; RefreshSuggestions(); var safe = Descendants<Button>(suggestionHost!).Single(b => b.Tag as string == "network-run"); safe.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, safe));
            Check(!command.Contains("Remove-Item"), "Other clipboard text is never inserted into the command");
            previewSuggestionText = new string(' ', 513) + "192.168.8.1"; RefreshSuggestions(); Check(suggestionHost!.Children.Count == 0, "Oversized clipboard text creates no suggestion");
            previewSuggestionText = "192.168.8.1"; vault.Mutate(d => d.Settings.ClipboardSuggestions = false); RefreshSuggestions(); Check(suggestionHost.Children.Count == 0, "Settings switch disables built-in diagnostics");
            vault.Mutate(d => d.Settings.ClipboardSuggestions = true); previewSuggestionText = "server IP 192.168.8.1 and port 8181"; RefreshSuggestions(); await Task.Delay(150); UpdateLayout();
            var image = new RenderTargetBitmap(342, 411, 96, 96, PixelFormats.Pbgra32); image.Render(this); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using (var file = File.Create(Path.Combine(output, "01-built-in-suggestion.png"))) png.Save(file);
            var stale = Descendants<Button>(suggestionHost).Single(b => b.Tag as string == "network-run"); var count = starts; ForceLock("Fixture lock"); stale.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, stale));
            Check(starts == count, "A stale suggestion cannot launch after the vault locks");
            var promptChecked = false;
            previewTypePromptShown = dialog =>
            {
                Check(dialog.Left > SystemParameters.WorkArea.Left + SystemParameters.WorkArea.Width / 2, "Type prompt appears on the right side rather than centered");
                promptChecked = true;
                var bitmap = new RenderTargetBitmap((int)dialog.ActualWidth, (int)dialog.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(dialog);
                var frame = new PngBitmapEncoder(); frame.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(Path.Combine(output, "02-type-prompt.png"))) frame.Save(file);
                dialog.DialogResult = true;
            };
            ConfirmType(false, 0, false, "Fictional Type positioning check");
            previewTypePromptShown = null; Check(promptChecked, "Type prompt placement is verified after its layout is rendered");
            Hide(); await PreviewChromeTyping(Check);
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Completed " + checks.Count + " network/browser checks");
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "preview-error.txt"), ex.ToString()); }
        finally { Application.Current.Shutdown(); }
    }
    private async Task PreviewChromeTyping(Action<bool, string> check)
    {
        var chrome = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google/Chrome/Application/chrome.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google/Chrome/Application/chrome.exe") }.FirstOrDefault(File.Exists);
        if (chrome is null) throw new InvalidOperationException("Chrome is not available for the browser typing check.");
        var page = Path.Combine(App.DataDirectory, "typing-fixture.html");
        var token = "SableTyping" + Guid.NewGuid().ToString("N");
        File.WriteAllText(page, "<!doctype html><meta charset=utf-8><title>" + token + "Ready</title><textarea autofocus id=t style='width:90vw;height:80vh'></textarea><script>let packets=0;document.addEventListener('keydown',e=>{if(e.key==='Unidentified'||e.code==='')packets++;});t.addEventListener('input',()=>document.title='" + token + "Typed='+encodeURIComponent(t.value)+';packets='+packets);t.focus();</script>");
        var start = new ProcessStartInfo(chrome) { UseShellExecute = false };
        foreach (var arg in new[] { "--user-data-dir=" + Path.Combine(App.DataDirectory, "chrome-fixture"), "--no-first-run", "--no-default-browser-check", "--disable-extensions", "--disable-background-networking", "--disable-sync", "--app=" + new Uri(page).AbsoluteUri, "--window-position=60,60", "--window-size=500,350" }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!; IntPtr browser = IntPtr.Zero;
        try
        {
            for (var attempt = 0; attempt < 100 && browser == IntPtr.Zero; attempt++)
            {
                EnumWindows((window, _) => { var title = FixtureTitle(window); if (title.StartsWith(token + "Ready", StringComparison.Ordinal)) { browser = window; return false; } return true; }, IntPtr.Zero);
                if (browser == IntPtr.Zero) await Task.Delay(100);
            }
            check(browser != IntPtr.Zero, "Chrome opens only the isolated local typing test page");
            FixtureRaise(browser, new IntPtr(-1), 0, 0, 0, 0, 0x43); FixtureForeground(browser); await Task.Delay(100);
            FixtureRect(browser, out var bounds); GetCursorPos(out var old); SetCursorPos(bounds.Left + 100, bounds.Top + 100);
            FixtureMouse(2, 0, 0, 0, UIntPtr.Zero); FixtureMouse(4, 0, 0, 0, UIntPtr.Zero); await Task.Delay(150); SetCursorPos(old.X, old.Y);
            check(NativeTyping.GetForegroundWindow() == browser, "Isolated Chrome test window has focus");
            const string sample = "Aa19+{}:@,./"; var events = new List<(int Key, int Scan)>();
            using (var operation = new NativeTyping())
            {
                operation.ObserveKeyboard = (key, scan, _) => events.Add((key, scan));
                await operation.TypeTo(browser, sample, new() { StartDelayMs = 100, CharacterDelayMs = 25, InputMode = "auto" });
            }
            await Task.Delay(250); var result = FixtureTitle(browser);
            check(result.StartsWith(token + "Typed=" + Uri.EscapeDataString(sample) + ";packets=0", StringComparison.Ordinal), "Chrome receives exact punctuation as real keyboard events, without Unicode packet events");
            check(events.Count > 0 && events.All(e => e.Key != 0xE7 && e.Key != 0 && e.Scan > 0), "Remote-control-style hook receives nonzero virtual keys and scan codes");
        }
        finally
        {
            if (browser != IntPtr.Zero) FixturePost(browser, 0x0010, IntPtr.Zero, IntPtr.Zero);
            if (!process.HasExited && !process.WaitForExit(1500)) process.Kill(true);
        }
    }
    private static string FixtureTitle(IntPtr window) { var text = new StringBuilder(1024); FixtureWindowText(window, text, text.Capacity); return text.ToString(); }
    private delegate bool FixtureEnumCallback(IntPtr window, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct FixtureBounds { public int Left, Top, Right, Bottom; }
    [DllImport("user32", EntryPoint = "GetWindowRect")] private static extern bool FixtureRect(IntPtr window, out FixtureBounds bounds);
    [DllImport("user32", EntryPoint = "SetWindowPos")] private static extern bool FixtureRaise(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32")] private static extern bool EnumWindows(FixtureEnumCallback callback, IntPtr data);
    [DllImport("user32", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)] private static extern int FixtureWindowText(IntPtr window, StringBuilder text, int length);
    [DllImport("user32", EntryPoint = "SetForegroundWindow")] private static extern bool FixtureForeground(IntPtr window);
    [DllImport("user32", EntryPoint = "PostMessageW")] private static extern bool FixturePost(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
}
#endif
