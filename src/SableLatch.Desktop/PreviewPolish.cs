#if DEBUG
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Media.Imaging;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private async Task PreviewPolish()
    {
        var output = Path.Combine(App.DataDirectory, "renders"); Directory.CreateDirectory(output); var checks = new List<string>();
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks.Add("PASS: " + name); File.WriteAllLines(Path.Combine(output, "checks.txt"), checks); }
        Button Named(string name) { UpdateLayout(); return Descendants<Button>(Body).Single(b => b.Content as string == name || AutomationProperties.GetName(b) == name); }
        void Click(Button b) { b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, b)); UpdateLayout(); }
        async Task Render(string name) { await Task.Delay(160); UpdateLayout(); var bitmap = new RenderTargetBitmap(342, 411, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, name + ".png")); png.Save(file); }
        try
        {
            vault.Create("Polish-fictional-passphrase", Crypto.NewRecoveryKey()); session.Unlock();
            var group = new VaultFolder { Name = "Servers", Icon = "servers" };
            vault.Mutate(d =>
            {
                d.Settings.Startup = false; d.Settings.FirstEntryTourPending = false; d.Settings.Layout = "grid"; d.Folders.Add(group);
                d.Items.Add(new() { Label = "VPN code", Payload = JsonSerializer.SerializeToElement(new { text = "fictional-value" }) });
                d.Items.Add(new() { ContentType = "sable.shortcut", Label = "Open notes", IsSensitive = false, ClickBehavior = "run", Advanced = new() { Action = new() { Kind = "run", Target = "notepad" } }, Order = 1 });
                d.Items.Add(new() { Label = "Server token", Payload = JsonSerializer.SerializeToElement(new { text = "fictional-token" }), Advanced = new() { Action = new() { Kind = "run", Target = "notepad" } }, Order = 2 });
                d.Items.Add(new() { ContentType = "sable.login", Label = "Lab login", Payload = JsonSerializer.SerializeToElement(new { username = "demo", password = "fictional-password" }), Order = 3 });
            });
            session.Mode = PanelMode.Window; UpdatePanelTopmost(); ApplyTaskbarVisibility(); UpdatePinButton(); ShowList(); UpdateLayout();
            var rows = entryList!.Items.Cast<RowModel>().ToArray();
            var shortcut = rows.Single(r => r.Item.Label == "Open notes"); var hybrid = rows.Single(r => r.Item.Label == "Server token");
            var shortcutTile = (ListBoxItem)entryList.ItemContainerGenerator.ContainerFromItem(shortcut);
            var hybridTile = (ListBoxItem)entryList.ItemContainerGenerator.ContainerFromItem(hybrid);
            Check(Descendants<Button>(shortcutTile).Single(b => b.Tag as string == "entry-action").IsVisible == false, "Launch-only grid tile has no redundant Run button");
            Check(Descendants<Button>(hybridTile).Single(b => b.Tag as string == "entry-action").IsVisible, "Copying tile keeps its separate Run action");
            Check(Descendants<EntryIdentity>(shortcutTile).Any(p => p.Kind == "shortcut" && p.IsVisible), "Grid tile exposes the entry identity on its left");
            var width = shortcutTile.ActualWidth; var task = ShowRunFeedback(shortcut.Item.Id); UpdateLayout();
            Check(Descendants<TextBlock>(shortcutTile).Single(t => t.Tag as string == "copy-feedback").IsVisible && shortcutTile.ActualWidth == width, "Grid launch feedback replaces the left icon without resizing");
            await task; await Render("01-grid-identities");
            previewSuggestionText = "192.168.8.1"; previewSuggestionSequence++; RefreshSuggestions(); UpdateLayout();
            var original = Descendants<Button>(suggestionHost!).Single(b => b.Tag as string == "network-run");
            var dismiss = Named("Dismiss network suggestion"); var runPath = Descendants<System.Windows.Shapes.Path>(original).Single(); var closePath = (System.Windows.Shapes.Path)dismiss.Content;
            var runCenter = runPath.TranslatePoint(new Point(8, 8), suggestionHost); var closeCenter = closePath.TranslatePoint(new Point(6, 6), suggestionHost);
            Check(Math.Abs(runCenter.Y - closeCenter.Y) < 1 && closeCenter.X - runCenter.X >= 24, "Suggestion Run and X align vertically with separate consistent targets");
            await Render("02-suggestion-padding");
            var card = (Border)suggestionHost!.Children[0]; var size = card.RenderSize; Click(dismiss);
            var once = Named("Only this copy"); var never = Named("Don't show again");
            Check(card.RenderSize == size && once.ActualHeight == never.ActualHeight && Math.Abs(once.TranslatePoint(new(), card).Y - never.TranslatePoint(new(), card).Y) < 1, "Inline dismissal uses aligned controls within the unchanged card");
            Check(never.TranslatePoint(new Point(never.ActualWidth, 0), card).X <= card.ActualWidth - 7, "Dismissal choices retain a right inset");
            await Render("03-inline-dismiss"); Click(once);
            Check(vault.Document!.Settings.ClipboardDismissHintSeen, "The first X click persists its one-time explanation state");
            previewSuggestionSequence++; RefreshSuggestions(); Click(Named("Dismiss network suggestion"));
            Check(suggestionHost.Children.Count == 0, "Later X clicks dismiss directly without another question");
            previewSuggestionSequence++; RefreshSuggestions(); folder = group.Id; ShowList(); RefreshSuggestions();
            Check(suggestionHost.Children.Count == 0, "Entering a folder hides clipboard diagnostics");
            previewSuggestionText = "192.168.8.2:53"; previewSuggestionSequence++; RefreshSuggestions();
            Check(suggestionHost.Children.Count == 0, "Clipboard changes inside a folder stay quiet");
            var launches = 0; previewActionLaunch = _ => launches++;
            Click(original); Check(launches == 0, "An old root suggestion cannot launch from inside a folder");
            folder = null; ShowList(); RefreshSuggestions(); UpdateLayout();
            Check(Descendants<TextBlock>(suggestionHost).Any(t => t.Text == "192.168.8.2:53"), "Returning to All items checks the latest clipboard without a history");
            previewActionLaunch = null; previewSuggestionText = null; RefreshSuggestions();
            var shapes = new HashSet<string>();
            foreach (var mode in new[] { PanelMode.Tray, PanelMode.AlwaysOnTop, PanelMode.Window })
            {
                session.Mode = mode; UpdatePanelTopmost(); ApplyTaskbarVisibility(); UpdatePinButton(); UpdateLayout();
                var icon = (Grid)PinButton.Content; shapes.Add(((System.Windows.Shapes.Path)icon.Children[0]).Data.ToString());
                Check(icon.Width == 16 && icon.Height == 16, mode + " keeps a fixed static toolbar icon"); await Render("04-mode-" + mode);
            }
            Check(shapes.Count == 3, "Tray, pin and normal-window modes have different silhouettes");
            foreach (var icon in FolderIcons.Choices.Keys)
            { var path = (System.Windows.Shapes.Path)FolderIcons.Create(icon); Check(!path.Data.Bounds.IsEmpty && !path.HasAnimatedProperties, "Folder icon renders without animation: " + icon); }
            ShowFolderEditor(group); Click(Named("Customize")); await Render("05-folder-icons");
            Check(Descendants<Button>(Body).Count(b => AutomationProperties.GetName(b).EndsWith(" icon", StringComparison.Ordinal)) == 20, "Folder customization offers all 20 icons");
            var strip = new System.Windows.Controls.Primitives.UniformGrid { Columns = 8, Width = 400, Height = 60, Background = new SolidColorBrush(Color.FromRgb(16, 16, 18)) };
            foreach (var kind in new[] { "secret", "login", "shortcut", "terminal", "desktop", "keyboard", "run", "run-copy" })
            {
                var column = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; var symbol = new EntryIdentity { Kind = kind, HorizontalAlignment = HorizontalAlignment.Center }; column.Children.Add(symbol);
                column.Children.Add(new TextBlock { Text = kind, FontSize = 9, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) }); strip.Children.Add(column);
            }
            strip.Measure(new Size(400, 60)); strip.Arrange(new Rect(0, 0, 400, 60)); strip.UpdateLayout();
            var stripImage = new RenderTargetBitmap(400, 60, 96, 96, PixelFormats.Pbgra32); stripImage.Render(strip); var stripPng = new PngBitmapEncoder(); stripPng.Frames.Add(BitmapFrame.Create(stripImage)); using (var file = File.Create(Path.Combine(output, "06-entry-optical-sizes.png"))) stripPng.Save(file);
            await PreviewEntryMotion(Check, output);
            if (Environment.GetCommandLineArgs().Contains("--preview-icons-only"))
            { File.WriteAllText(Path.Combine(output, "complete.txt"), "Completed " + checks.Count + " UI checks; visual-only run"); return; }
            ShowList(); session.Mode = PanelMode.Window; UpdatePanelTopmost(); ApplyTaskbarVisibility();
            Show(); Activate(); FixtureRaise(handle, new IntPtr(-1), 0, 0, 0, 0, 0x43); FixtureForeground(handle); await Task.Delay(100);
            FixtureRect(handle, out var bounds); GetCursorPos(out var cursor); SetCursorPos(bounds.Left + 30, bounds.Top + 70);
            FixtureMouse(2, 0, 0, 0, UIntPtr.Zero); FixtureMouse(4, 0, 0, 0, UIntPtr.Zero); await Task.Delay(120); SetCursorPos(cursor.X, cursor.Y);
            FixtureRaise(handle, new IntPtr(-2), 0, 0, 0, 0, 0x43);
            await Task.Delay(150);
            Check(NativeTyping.GetForegroundWindow() == handle, "Fictional diagnostic fixture starts from the user's foreground click");
            var probe = new ScriptEntryAction("powershell").Prepare(new() { Kind = "powershell", Target = "Write-Output 'Sable local foreground fixture'; Start-Sleep -Seconds 4", KeepTerminalOpen = false });
            var front = await DiagnosticTerminal.Run(probe, handle, CancellationToken.None);
            await Task.Delay(150);
            Check(front && FixtureTitle(NativeTyping.GetForegroundWindow()).Contains("Sable network check", StringComparison.Ordinal), "Actual local diagnostic console receives foreground without any network request");
            await Task.Delay(4300);
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Completed " + checks.Count + " polish checks");
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "preview-error.txt"), ex.ToString()); }
        finally { Application.Current.Shutdown(); }
    }
}
#endif
