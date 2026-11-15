#if DEBUG
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Media.Imaging;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private async Task PreviewV91()
    {
        var output = Path.Combine(App.DataDirectory, "renders"); Directory.CreateDirectory(output);
        var results = new List<string>();
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); results.Add("PASS: " + name); File.WriteAllLines(Path.Combine(output, "checks.txt"), results); }
        Button Btn(string label) => Descendants<Button>(Body).Single(b => b.Content as string == label);
        TextBox InputNamed(string label) => Descendants<TextBox>(Body).Single(b => AutomationProperties.GetName(b) == label);
        ComboBox Choice(string label) => Descendants<ComboBox>(Body).Single(b => AutomationProperties.GetName(b) == label);
        void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, button));
        async Task Render(string name, FrameworkElement? target = null, int wait = 240)
        {
            await Task.Delay(wait); UpdateLayout(); target ??= this;
            var bitmap = new RenderTargetBitmap(Math.Max(1, (int)target.ActualWidth), Math.Max(1, (int)target.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(target);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
        }
        try
        {
            if (vault.Exists) throw new InvalidOperationException("Use an empty fixture directory.");
            ShowWelcome(); await Render("01-welcome", wait: 900);
            var setup = new SetupDraft(); ShowSetupRecovery(setup); UpdateLayout();
            Check(Btn("Save key").IsVisible && Descendants<TextBox>(Body).All(b => !b.IsVisible), "Recovery key is hidden and Save key is available");
            for (var i = 0; i < 4; i++) Click(pageSubmit!);
            Check(!Descendants<TextBlock>(Body).Any(t => t.Text == "Save your recovery key first."), "Repeated unchecked recovery confirmation adds no inline explanation");
            Check(Descendants<ScrollViewer>(Body).First().ScrollableHeight == 0, "Recovery step still fits without scrolling");
            Click(Btn("Reveal key")); UpdateLayout();
            var key = Descendants<TextBox>(Body).Single();
            key.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseUpEvent });
            Check(((PreviewClipboard)clipboard).LastText == setup.Recovery, "Clicking the revealed recovery key copies its real value");
            Check(Descendants<TextBlock>(Body).Any(t => t.Text == "✓ Copied"), "Recovery copy has green confirmation");
            clipboard.ClearOwned(); key.SelectAll(); ApplicationCommands.Copy.Execute(null, key);
            Check(((PreviewClipboard)clipboard).LastText == setup.Recovery && ((PreviewClipboard)clipboard).LastSensitive, "Keyboard Copy uses the protected recovery-key clipboard path");
            await Render("02-recovery");
            vault.Create("Preview-only-passphrase-v91", Crypto.NewRecoveryKey()); session.Unlock(); session.IsPinned = true; Topmost = true;
            vault.Mutate(d => { d.Settings.Startup = false; d.Settings.CloseAfterCopy = false; d.Settings.FirstEntryTourPending = false; });
            ShowSetupReady(); await Render("03-ready-start", wait: 80); await Render("04-ready-settle", wait: 170); await Render("05-ready", wait: 700);
            Check(Descendants<TextBlock>(Body).Any(t => t.Text == "Sable is ready") && !Descendants<TextBlock>(Body).Any(t => t.Text == "Welcome to Sable"), "Ready screen introduces secrets and shortcuts with the new scene");
            ShowEditor(); UpdateLayout();
            Check(!Descendants<RadioButton>(Body).Any(), "Editor has no nested Secret/Value/Login/More tabs");
            var template = Descendants<Button>(Body).Single(b => AutomationProperties.GetName(b) == "Choose entry template");
            InputNamed("Label").Text = "Saved API token";
            Descendants<MaskedSecretBox>(Body).Single().Password = "fictional-token-only";
            Click(Btn("Description · optional")); InputNamed("Shown in the entry tooltip").Text = "Development account · rotate monthly";
            Click(pageSubmit!); UpdateLayout();
            var item = vault.Document!.Items.Single();
            Check(item.ClickBehavior == "copy" && item.Description.Length > 0, "Secret saves description and explicit copy behavior");
            var row = entryList!.Items.Cast<RowModel>().Single();
            Check(row.HoverDetails.Contains(item.Description) && !row.HoverDetails.Contains("fictional-token"), "Hover includes description without displaying the secret");
            var menu = BuildEntryContext(row); var del = menu.Items.OfType<MenuItem>().Single(m => m.Header as string == "Delete…");
            del.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(vault.Document.Items.Count == 1 && del.Header is TextBlock { Text: "Confirm delete", Foreground: SolidColorBrush red } && red.Color.R > red.Color.G && !row.DeleteArmed, "First Delete confirms in muted red inside the context menu, not in the middle of the entry");
            menu.PlacementTarget = Body; menu.IsOpen = true; await Render("06-delete-menu", menu); menu.IsOpen = false;
            Check(vault.Document.Items.Count == 1, "Closing confirmation leaves the entry intact");
            del.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Check(vault.Document.Items.Single().DeletedAt is not null, "Second Delete moves the secret to encrypted Trash");
            vault.Mutate(d => d.Items.Single().DeletedAt = null);
            ShowList(); UpdateLayout(); ArmDelete(entryList!.Items.Cast<RowModel>().Single());
            await Dispatcher.InvokeAsync(() => { });
            Check(activeContextMenu?.IsOpen == true && armedDelete is null && activeContextMenu.Items.OfType<MenuItem>().Any(m => m.Header is TextBlock { Text: "Confirm delete" }), "Keyboard deletion uses the same in-menu confirmation");
            CloseContextMenu();
            ShowEditor(selectedType: "sable.shortcut"); UpdateLayout();
            InputNamed("Label").Text = "Health check";
            Check(!Choice("Action").Items.Cast<string>().Any(s => s is "Ping" or "Test port" or "DNS lookup"), "New actions omit hard-coded diagnostics");
            Choice("Action").SelectedItem = "PowerShell"; UpdateLayout();
            InputNamed("Action target or script").Text = "Write-Output $name";
            var fields = LogicalDescendants(Body).OfType<CheckBox>().Single(c => c.Content is TextBlock t && t.Text == "Pass this entry's fields to PowerShell"); fields.IsChecked = true;
            var admin = LogicalDescendants(Body).OfType<CheckBox>().Single(c => c.Content is TextBlock t && t.Text == "Run as administrator");
            Check(admin.IsVisible, "PowerShell exposes the administrator option");
            var launchCount = 0; previewActionLaunch = _ => launchCount++;
            Click(pageSubmit!); Check(vault.Document.Items.Count == 1, "Untested action cannot be saved");
            Click(Btn("Test action")); Click(pageSubmit!); UpdateLayout();
            var command = vault.Document.Items.Single(i => i.Label == "Health check");
            Check(launchCount == 1 && command.Advanced!.Action!.UseEntryVariables && command.ClickBehavior == "run", "Test then save persists a PowerShell template and run behavior");
            ShowEditor(command); await Render("07-command-editor");
            var login = new VaultItem { Label = "Run and copy login", ContentType = "sable.login", ClickBehavior = "run-copy", Advanced = new() { Action = new() { Kind = "powershell", Target = "Write-Output 'fixture'", CopyOnRun = true } }, Payload = JsonSerializer.SerializeToElement(new { username = "fictional-user", password = "fictional-password" }) };
            vault.Mutate(d => d.Items.Add(login)); actionApprovals.Remember(login.Id, login.Advanced.Action!); ShowList(); UpdateLayout();
            var loginRow = entryList!.Items.Cast<RowModel>().Single(r => r.Item.Id == login.Id);
            var loginButton = new Button { Tag = "copy", DataContext = loginRow };
            RowClick(entryList, new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, loginButton)); await Task.Delay(260);
            Check(((PreviewClipboard)clipboard).LastText == "fictional-user" && pendingLogin == login.Id && holdForPassword, "Run and copy login runs then copies username and holds the panel");
            var beforeSecondClick = launchCount;
            RowClick(entryList, new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, loginButton)); await Task.Delay(260);
            Check(((PreviewClipboard)clipboard).LastText == "fictional-password" && pendingLogin is null && launchCount == beforeSecondClick, "Second login click copies the password without running twice");
            foreach (var archive in new[] { "putty.zip", "rdp.zip" }) modules.Install(ModulePackages.Read(ModuleTrust.Resource(archive), ModuleTrust.Verify));
            ReloadModules(); ShowEditor(); UpdateLayout();
            var templates = Descendants<Button>(Body).Single(b => AutomationProperties.GetName(b) == "Choose entry template"); Click(templates);
            Check(templates.ContextMenu.Items.OfType<MenuItem>().Any(m => m.Header as string == "SSH · PuTTY") && templates.ContextMenu.Items.OfType<MenuItem>().Any(m => m.Header as string == "Remote Desktop"), "Installed SSH and RDP appear directly in the single template picker");
            await Render("09-template-picker", templates.ContextMenu); templates.ContextMenu.IsOpen = false;
            InputNamed("Label").Text = "Name kept across templates";
            var sshChoice = templates.ContextMenu.Items.OfType<MenuItem>().Single(m => m.Header as string == "SSH · PuTTY"); sshChoice.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); UpdateLayout();
            Check(InputNamed("Name").Text == "Name kept across templates" && Btn("Connection options").IsVisible, "Switching to SSH keeps the name and exposes connection settings");
            var rdpProvider = content.All.OfType<DeclarativeProvider>().Single(p => p.Package.Manifest.Handler == "rdp");
            ShowEditor(selectedType: rdpProvider.Descriptor.Id); UpdateLayout(); InputNamed("Name").Text = "Lab server"; InputNamed("Host").Text = "server.example.test"; InputNamed("Username").Text = "fixture";
            Descendants<PasswordBox>(Body).Single().Password = "fictional-rdp-secret"; Choice("When I click the entry").SelectedItem = "Copy"; Click(pageSubmit!); UpdateLayout();
            var rdpRow = entryList!.Items.Cast<RowModel>().Single(r => r.Item.Label == "Lab server");
            RowClick(entryList, new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, new Button { Tag = "copy", DataContext = rdpRow })); await Task.Delay(260);
            Check(((PreviewClipboard)clipboard).LastText == "fictional-rdp-secret" && screen == "list", "RDP Copy behavior copies its password without launching a session");
            ShowEditor(rdpRow.Item); UpdateLayout(); Check(Choice("When I click the entry").SelectedItem as string == "Copy" && !Descendants<RadioButton>(Body).Any(), "RDP uses one common behavior control without duplicate launch modes");
            ShowEditor(item); UpdateLayout();
            Check(Choice("When I click the entry").SelectedItem as string == "Copy", "Editing preserves click behavior");
            ShowList();
            var f1 = new VaultFolder { Name = "KU", Order = 0 }; var f2 = new VaultFolder { Name = "System", Order = 1 }; var f3 = new VaultFolder { Name = "A long folder name with more information", Order = 2 };
            vault.Mutate(d => d.Folders.AddRange([f1, f2, f3, new() { Name = "Archive", Order = 3 }, new() { Name = "Servers", Order = 4 }]));
            ShowList(); UpdateLayout();
            var chips = folderChips!.Children.OfType<Button>().ToArray();
            Check(chips[0].ActualWidth < chips[1].ActualWidth && chips[2].ActualWidth <= 150 && chips[2].ToolTip.ToString()!.Contains(f3.Name), "Folder chips follow name width, cap long names and retain full tooltip");
            Check(chips.Select(c => c.TranslatePoint(new(), folderChips).Y).Distinct().Count() == 1, "Folders remain one horizontal row");
            Check(!FolderRail.ShouldBrowse(true, 100, new Vector(30, 0)), "Dragging a folder no longer changes into rail panning");
            var dragged = false; previewFolderPressed = _ => true; var pointer = new Point(20, 80); previewFolderPointer = _ => pointer;
            previewFolderDrag = (_, data) => dragged = OwnFolderDrag(data, out var id) && id == f1.Id;
            chips[0].RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            pointer = new Point(55, 80); chips[0].RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseMoveEvent });
            Check(dragged && activeDragFolder is null, "Immediate horizontal gesture starts an owned folder move and cleans up");
            previewFolderPressed = null; previewFolderPointer = null; previewFolderDrag = null;
            Check(PlaceFolder(f1.Id, 2) && vault.Document.Folders.Where(f => f.ParentId is null).OrderBy(f => f.Order).ElementAt(1).Id == f1.Id, "Folder reordering persists");
            Check(MoveFolderInto(f1.Id, f2.Id) && vault.Document.Folders.Single(f => f.Id == f1.Id).ParentId == f2.Id, "Dropping a folder into another persists nesting");
            Check(!MoveFolderInto(f2.Id, f1.Id), "Folder cannot be nested inside its descendant");
            MoveFolderInto(f1.Id, null); ShowList(); UpdateLayout();
            Click(Descendants<Button>(Body).Single(b => AutomationProperties.GetName(b) == "Toggle entry layout")); Check(CurrentEntryLayout == "grid", "Root view switches to grid");
            folder = f2.Id; ShowList(); Check(CurrentEntryLayout == "rows", "Folder retains its own row view");
            UpdateLayout(); Click(Descendants<Button>(Body).Single(b => AutomationProperties.GetName(b) == "Toggle entry layout"));
            folder = f1.Id; ShowList(); Check(CurrentEntryLayout == "rows", "Changing one folder does not change another");
            folder = f2.Id; ShowList(); Check(CurrentEntryLayout == "grid", "Returning to a folder restores its grid view");
            folder = null; ShowList(); await Render("08-grid"); Check(CurrentEntryLayout == "grid", "Root view survives folder navigation");
            previewActionLaunch = null;
            var ps = new EntryAction { Kind = "powershell", Target = "Write-Output $value1", UseEntryVariables = true, RunAsAdministrator = true };
            var start = PowerShellBroker.Prepare(ps, "Sable-0123456789");
            Check(start.UseShellExecute && start.Verb == "runas", "Administrator launch uses Windows UAC");
            var loader = Encoding.Unicode.GetString(Convert.FromBase64String(start.ArgumentList.Last()));
            Check(loader.Contains("NamedPipeClientStream") && !loader.Contains(ps.Target), "Command line contains fixed pipe loader, not user script or field values");
            var probe = Path.Combine(App.DataDirectory, "pipe-proof.txt");
            var fake = new VaultItem { Label = "fictional name", Payload = JsonSerializer.SerializeToElement(new { text = "'; $(throw 'not code')\nline two" }) };
            ps.RunAsAdministrator = false; ps.Target = "[System.IO.File]::WriteAllText('" + probe.Replace("'", "''") + "', [System.BitConverter]::ToString([System.Security.Cryptography.SHA256]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes($value1))))";
            await PowerShellBroker.Run(ps, fake, CancellationToken.None, hidden: true);
            for (var n = 0; n < 80 && !File.Exists(probe); n++) await Task.Delay(100);
            Check(File.Exists(probe) && File.ReadAllText(probe) == BitConverter.ToString(SHA256.HashData(Encoding.UTF8.GetBytes(fake.Field("text")))), "Real PowerShell receives exact multiline data through the pipe without executing embedded shell text");
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Completed " + results.Count + " checks");
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "preview-error.txt"), ex.ToString()); }
        finally { Application.Current.Shutdown(); }
    }
}
#endif
