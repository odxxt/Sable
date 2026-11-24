#if DEBUG
using System.Text;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Media.Imaging;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private async Task PreviewNetworkProfile()
    {
        var output = Path.Combine(App.DataDirectory, "renders");
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add("PASS: " + message);
            File.WriteAllLines(Path.Combine(output, "checks.txt"), checks);
        }
        Button FindButton(string label) => LogicalDescendants(Body).OfType<Button>()
            .Single(b => b.Content as string == label || AutomationProperties.GetName(b) == label);
        T Field<T>(string name) where T : Control =>
            LogicalDescendants(Body).OfType<T>().Single(c => AutomationProperties.GetName(c) == name);
        Ipv4EntryField AddressField(string name) =>
            LogicalDescendants(Body).OfType<Ipv4EntryField>().Single(c => AutomationProperties.GetName(c) == name);
        TextBox Octet(string name, int part) => LogicalDescendants(AddressField(name)).OfType<TextBox>().ElementAt(part - 1);
        void PasteAddress(string name, string value)
        {
            var data = new System.Windows.DataObject(DataFormats.UnicodeText, value);
            var args = new DataObjectPastingEventArgs(data, false, DataFormats.UnicodeText) { RoutedEvent = System.Windows.DataObject.PastingEvent };
            Octet(name, 1).RaiseEvent(args);
            UpdateLayout();
        }
        void Click(Button b)
        {
            b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, b));
            UpdateLayout();
        }
        void AddChange(string name)
        {
            var add = FindButton("Add a change");
            Click(add);
            var item = add.ContextMenu!.Items.OfType<MenuItem>().Single(i => (string)i.Header == name);
            Check(item.IsEnabled, "The Add a change menu offers " + name);
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
            UpdateLayout();
        }
        void Render(string name)
        {
            UpdateLayout();
            var bitmap = new RenderTargetBitmap(342, 411, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(this);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, name + ".png"));
            encoder.Save(file);
        }

        try
        {
            vault.Create("Fictional-network-fixture-passphrase", Crypto.NewRecoveryKey());
            session.Unlock();
            vault.Mutate(d => { d.Settings.Startup = false; d.Settings.FirstEntryTourPending = false; });
            var adapterId = Guid.Parse("3F067E7E-426D-4AC0-B8C1-B9F5E8ACDA3C");
            previewNetworkAdapters = () => [new(adapterId, "Fixture Ethernet")];
            var applications = new List<NetworkProfile>();
            var applyCode = 0;
            previewNetworkApply = profile => { applications.Add(profile); return Task.FromResult(applyCode); };
            ShowPluginManager();
            Check(installedModules.Count == 0 && !LogicalDescendants(Body).OfType<Button>()
                .Any(b => AutomationProperties.GetName(b).EndsWith(" add", StringComparison.Ordinal)),
                "Fresh installs expose GitHub and local ZIP installation without offering bundled modules");
            var package = ModulePackages.Read(ModuleTrust.Resource("network-profile.zip"), ModuleTrust.Verify);
            Check(package.Official && package.Manifest.EffectiveCategory == "extras", "Official Network Profile ZIP verifies and belongs under Extras");
            Click(FindButton("GitHub catalog"));
            Click(FindButton("Network Profile  ↓"));
            Check(installedModules.Count == 0 && FindButton("Install").IsEnabled,
                "Verified catalog and ZIP open a review before installation");
            Click(FindButton("Install"));
            Check(installedModules.Single().Fingerprint == package.Fingerprint, "Install registers the verified ZIP from the catalog");
            Click(FindButton("GitHub catalog"));
            Check(FindButton("Network Profile  ✓").IsEnabled, "The catalog marks the package as already installed");
            modules.Remove(package);
            ReloadModules();
            ShowPluginManager();
            var appZip = Path.Combine(output, "Sable-app-preview-win-x64.zip");
            using (var oversized = File.Create(appZip)) oversized.SetLength(ModulePackages.MaxBytes + 1L);
            ReviewModuleFile(appZip);
            Check(installedModules.Count == 0 && formError?.Text.Contains("extract it and open Sable.exe", StringComparison.Ordinal) == true,
                "Choosing the app ZIP gives extraction instructions without attempting to install it");
            var moduleZip = Path.Combine(output, "sable.network-profile.zip");
            File.WriteAllBytes(moduleZip, package.Archive);
            ReviewModuleFile(moduleZip);
            Check(installedModules.Count == 0 && FindButton("Install").IsEnabled, "The local module ZIP opens the install review");
            Click(FindButton("Install"));
            Check(installedModules.Single().Fingerprint == package.Fingerprint, "Installing the local ZIP registers Network Profile");
            ShowEntryStarters();
            Click(FindButton("Extras"));
            Click(FindButton("Network change"));
            var nameField = Field<TextBox>("Entry name");
            var adapterField = Field<ComboBox>("Network adapter");
            nameField.Text = "Office DNS";
            adapterField.SelectedIndex = 0;
            UpdateLayout();
            Check(Math.Abs(nameField.TranslatePoint(new Point(), Body).Y - adapterField.TranslatePoint(new Point(), Body).Y) < .1
                && Math.Abs(nameField.ActualHeight - adapterField.ActualHeight) < .1,
                "Entry name and adapter share the same top and height");
            Check(!FindButton("Save entry").IsEnabled && vault.Document!.Items.Count == 0, "Saving without selecting a change is blocked");
            AddChange("DNS");
            Check(!AddressField("IP address").IsVisible && AddressField("DNS server").IsVisible, "A DNS-only entry exposes only its own settings");
            PasteAddress("DNS server", "192.0.2.53");
            Octet("DNS server", 4).Text = "300";
            Click(FindButton("Save entry"));
            Check(vault.Document!.Items.Count == 0 && AutomationProperties.GetHelpText(AddressField("DNS server")).Length > 0,
                "Malformed DNS marks its own field and prevents a save");
            PasteAddress("DNS server", "192.0.2.53");
            Click(FindButton("Add Secondary DNS"));
            PasteAddress("Secondary DNS", "192.0.2.54");
            var secondary = AddressField("Secondary DNS");
            var removeSecondary = FindButton("Remove Secondary DNS");
            Check(removeSecondary.TranslatePoint(new Point(), secondary).X > secondary.ActualWidth / 2,
                "Secondary DNS has a separate X on its right");
            await Task.Delay(200);
            Render("network-profile-editor");
            Click(removeSecondary);
            Check(!secondary.IsVisible && secondary.Value.Length == 0 && AddressField("DNS server").Value == "192.0.2.53",
                "Removing Secondary DNS clears it and preserves the primary server");
            Click(FindButton("Add Secondary DNS"));
            Check(secondary.Value.Length == 0, "Reopening Secondary DNS starts blank");
            Click(FindButton("Remove Secondary DNS"));
            Click(FindButton("Save entry"));
            var entry = vault.Document.Items.Single();
            var profile = NetworkProfile.Read(entry);
            Check(profile.AddressMode == NetworkSetting.Keep && profile.DnsMode == NetworkSetting.Manual
                && profile.SecondaryDns is null && profile.Review && applications.Count == 0,
                "Saving a DNS-only entry does not apply it or include a cleared value or address change");

            var row = (ListBoxItem)entryList!.ItemContainerGenerator.ContainerFromIndex(0);
            Click(Descendants<Button>(row).Single(b => b.Tag as string == "copy"));
            Check(screen == "module-permission" && applications.Count == 0, "Clicking an entry opens an apply review without making a change");
            Render("network-profile-review");
            GoBack();
            Check(applications.Count == 0, "Going back from review leaves the adapter alone");
            await RunNetworkProfile(entry);
            Click(FindButton("Apply changes"));
            Check(applications.Count == 1 && applications[0] == profile, "Explicit Apply sends the selected DNS-only profile to the Windows broker");

            ShowEditor(entry);
            AddChange("IP address");
            Check(AddressField("Subnet mask").Value == "255.255.255.0",
                "Adding manual IPv4 starts with the default subnet mask");
            var ipGroup = FindParent<Border>(FindButton("IP address settings"));
            Check(ipGroup?.Background == FindResource("OptionGroupSurface") && ipGroup.BorderThickness.Left == 0,
                "An expanded network group uses the shared options surface without a separate outline");
            PasteAddress("IP address", "192.0.2.10");
            PasteAddress("Subnet mask", "255.0.255.0");
            Click(FindButton("Save entry"));
            Check(NetworkProfile.Read(vault.Document.Items.Single()).AddressMode == NetworkSetting.Keep
                && AutomationProperties.GetHelpText(AddressField("Subnet mask")).Length > 0,
                "A noncontiguous mask marks the mask field and leaves the stored entry intact");
            PasteAddress("Subnet mask", "/24");
            Check(AddressField("Subnet mask").Value == "255.255.255.0", "Pasting a prefix fills in the dotted subnet mask");
            Click(FindButton("Add Default gateway"));
            PasteAddress("Default gateway", "192.0.2.1");
            var gateway = AddressField("Default gateway");
            var removeGateway = FindButton("Remove Default gateway");
            Check(removeGateway.TranslatePoint(new Point(), gateway).X > gateway.ActualWidth / 2,
                "Default gateway has a separate X on its right");
            Click(removeGateway);
            Check(!gateway.IsVisible && gateway.Value.Length == 0 && AddressField("IP address").Value == "192.0.2.10",
                "Removing Default gateway preserves the IP address");
            var ipHeader = FindButton("IP address settings");
            var dnsHeader = FindButton("DNS settings");
            Click(dnsHeader);
            Check(dnsHeader.TranslatePoint(new Point(), Body).Y > ipHeader.TranslatePoint(new Point(), Body).Y
                && AddressField("DNS server").IsVisible && !AddressField("IP address").IsVisible
                && AddressField("IP address").Value == "192.0.2.10" && AddressField("DNS server").Value == "192.0.2.53",
                "DNS opens below the IP block and preserves both values");
            Render("network-change-block-order");
            Click(FindButton("Save entry"));
            entry = vault.Document.Items.Single();
            await RunNetworkProfile(entry);
            UpdateLayout();
            var ordered = Descendants<TextBlock>(Body).Where(t => t.Text is "IP address" or "Subnet mask" or "DNS").ToArray();
            Check(ordered.Select(t => t.Text).SequenceEqual(["IP address", "Subnet mask", "DNS"])
                && Descendants<TextBlock>(Body).Where(t => t.Text is "192.0.2.10" or "255.255.255.0" or "192.0.2.53").Count(t => t.FontSize >= 16) == 3,
                "The combined review stacks IP, subnet and DNS with larger values");
            Check(!Descendants<TextBlock>(Body).Any(t => t.Text is "Default gateway" or "Secondary DNS"),
                "The review omits both removed optional values");
            Check(Descendants<ScrollViewer>(Body).Single().ScrollableHeight == 0,
                "The combined review without optional fields fits in the standard window");
            Render("network-change-review");
            Click(FindButton("Cancel"));

            // Earlier versions also saved CIDR in the same field.
            var fields = entry.Payload.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
            fields["mask"] = "/25";
            vault.Mutate(d => d.Items.Single().Payload = JsonSerializer.SerializeToElement(fields));
            entry = vault.Document.Items.Single();
            ShowEditor(entry);
            UpdateLayout();
            Check(AddressField("Subnet mask").Value == "255.255.255.128" && AddressField("DNS server").Value == "192.0.2.53",
                "A saved subnet mask overrides the default when reopening an entry");
            Click(FindButton("Remove DNS change"));
            var review = LogicalDescendants(Body).OfType<CheckBox>()
                .Single(c => c.Content is TextBlock t && t.Text == "Review before applying");
            review.IsChecked = false;
            Click(FindButton("Save entry"));
            entry = vault.Document.Items.Single();
            await RunNetworkProfile(entry);
            Check(applications.Count == 2 && applications[1] is
                { AddressMode: NetworkSetting.Manual, PrefixLength: 25, DnsMode: NetworkSetting.Keep },
                "Unchecking review in the editor applies locally by click, and static IPv4 leaves DNS alone");
            moduleApprovals.Clear();
            await RunNetworkProfile(entry);
            Check(screen == "module-permission" && applications.Count == 2,
                "A missing device approval restores review even if a transferred entry has review disabled");
            applyCode = 12;
            Click(FindButton("Apply changes"));
            Check(screen == "module-permission" && applications.Count == 3, "Windows failure leaves the review open for inspection or retry");
            GoBack();
            previewNetworkAdapters = () => [];
            await RunNetworkProfile(entry);
            Check(applications.Count == 3, "A missing saved adapter never falls back to a different adapter");
            modules.Remove(package);
            ReloadModules();
            ShowList();
            await RunNetworkProfile(entry);
            Check(applications.Count == 3 && vault.Document.Items.Count == 1,
                "Uninstall blocks applying while preserving the saved entry");

            var start = NetworkProfileBroker.Prepare(profile);
            Check(start.Verb == "runas" && start.FileName == EntryActions.SystemFile(@"WindowsPowerShell\v1.0\powershell.exe")
                && start.ArgumentList.Contains("-NoProfile"), "Windows authorization uses one fixed system PowerShell command");
            var script = Encoding.Unicode.GetString(Convert.FromBase64String(start.ArgumentList.Last()));
            File.WriteAllText(Path.Combine(output, "broker-dns.ps1"), script);
            File.WriteAllText(Path.Combine(output, "broker-static.ps1"), Encoding.Unicode.GetString(
                Convert.FromBase64String(NetworkProfileBroker.Prepare(NetworkProfile.Read(entry)).ArgumentList.Last())));
            var auto = profile with { AddressMode = NetworkSetting.Automatic, DnsMode = NetworkSetting.Automatic };
            File.WriteAllText(Path.Combine(output, "broker-auto.ps1"), Encoding.Unicode.GetString(
                Convert.FromBase64String(NetworkProfileBroker.Prepare(auto).ArgumentList.Last())));
            File.WriteAllText(Path.Combine(output, "complete.txt"), $"Completed {checks.Count} network profile checks");
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "preview-error.txt"), ex.ToString()); }
        finally { System.Windows.Application.Current.Shutdown(); }
    }
}
#endif
