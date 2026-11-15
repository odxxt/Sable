#if DEBUG
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Media.Imaging;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private async Task PreviewRefine()
    {
        var output = Path.Combine(App.DataDirectory, "renders"); Directory.CreateDirectory(output); var results = new List<string>();
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); results.Add("PASS: " + name); File.WriteAllLines(Path.Combine(output, "checks.txt"), results); }
        Button Btn(string text) { UpdateLayout(); return Descendants<Button>(Body).Single(b => b.Content as string == text || AutomationProperties.GetName(b) == text); }
        void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, b));
        ComboBox Choice(string text) { UpdateLayout(); return Descendants<ComboBox>(Body).Single(c => AutomationProperties.GetName(c) == text); }
        async Task Render(string name, FrameworkElement? element = null)
        {
            await Task.Delay(220); UpdateLayout(); element ??= this;
            var bitmap = new RenderTargetBitmap(Math.Max(1, (int)element.ActualWidth), Math.Max(1, (int)element.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
        }
        try
        {
            if (vault.Exists) throw new InvalidOperationException("Use a fresh disposable fixture.");
            ShowWelcome(); UpdateLayout();
            var brand = Descendants<TextBlock>(Body).Single(t => t.Text == "Sable");
            Check(brand.FontSize == 42 && BrandTitle.Visibility == Visibility.Collapsed, "Welcome leads with one large Sable wordmark");
            await Render("01-welcome");
            var animated = Descendants<Border>(Body).Where(b => b.HasAnimatedProperties).ToArray();
            await Task.Delay(5100);
            Check(animated.Length == 2 && animated.All(b => b.HasAnimatedProperties), "Welcome illustration continues across its first loop");
            await Render("02-welcome-loop");
            ShowSetupRecovery(new()); UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); await Task.Delay(50);
            Check(animated.All(b => !b.HasAnimatedProperties), "Leaving welcome removes its animation clocks");
            Click(Btn("Reveal key")); var key = Descendants<TextBox>(Body).Single(); var keySize = key.RenderSize;
            Mouse.Capture(key);
            key.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseUpEvent });
            await Task.Delay(200); UpdateLayout();
            var copied = Descendants<TextBlock>(Body).Single(t => t.Text == "✓ Copied");
            Check(!key.IsMouseCaptureWithin && Mouse.Captured is null, "Recovery copy releases selection capture immediately");
            Check(ReferenceEquals(copied.Parent, key.Parent) && copied.Opacity > .9 && key.Opacity < .15 && key.RenderSize == keySize, "Copied feedback overlays the same key box with stable geometry");
            await Render("03-recovery-copied");
            var saved = Descendants<CheckBox>(Body).Single(); saved.IsChecked = true; Click(pageSubmit!); UpdateLayout();
            Check(screen == "setup" && Descendants<PasswordBox>(Body).Count() == 2, "Next remains clickable while copy feedback is active");
            ShowSetupRecovery(new()); Click(Btn("Reveal key")); key = Descendants<TextBox>(Body).Single();
            key.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseUpEvent }); await Task.Delay(1150);
            Check(key.Opacity > .99 && Descendants<TextBlock>(Body).Single(t => t.Text == "✓ Copied").Opacity < .01, "Key fades back after the confirmation");
            vault.Create("Preview-only-refined-passphrase", Crypto.NewRecoveryKey()); session.Unlock(); session.IsPinned = true; Topmost = true;
            vault.Mutate(d => { d.Settings.Startup = false; d.Settings.FirstEntryTourPending = false; });
            bool HasGuide() { UpdateLayout(); return Descendants<TextBlock>(Body).Any(t => t.Text is "Finish setting up" or "Continue setup"); }
            ShowSettings(); Check(!HasGuide(), "Fresh settings hide the setup checklist");
            vault.Mutate(d => d.Items.AddRange([new() { Label = "One" }, new() { Label = "Two" }])); ShowSettings(); Check(!HasGuide(), "Two entries do not show the setup checklist");
            vault.Mutate(d => d.Items.Add(new() { Label = "Three" })); ShowSettings(); Check(HasGuide(), "Three saved entries reveal the setup checklist");
            vault.Mutate(d => MarkGuideStep(d, "backups")); ShowSetupGuide(); UpdateLayout();
            Check(Descendants<Button>(Body).Single(b => b.Content is StackPanel p && p.Children.OfType<TextBlock>().Any(t => t.Text.Contains("Protect a backup"))).Opacity < .6, "Completed setup tasks are visually faded");
            await Render("04-setup-guide");
            ShowGuideBackup(); UpdateLayout(); var path = Descendants<TextBox>(Body).Single(t => AutomationProperties.GetName(t) == "Backup folder"); var browse = Btn("Browse…");
            Check(ReferenceEquals(path.Parent, browse.Parent) && Math.Abs(path.TranslatePoint(new(), Body).Y - browse.TranslatePoint(new(), Body).Y) < 1 && browse.TranslatePoint(new(), Body).X > path.TranslatePoint(new(), Body).X, "Folder browse sits beside its path field");
            await Render("05-backup-folder");
            ShowModules(); UpdateLayout(); Check(!Descendants<CheckBox>(Body).Any(c => c.IsVisible), "Module page keeps standard type controls collapsed");
            Check(Btn("PuTTY SSH add").IsVisible && Btn("Remote Desktop add").IsVisible, "Available connections have clear Add rows"); await Render("06-modules-available");
            var putty = ModulePackages.Read(ModuleTrust.Resource("putty.zip"), ModuleTrust.Verify); var rdp = ModulePackages.Read(ModuleTrust.Resource("rdp.zip"), ModuleTrust.Verify);
            ReviewModule(putty); Click(Btn("Install")); UpdateLayout();
            Check(Btn("PuTTY SSH installed").IsVisible && !Descendants<Button>(Body).Any(b => AutomationProperties.GetName(b) == "PuTTY SSH add"), "Installing SSH replaces Add with Installed");
            modules.Install(rdp); ReloadModules(); ShowModules(); await Render("07-modules-installed");
            ShowEditor(); UpdateLayout(); Check(!Choice("When I click the entry").IsVisible && !Btn("Entry script fields").IsVisible, "Plain Secret hides action-only controls");
            Descendants<TextBox>(Body).Single(t => AutomationProperties.GetName(t) == "Label").Text = "Refined secret";
            Descendants<MaskedSecretBox>(Body).Single().Password = "fictional-refinement-value";
            Click(pageSubmit!); UpdateLayout();
            Check(vault.Document!.Items.Any(i => i.Label == "Refined secret" && i.ClickBehavior == "copy"), "Secret saves with normal copying while action controls are hidden");
            ShowEditor(); UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var choose = Btn("Choose entry template"); Click(choose); await Dispatcher.InvokeAsync(() => { });
            Check(choose.ContextMenu.IsOpen && choose.ContextMenu.Items.OfType<MenuItem>().Any(m => m.Header as string == "Shortcut"), "Template picker uses the Shortcut name (open=" + choose.ContextMenu.IsOpen + "; labels=" + string.Join(",", choose.ContextMenu.Items.OfType<MenuItem>().Select(m => m.Header)) + ")");
            Click(choose); Check(!choose.ContextMenu.IsOpen, "Second template-trigger click closes the menu");
            Click(choose); Check(choose.ContextMenu.IsOpen, "Third template-trigger click opens the menu again"); choose.ContextMenu.IsOpen = false;
            ShowEditor(selectedType: "sable.shortcut"); UpdateLayout(); Check(!Choice("When I click the entry").IsVisible && !Btn("Entry script fields").IsVisible, "Blank Shortcut waits for an action before exposing extra controls");
            Choice("Action").SelectedItem = "PowerShell"; UpdateLayout();
            Check(Choice("When I click the entry").IsVisible && Btn("Script fields · optional").IsVisible, "PowerShell exposes its fields and click behavior");
            Choice("Action").SelectedItem = "CMD"; UpdateLayout(); Check(!Btn("Entry script fields").IsVisible && Choice("When I click the entry").IsVisible, "CMD keeps behavior controls but hides PowerShell fields");
            ShowEditor(selectedType: rdp.Manifest.ContentType); UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Check(!Descendants<TextBox>(Body).Single(t => AutomationProperties.GetName(t) == "Port").IsVisible, "Connections start with options closed independently of Shortcut action state");
            var options = Btn("Connection options"); var name = Descendants<TextBox>(Body).Single(t => AutomationProperties.GetName(t) == "Name"); var width = name.ActualWidth;
            Click(options); UpdateLayout(); var port = Descendants<TextBox>(Body).Single(t => AutomationProperties.GetName(t) == "Port"); options.BringIntoView(); await Task.Delay(180);
            await Render("08-connection-options");
            Check(port.IsVisible && FindParent<Border>(port) is { Background: SolidColorBrush background } && background.Color.A == 13 && Math.Abs(port.ActualWidth - name.ActualWidth) < 1, "Connection options share a subtle rounded section without shrinking the fields (visible=" + port.IsVisible + ", port=" + port.ActualWidth + ", name=" + name.ActualWidth + ", before=" + width + ", color=" + FindParent<Border>(port)?.Background + ")");
            Click(options); Check(!port.IsVisible, "Connection section collapses from the same header");
            OpenAbout(this, new RoutedEventArgs()); UpdateLayout(); Check(Descendants<TextBlock>(Body).Any(t => t.Text == "Version 0.9.1 · Preview"), "About reads the current assembly version"); await Render("09-about");
            var outsideCount = vault.Document!.Items.Count;
            var folderA = new VaultFolder { Name = "Delete fixture" }; var child = new VaultFolder { Name = "Nested", ParentId = folderA.Id };
            vault.Mutate(d => { d.Folders.AddRange([folderA, child]); d.Items.Add(new() { Label = "Nested fixture", FolderId = child.Id }); }); folder = null; ShowList(); UpdateLayout();
            var folderButton = folderChips!.Children.OfType<Button>().Single(b => b.Tag is Guid id && id == folderA.Id);
            var remove = folderButton.ContextMenu.Items.OfType<MenuItem>().Single(m => m.Header as string == "Delete folder…"); remove.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); UpdateLayout();
            Check(Descendants<TextBox>(Body).Any(t => t.Text == "Delete fixture") && vault.Document!.Folders.Count == 2, "Folder Delete opens its editor and preserves contents");
            Click(Btn("Delete folder")); UpdateLayout();
            Check(vault.Document!.Folders.Count == 2 && Descendants<TextBlock>(Body).Any(t => t.Text.Contains("cannot be undone")), "First folder-delete press displays scope and irreversibility");
            Click(Btn("Delete permanently")); Check(vault.Document.Folders.Count == 2, "Rapid double-click cannot bypass folder confirmation"); await Task.Delay(650); await Render("10-folder-delete");
            Click(Btn("Delete permanently")); Check(vault.Document.Folders.Count == 0 && vault.Document.Items.Count == outsideCount, "Final confirmation removes only the selected folder subtree");
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Completed " + results.Count + " checks");
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "preview-error.txt"), ex.ToString()); }
        finally { Application.Current.Shutdown(); }
    }
}
#endif
