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
        void Click(Button b)
        {
            b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, b));
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
            ShowEntryStarters();
            Click(FindButton("Extras"));
            Click(FindButton("Network Profile"));
            Field<TextBox>("Name").Text = "Office DNS";
            Field<ComboBox>("Network adapter").SelectedIndex = 0;
            Click(FindButton("Save entry"));
            Check(vault.Document!.Items.Count == 0, "Saving without selecting a change is blocked");
            Field<ComboBox>("IPv4 DNS servers").SelectedIndex = 2;
            Field<TextBox>("Primary DNS").Text = "192.0.2.053";
            Click(FindButton("Save entry"));
            Check(vault.Document.Items.Count == 0, "Malformed IPv4 DNS values are rejected");
            Field<TextBox>("Primary DNS").Text = "192.0.2.53";
            Field<TextBox>("Secondary DNS (optional)").Text = "192.0.2.54";
            await Task.Delay(200);
            Render("network-profile-editor");
            Click(FindButton("Save entry"));
            var entry = vault.Document.Items.Single();
            var profile = NetworkProfile.Read(entry);
            Check(profile.AddressMode == NetworkSetting.Keep && profile.DnsMode == NetworkSetting.Manual
                && profile.Review && applications.Count == 0, "Saving a DNS-only entry does not apply it or include an address change");

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
            Field<ComboBox>("IPv4 address").SelectedIndex = 2;
            Field<ComboBox>("IPv4 DNS servers").SelectedIndex = 0;
            Field<TextBox>("IP address").Text = "192.0.2.10";
            Field<TextBox>("Subnet mask").Text = "255.0.255.0";
            Click(FindButton("Save entry"));
            Check(NetworkProfile.Read(vault.Document.Items.Single()).AddressMode == NetworkSetting.Keep,
                "An invalid noncontiguous subnet mask leaves the stored profile intact");
            Field<TextBox>("Subnet mask").Text = "255.255.255.0";
            Field<TextBox>("Gateway (optional)").Text = "192.0.2.1";
            var review = LogicalDescendants(Body).OfType<CheckBox>()
                .Single(c => c.Content is TextBlock t && t.Text == "Review changes before applying");
            review.IsChecked = false;
            Click(FindButton("Save entry"));
            entry = vault.Document.Items.Single();
            await RunNetworkProfile(entry);
            Check(applications.Count == 2 && applications[1] is
                { AddressMode: NetworkSetting.Manual, PrefixLength: 24, DnsMode: NetworkSetting.Keep },
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
