#if DEBUG
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private async Task PreviewType()
    {
        var output = Path.Combine(App.DataDirectory, "renders"); Directory.CreateDirectory(output);
        var checks = new List<string>(); Window? target = null; Window? other = null;
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks.Add("PASS: " + name); File.WriteAllLines(Path.Combine(output, "checks.txt"), checks); }
        Button Btn(string name) => Descendants<Button>(Body).Single(b => b.Content as string == name || AutomationProperties.GetName(b) == name);
        void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, button)); UpdateLayout(); }
        async Task Render(string name)
        {
            await Task.Delay(180); UpdateLayout(); var bitmap = new RenderTargetBitmap(342, 411, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, name + ".png")); png.Save(file);
        }
        try
        {
            if (vault.Exists) throw new InvalidOperationException("Fresh fixture required.");
            vault.Create("Type-fixture-only-passphrase", Crypto.NewRecoveryKey()); session.Unlock();
            vault.Mutate(d => { d.Settings.Startup = false; d.Settings.FirstEntryTourPending = false; });
            foreach (var resource in new[] { "putty.zip", "rdp.zip" }) modules.Install(ModulePackages.Read(ModuleTrust.Resource(resource), ModuleTrust.Verify)); ReloadModules();
            ShowEntryStarters(); UpdateLayout(); Check(!Descendants<Button>(Body).Any(b => b.Content as string == "Extras" || AutomationProperties.GetName(b) == "Extras"), "Extras hidden without an extra module");
            Check(Descendants<ScrollViewer>(Body).First().ScrollableHeight == 0, "Three starter categories fit without scrolling");
            var package = ModulePackages.Read(ModuleTrust.Resource("type.zip"), ModuleTrust.Verify); modules.Install(package); ReloadModules();
            Check(package.Official && package.Manifest.Id == "sable.type", "Type ZIP verifies against the existing Sable publisher key");
            ShowEntryStarters(); await Render("01-four-starters");
            Check(Descendants<Button>(Body).Count(b => AutomationProperties.GetName(b) is "Secret or login" or "Shortcut" or "Connection" or "Extras") == 4, "Installed modules produce exactly four root categories");
            Check(Descendants<ScrollViewer>(Body).First().ScrollableHeight == 0, "All four starters fit the fixed window without scrolling");
            Click(Btn("Extras")); Click(Btn("Type clipboard")); await Render("02-type-entry");
            Check(Descendants<CheckBox>(Body).Any(c => c.IsChecked == true && c.Content is TextBlock t && t.Text == "Use module defaults"), "New Type entries inherit global defaults");
            Click(pageSubmit!); var entry = vault.Document!.Items.Last();
            Check(entry.ContentType == "module.sable.type" && entry.Field("custom") == "false", "Type entry saves without a stored secret or clipboard snapshot");
            ShowTypeModuleSettings(package); await Render("03-type-settings");
            var start = Descendants<TextBox>(Body).Single(b => AutomationProperties.GetName(b) == "Start delay · milliseconds"); start.Text = "750";
            Click(pageSubmit!); Check(TypeSettings(package).Options.StartDelayMs == 750, "Global Type timing is saved outside entry overrides");
            ShowEditor(entry); UpdateLayout(); var defaults = Descendants<CheckBox>(Body).Single(c => c.Content is TextBlock t && t.Text == "Use module defaults"); defaults.IsChecked = false; UpdateLayout();
            Descendants<TextBox>(Body).Single(b => AutomationProperties.GetName(b) == "Start delay · milliseconds").Text = "600";
            Click(pageSubmit!); entry = vault.Document.Items.Single(i => i.Id == entry.Id);
            Check(TypeClipboardOptions.From(entry, TypeSettings(package).Options).StartDelayMs == 600 && TypeSettings(package).Options.StartDelayMs == 750, "Entry timing overrides do not modify module defaults");
            ShowList(); await Render("04-type-row");
            ForceLock("Fixture lock"); Check(!vault.IsUnlocked && TrayTypePackage?.Manifest.Id == "sable.type", "Type tray capability remains available with the vault locked");
            LoadTypeSettings(); Check(TrayTypePackage is not null && TypeSettings(package).Options.StartDelayMs == 750, "Locked tray defaults survive a local settings reload");
            var oldSettings = TypeSettings(package); oldSettings.TypingVersion = 0; oldSettings.Options.InputMode = "unicode"; typeSettings[package.Manifest.Id] = oldSettings; SaveTypeSettings(); LoadTypeSettings();
            Check(TypeSettings(package).Options.InputMode == "auto" && TypeSettings(package).Options.StartDelayMs == 750, "Existing Unicode defaults migrate to automatic input without changing saved timing");
            oldSettings = TypeSettings(package); oldSettings.TypingVersion = 2; oldSettings.Options.InputMode = "unicode"; SaveTypeSettings(); LoadTypeSettings();
            Check(TypeSettings(package).Options.InputMode == "unicode", "A new explicit Unicode preference remains available");
            Hide();
            var input = new TextBox { AcceptsReturn = true, AcceptsTab = true, Margin = new Thickness(20), FontSize = 16 };
            target = new Window { Title = "Sable fictional typing fixture", Width = 430, Height = 230, Content = input, Topmost = true, ShowInTaskbar = false, Left = 60, Top = 60 };
            target.Show(); target.Activate(); input.Focus(); await Task.Delay(250);
            var handle = new WindowInteropHelper(target).Handle;
            using (var operation = new NativeTyping())
            {
                var selection = operation.SelectTarget(); var point = input.PointToScreen(new Point(20, 20));
                GetCursorPos(out var old); SetCursorPos((int)point.X, (int)point.Y); FixtureMouse(2, 0, 0, 0, UIntPtr.Zero); FixtureMouse(4, 0, 0, 0, UIntPtr.Zero);
                var chosen = await selection.WaitAsync(TimeSpan.FromSeconds(5)); SetCursorPos(old.X, old.Y);
                await Task.Delay(80); Check(NativeTyping.GetForegroundWindow() == handle, "Disposable destination window has foreground focus after the real target click");
                Check(chosen == handle, "Real crosshair click selects the disposable destination window");
                await operation.TypeTo(chosen, TypeClipboardText.Prepare("A{}+é😀\r\nline\tend\r\n"), new() { StartDelayMs = 80, CharacterDelayMs = 2, InputMode = "unicode" });
            }
            await Task.Delay(120); Check(input.Text.Replace("\r\n", "\n") == "A{}+é😀\nline\tend", "Native Unicode typing preserves literal symbols, Unicode, internal Enter/Tab and omits trailing Enter");
            input.Clear(); input.Focus();
            using (var operation = new NativeTyping()) await operation.TypeTo(handle, "Az19{}", new() { StartDelayMs = 80, CharacterDelayMs = 4, InputMode = "keyboard" });
            await Task.Delay(80); Check(input.Text == "Az19{}", "Native keyboard compatibility types through the destination layout");
            input.Clear(); input.Focus();
            using (var operation = new NativeTyping())
            {
                var task = operation.TypeTo(handle, "never typed", new() { StartDelayMs = 500 }); await Task.Delay(150);
                FixtureKey(27, 0, 0, UIntPtr.Zero); FixtureKey(27, 0, 2, UIntPtr.Zero);
                var cancelled = false; try { await task; } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && input.Text.Length == 0, "Real Escape cancels the delay before any character is sent");
            }
            input.Focus();
            using (var operation = new NativeTyping())
            {
                var task = operation.TypeTo(handle, new string('x', 60), new() { StartDelayMs = 0, CharacterDelayMs = 20 }); await Task.Delay(220);
                FixtureKey(27, 0, 0, UIntPtr.Zero); FixtureKey(27, 0, 2, UIntPtr.Zero);
                var cancelled = false; try { await task; } catch (OperationCanceledException) { cancelled = true; }
                var count = input.Text.Length; await Task.Delay(100);
                Check(cancelled && count is > 0 and < 60 && input.Text.Length == count, "Escape during active typing stops further characters without undoing earlier input");
            }
            input.Clear(); input.Focus();
            using (var operation = new NativeTyping())
            {
                var task = operation.TypeTo(handle, "must stop", new() { StartDelayMs = 500 }); await Task.Delay(150);
                var elsewhere = new TextBox(); other = new Window { Content = elsewhere, Width = 250, Height = 120, Topmost = true, ShowInTaskbar = false, Title = "Sable cancellation fixture" }; other.Show(); other.Activate(); elsewhere.Focus();
                var cancelled = false; try { await task; } catch (OperationCanceledException) { cancelled = true; } catch (InvalidOperationException) { cancelled = true; }
                Check(cancelled && input.Text.Length == 0 && elsewhere.Text.Length == 0, "Changing destination windows cancels without typing into the new window");
            }
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Completed " + checks.Count + " Type checks");
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "preview-error.txt"), ex.ToString()); }
        finally { target?.Close(); other?.Close(); Application.Current.Shutdown(); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct FixturePoint { public int X, Y; }
    [DllImport("user32")] private static extern bool GetCursorPos(out FixturePoint point);
    [DllImport("user32")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32", EntryPoint = "mouse_event")] private static extern void FixtureMouse(uint flags, uint x, uint y, uint data, UIntPtr extra);
    [DllImport("user32", EntryPoint = "keybd_event")] private static extern void FixtureKey(byte key, byte scan, uint flags, UIntPtr extra);
}
#endif
