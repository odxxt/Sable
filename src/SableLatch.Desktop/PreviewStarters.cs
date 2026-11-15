#if DEBUG
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Media.Imaging;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private async Task PreviewStarters()
    {
        var output = Path.Combine(App.DataDirectory, "renders"); Directory.CreateDirectory(output); var checks = new List<string>();
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks.Add("PASS: " + message); File.WriteAllLines(Path.Combine(output, "checks.txt"), checks); }
        Button Btn(string name) { UpdateLayout(); return Descendants<Button>(Body).Single(b => b.Content as string == name || AutomationProperties.GetName(b) == name); }
        TextBox Input(string name) { UpdateLayout(); return Descendants<TextBox>(Body).Single(b => AutomationProperties.GetName(b) == name); }
        ComboBox Select(string name) { UpdateLayout(); return Descendants<ComboBox>(Body).Single(b => AutomationProperties.GetName(b) == name); }
        CheckBox Toggle(string name) => LogicalDescendants(Body).OfType<CheckBox>().Single(b => b.Content is TextBlock t && t.Text == name);
        void Click(Button b) { b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, b)); UpdateLayout(); }
        async Task Render(string name)
        {
            await Task.Delay(180); UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, name + ".png")); png.Save(file);
            var caption = Descendants<TextBlock>(Body).FirstOrDefault(t => t.Text == "Name");
            if (caption is not null)
            {
                var scroller = FindParent<ScrollViewer>(caption)!;
                File.AppendAllText(Path.Combine(output, "layout.txt"), name + ": caption=" + caption.TranslatePoint(new(), scroller).Y + ", height=" + caption.ActualHeight + ", offset=" + scroller.VerticalOffset + ", visible=" + caption.IsVisible + "\n");
            }
        }
        void Secret(string name, string value) { Input("Name").Text = name; Descendants<MaskedSecretBox>(Body).Single().Password = value; }
        try
        {
            if (vault.Exists) throw new InvalidOperationException("Use a fresh disposable fixture.");
            ShowSetupRecovery(new()); UpdateLayout();
            Check(Descendants<ScrollViewer>(Body).All(s => s.ScrollableHeight == 0), "Shared form padding preserves the no-scroll recovery setup page");
            vault.Create("Preview-only-starter-passphrase", Crypto.NewRecoveryKey()); session.Unlock(); session.IsPinned = true; Topmost = true;
            vault.Mutate(d => { d.Settings.Startup = false; d.Settings.FirstEntryTourPending = false; d.Settings.CloseAfterCopy = false; });
            ShowEditor(); UpdateLayout();
            Check(Btn("Secret or login").ToolTip is string && Btn("Shortcut").IsVisible && !Descendants<Button>(Body).Any(b => AutomationProperties.GetName(b) == "Connection"), "No connection starter without an installed module; starters have descriptions and tooltips");
            Check(!Descendants<ComboBox>(Body).Any() && !Descendants<Button>(Body).Any(b => b.Content?.ToString()?.Contains("Install") == true), "Starter screen has no dropdown or module installer");
            Check(Descendants<ScrollViewer>(Body).First().ScrollableHeight == 0, "Default starters fit the fixed window without scrolling");
            await Render("01-starters"); Click(Btn("Secret or login"));
            Check(Btn("Add username").IsVisible && !Input("Username").IsVisible && Descendants<TextBlock>(Body).Any(t => t.Text == "Secret value"), "Secret has a value and an optional username above it");
            Secret("API token", "fictional-multiline\nsecret");
            await Render("02-secret"); Click(Btn("Add username")); Input("Username").Text = "admin";
            Check(Descendants<TextBlock>(Body).Any(t => t.Text == "Password") && Btn("Remove username").TranslatePoint(new(), Body).Y < Input("Username").TranslatePoint(new(), Body).Y, "Username toggle stays above the credentials and changes the value caption");
            Check(captureEditorDraft!().ContentType == "sable.login" && captureEditorDraft!().Field("password") == "fictional-multiline\nsecret", "Adding username retains the secret without saving");
            Click(Btn("Remove username")); Check(captureEditorDraft!().Field("text").Contains('\n'), "Removing username keeps a multiline secret");
            Click(Btn("Add username")); Check(Input("Username").Text == "admin", "Username draft survives toggle off and back on");
            await Render("03-login"); Click(Btn("Change starter")); Click(Btn("Shortcut"));
            Check(Input("Name").Text == "API token", "Changing starters retains the name");
            Check(!Select("Action").Items.Cast<string>().Any(s => s.Contains("Connection")) && !Btn("Entry script fields").IsVisible, "Shortcut has action types only and hides script fields until relevant");
            Input("Name").Text = "Retained name"; Select("Action").SelectedItem = "PowerShell"; Input("Action target or script").Text = "Write-Output $name";
            Click(Btn("Change starter")); Click(Btn("Secret or login"));
            Check(Input("Name").Text == "Retained name" && Input("Username").Text == "admin" && Descendants<MaskedSecretBox>(Body).Single().Password.Contains('\n'), "Returning to a starter preserves credentials and shares the latest name");
            var windowSize = RenderSize;
            for (var i = 0; i < 6; i++) Click(Btn("Toggle username"));
            await Task.Delay(240);
            Check(RenderSize == windowSize && Input("Username").Opacity == 1 && Input("Username").Text == "admin" && captureEditorDraft!().ContentType == "sable.login", "Rapid username toggles settle without resizing or losing values");
            Click(Btn("Color & description")); Click(Btn("Entry color sage")); Input("Shown in the entry tooltip").Text = "Development sign-in";
            Click(pageSubmit!); var login = vault.Document!.Items.Single();
            Check(login.ContentType == "sable.login" && login.ClickBehavior == "copy" && login.Advanced?.Color == "sage" && login.Description == "Development sign-in", "Combined login saves color, description and copying behavior");
            ShowEditor(login); Click(Btn("Advanced")); Toggle("Include a shortcut").IsChecked = true; UpdateLayout();
            Select("Action").SelectedItem = "PowerShell"; Input("Action target or script").Text = "Write-Output $name";
            Toggle("Pass this entry's fields to PowerShell").IsChecked = true;
            LogicalDescendants(Body).OfType<RadioButton>().Single(b => b.Content as string == "Run and copy").IsChecked = true;
            var launches = 0; previewActionLaunch = _ => launches++;
            var version = vault.Document.Items.Single().UpdatedAt; Click(pageSubmit!);
            Check(vault.Document.Items.Single().UpdatedAt == version && pageSubmit!.Opacity < 1, "Hybrid action requires a test before saving");
            // Editing an existing entry deliberately reviews its first newly configured action.
            Click(Btn("Test action"));
            if (modalDepth > 0) Click(Descendants<Button>(Body).Last(b => b.Content as string == "Test action" && b.IsVisible));
            await Task.Delay(100); UpdateLayout();
            Check(launches == 1, "Test action uses the intercepted fixture launcher once");
            Click(pageSubmit!); login = vault.Document.Items.Single();
            Check(login.ClickBehavior == "copy" && login.Advanced!.Action!.CopyOnRun, "Hybrid keeps row copying separate from its Run and copy button");
            var row = entryList!.Items.Cast<RowModel>().Single();
            Check(row.ActionLabel == "Run & copy" && row.ActionCopyVisibility == Visibility.Visible, "Run and copy badge matches the independent action behavior");
            var priorLaunches = launches;
            session.IsPinned = false; Topmost = false; vault.Mutate(d => d.Settings.CloseAfterCopy = true);
            RowClick(entryList, new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, new Button { Tag = "copy", DataContext = row })); await Task.Delay(240);
            Check(launches == priorLaunches && ((PreviewClipboard)clipboard).LastText == "admin" && holdForPassword, "Ordinary hybrid click copies username and waits without running");
            RowClick(entryList, new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, new Button { Tag = "copy", DataContext = row })); await Task.Delay(240);
            Check(((PreviewClipboard)clipboard).LastText.Contains('\n') && pendingLogin is null && launches == priorLaunches && !IsVisible, "Next ordinary click copies password and closes the unpinned panel without launching");
            OpenPanel(); UpdateLayout();
            RowClick(entryList, new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, new Button { Tag = "entry-action", DataContext = row })); await Task.Delay(240);
            Check(launches == priorLaunches + 1 && ((PreviewClipboard)clipboard).LastText == "admin" && holdForPassword, "Run and copy launches once and copies the username first");
            RowClick(entryList, new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, new Button { Tag = "copy", DataContext = row })); await Task.Delay(240);
            Check(launches == priorLaunches + 1 && pendingLogin is null && !IsVisible, "Password completion closes the unpinned panel and never launches twice");
            OpenPanel(); session.IsPinned = true; Topmost = true;
            ShowEditor(login); Click(Btn("Advanced")); UpdateLayout(); await Render("04-hybrid");
            ShowList(); ShowEditor(selectedType: "sable.shortcut"); Input("Name").Text = "Health check"; Select("Action").SelectedItem = "PowerShell"; Input("Action target or script").Text = "Write-Output $value1";
            Check(Toggle("Run as administrator").IsVisible && Toggle("Keep terminal open when finished").IsChecked == true && Toggle("Pass this entry's fields to PowerShell").IsVisible, "Shortcut retains elevation, private fields and terminal options");
            Click(Btn("Entry script fields")); Click(Btn("+ Add field")); Descendants<PasswordBox>(Body).Single().Password = "example.test";
            Click(Btn("Test action")); Click(pageSubmit!);
            Check(vault.Document.Items.Last().ClickBehavior == "run" && vault.Document.Items.Last().ScriptValues["value1"] == "example.test", "Shortcut saves its custom encrypted values and run behavior");
            ShowEditor(vault.Document.Items.Last()); Select("When I click the entry").SelectedItem = "Run and copy";
            Check(pageSubmit!.Opacity < 1, "Changing a shortcut to Run and copy requires retesting that behavior");
            Click(Btn("Test action")); if (modalDepth > 0) Click(Descendants<Button>(Body).Last(b => b.Content as string == "Test action" && b.IsVisible)); Click(pageSubmit!);
            var shortcut = vault.Document.Items.Last(); ShowEditor(shortcut);
            Check(pageSubmit!.Opacity == 1 && shortcut.Advanced!.Action!.CopyOnRun && actionApprovals.Contains(shortcut.Id, shortcut.Advanced.Action), "Saved shortcut copy mode matches its tested and approved action without an edit loop");
            ShowEditor(vault.Document.Items.Last()); await Render("05-shortcut");
            foreach (var archive in new[] { "putty.zip", "rdp.zip" }) modules.Install(ModulePackages.Read(ModuleTrust.Resource(archive), ModuleTrust.Verify)); ReloadModules();
            ShowList(); ShowEntryStarters(); await Render("06-with-connections"); Click(Btn("Connection"));
            Check(Btn("SSH · PuTTY").IsVisible && Btn("Remote Desktop").IsVisible, "Installed connection handlers populate their own section");
            await Render("07-connections");
            foreach (var name in new[] { "SSH · PuTTY", "Remote Desktop" })
            {
                Click(Btn(name));
                Check(!Input("Port").IsVisible && Btn("More options").IsVisible && !Descendants<Button>(Body).Any(b => b.Content as string == "Options"), name + " has one collapsed More options section below credentials");
                Input("Name").Text = "Lab server"; Input("Host").Text = "server.example.test"; Input("Username").Text = "admin"; Descendants<PasswordBox>(Body).Single().Password = "fictional-connection-password";
                await Render(name.StartsWith("SSH") ? "08-ssh" : "09-rdp"); Click(Btn("More options")); Check(Input("Port").IsVisible, name + " expands its existing connection controls");
                Click(Btn("Color & description")); Click(Btn("Entry color blue")); Click(pageSubmit!);
                Check(vault.Document.Items.Last().Advanced?.Color == "blue", name + " retains color customization");
                ShowEntryStarters(); Click(Btn("Connection"));
            }
            vault.Mutate(d => d.Settings.DisabledModules.Add("module.sablelatch.putty")); ShowEntryStarters(); Click(Btn("Connection"));
            Check(!Descendants<Button>(Body).Any(b => AutomationProperties.GetName(b) == "SSH · PuTTY") && Btn("Remote Desktop").IsVisible, "Disabled connection module disappears from the section");
            vault.Mutate(d => d.Settings.DisabledModules.Add("module.sablelatch.rdp")); ShowEntryStarters();
            Check(!Descendants<Button>(Body).Any(b => AutomationProperties.GetName(b) == "Connection"), "Connection category disappears when no enabled connection module remains");
            await PreviewPanelModes(Check, Render);
            Click(Btn("Secret or login")); Secret("Unsaved draft", "discard-on-lock"); Click(Btn("Change starter")); ForceLock("Fixture lock");
            Check(editorDrafts.Count == 0 && editorSharedLabel.Length == 0 && captureEditorDraft is null, "Lock clears all starter drafts and editor capture hooks");
            Check(!vault.IsUnlocked && session.Mode == PanelMode.Window && !Topmost && ShowInTaskbar, "Lock preserves normal-window presentation while removing vault access");
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Completed " + checks.Count + " checks");
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "preview-error.txt"), ex.ToString()); }
        finally { Application.Current.Shutdown(); }
    }
}
#endif
