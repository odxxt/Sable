using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private readonly ModuleStore modules;
    private readonly ModuleApprovals moduleApprovals;
    private IReadOnlyList<ModulePackage> installedModules = [];
    private int rejectedModules;
    private bool moduleConnecting;
    private void ReloadModules()
    {
        foreach (var provider in content.All.OfType<DeclarativeProvider>().ToArray()) { content.Remove(provider.Descriptor.Id); extensions.Editors.Remove(provider.Descriptor.Id); }
        installedModules = modules.Load(out rejectedModules);
        foreach (var package in installedModules)
        {
            var provider = new DeclarativeProvider(package); content.Register(provider);
            extensions.RegisterEditor(new FieldEditorFactory(provider.Descriptor.Id, (item, save, _) => package.Manifest.Handler switch
            {
                "putty" => BuildPuttyFields(provider, item, save),
                "rdp" => BuildRdpFields(provider, item, save),
                "type" => BuildTypeFields(provider, item, save),
                "network-profile" => BuildNetworkProfileFields(provider, item, save),
                _ => BuildFields(provider, item, save)
            }));
        }
        SyncTypeAvailability();
    }
    private bool ModuleAvailable(VaultItem item) => content.Find(item.ContentType) is not DeclarativeProvider provider || item.ModuleSchema == provider.Package.Manifest.SchemaFingerprint();
    private void ShowMoreTypes() => ShowEntryStarters();
    private void ShowPluginManager()
    {
        if (!vault.IsUnlocked) return;
        var stack = Form("Plugins & modules", "", "settings", parent: ShowSettings);
        if (rejectedModules > 0) stack.Children.Add(Text("An invalid module was blocked.", 11, true));
        void ModuleRow(ModulePackage package)
        {
            var body = new Grid(); body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = package.Manifest.Handler switch { "putty" => "SSH · PuTTY", "rdp" => "Remote Desktop", _ => package.Manifest.Name }, FontSize = 13, FontWeight = FontWeights.Medium });
            text.Children.Add(new TextBlock { Text = "Installed · " + package.Manifest.Version, FontSize = 11, Opacity = .6, Margin = new Thickness(0, 4, 8, 0), TextWrapping = TextWrapping.Wrap });
            body.Children.Add(text);
            var status = new TextBlock { Text = "✓   ›", FontSize = 11, Opacity = .7, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(status, 1); body.Children.Add(status);
            var row = Button(package.Manifest.Name, () => ShowModuleSettings(package));
            row.Content = body; row.HorizontalContentAlignment = HorizontalAlignment.Stretch; row.Padding = new Thickness(10, 12, 10, 12); row.Margin = new Thickness(0, 3, 0, 3); row.Background = (Brush)FindResource("ControlSurface");
            System.Windows.Automation.AutomationProperties.SetName(row, package.Manifest.Name + " installed"); stack.Children.Add(row);
        }
        if (installedModules.Count > 0)
        {
            stack.Children.Add(Text("Installed", 11, true));
            foreach (var module in installedModules) ModuleRow(module);
        }
        var sources = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(0, 10, 0, 8) };
        sources.Children.Add(Button("GitHub catalog", ShowModuleCatalog)); sources.Children.Add(Button("Install from ZIP…", PickModule)); stack.Children.Add(sources);
        var defaults = new StackPanel(); defaults.Children.Add(Text("Choose which standard templates appear in New entry.", 11, true));
        var choices = content.All.Where(p => p is not DeclarativeProvider).ToDictionary(p => p.Descriptor.Id, p => Check(defaults, TemplateName(p), !vault.Document!.Settings.DisabledModules.Contains(p.Descriptor.Id)));
        defaults.Children.Add(Button("Save entry types", () =>
        {
            if (choices.Values.All(c => c.IsChecked != true) && installedModules.All(p => vault.Document!.Settings.DisabledModules.Contains(p.Manifest.ContentType))) { ShowError("Keep at least one entry type enabled."); return; }
            if (Change(d => { foreach (var pair in choices) { d.Settings.DisabledModules.Remove(pair.Key); if (pair.Value.IsChecked != true) d.Settings.DisabledModules.Add(pair.Key); } })) ShowModules();
        }));
        stack.Children.Add(GroupedOptions("Default entry types", defaults));
    }

    private void PickModule()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Sable module (*.zip)|*.zip", Title = "Install module", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > ModulePackages.MaxBytes) throw new InvalidDataException("Module ZIPs must be under 256 KB.");
            ReviewModule(ModulePackages.Read(File.ReadAllBytes(dialog.FileName), ModuleTrust.Verify));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException or CryptographicException or NullReferenceException) { ShowError("This module is invalid, unsupported or has an unverifiable signature."); }
    }
    private void ReviewModule(ModulePackage package)
    {
        if (!vault.IsUnlocked) return;
        var current = installedModules.FirstOrDefault(p => p.Manifest.Id == package.Manifest.Id);
        if (current is not null && (current.Fingerprint == package.Fingerprint || Version.Parse(current.Manifest.Version) > Version.Parse(package.Manifest.Version)))
        {
            var installed = Form(package.Manifest.Name, "", "settings", parent: ShowModules);
            installed.Children.Add(Text("✓  Already installed", 14));
            installed.Children.Add(Text("Version " + current.Manifest.Version, 11, true));
            PageAction(Button("Module settings", () => ShowModuleSettings(current))); pageSubmit = null;
            return;
        }
        var stack = Form(current is null ? "Install module" : "Update module", "", "settings", parent: ShowModules);
        stack.Children.Add(Text(package.Manifest.Name, 17));
        stack.Children.Add(Text(package.Manifest.Author + " · " + package.Manifest.Version, 11, true));
        stack.Children.Add(Text(package.Manifest.Handler switch { "rdp" => "Adds Remote Desktop entries using the Windows app.", "putty" => "Adds SSH entries. PuTTY must also be installed on this PC.", _ => package.Manifest.Description }, 12));
        stack.Children.Add(Text(package.Official ? "Verified Sable module" : "Not reviewed by Sable. Install only packages whose purpose you understand.", 12, !package.Official));
        var permission = package.Manifest.Handler switch
        {
            "type" => "Adds clipboard typing and a tray action, usable while locked. Sable handles text only during an explicit operation; the module cannot read vault entries.",
            "rdp" or "putty" => "Sable handles saved passwords. The module cannot read them or other entries.",
            "network-profile" => "Applies saved IPv4 or DNS settings only when you run an entry and Windows grants administrator approval.",
            _ => "Uses only its own fields. Cannot read other entries."
        };
        stack.Children.Add(Text(permission, 11, true));
        var reviewedPage = Body.Content; var startEpoch = epoch;
        PageAction(Button(current is null ? "Install" : "Update", () =>
        {
            if (!vault.IsUnlocked || epoch != startEpoch || !ReferenceEquals(Body.Content, reviewedPage)) return;
            try
            {
                var old = vault.Document!.Items.Where(i => i.ContentType == package.Manifest.ContentType);
                if (old.Any(i => i.ModuleSchema != package.Manifest.SchemaFingerprint())) throw new InvalidDataException("This package does not match the preserved entries' schema.");
                modules.Install(package); ReloadModules(); Audit(AuditEvent.ModuleInstalled); ShowModules(); SetStatus("Module installed");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { ShowError(ex.Message); }
        }, true));
        pageSubmit = null;
    }
    private async void ShowModuleCatalog()
    {
        if (!vault.IsUnlocked) return;
        var stack = Form("GitHub catalog", "", "settings", parent: ShowModules);
        var status = Text("Loading official modules…", 12, true);
        stack.Children.Add(status);
        var page = Body.Content;
        var startEpoch = epoch;
        var token = sessionCancellation.Token;
        bool IsCurrent() => startEpoch == epoch && vault.IsUnlocked && ReferenceEquals(page, Body.Content);
        try
        {
            var bytes = await ModuleTrust.Download(ModuleTrust.CatalogUrl, 64 * 1024, token);
            if (!IsCurrent()) return;
            var catalog = ModuleTrust.ReadCatalog(bytes);
            stack.Children.Remove(status);
            var installedByHash = installedModules.ToLookup(
                p => Convert.ToHexString(SHA256.HashData(p.Archive)), StringComparer.OrdinalIgnoreCase);
            foreach (var entry in catalog)
            {
                var installed = installedByHash[entry.Sha256].FirstOrDefault();
                if (installed is not null)
                {
                    stack.Children.Add(Button(entry.Name + "  ✓", () => ReviewModule(installed)));
                    stack.Children.Add(Text("Already installed · " + installed.Manifest.Version, 11, true));
                    continue;
                }

                var download = Button(entry.Name + "  ↓", () => { });
                download.Click += async (_, _) =>
                {
                    if (!IsCurrent() || !download.IsEnabled) return;
                    download.IsEnabled = false;
                    try
                    {
                        var zip = await ModuleTrust.Download(entry.Url, ModulePackages.MaxBytes, token);
                        if (!IsCurrent()) return;
                        if (!Convert.ToHexString(SHA256.HashData(zip)).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Catalog checksum mismatch.");
                        var package = ModulePackages.Read(zip, ModuleTrust.Verify);
                        if (!package.Official) throw new InvalidDataException("Module signature missing.");
                        ReviewModule(package);
                    }
                    catch (Exception ex) when (IsModuleDownloadFailure(ex))
                    {
                        if (IsCurrent()) ShowError("The module could not be downloaded and verified. Try again.");
                    }
                    finally { download.IsEnabled = true; }
                };
                stack.Children.Add(download);
                stack.Children.Add(Text(entry.Description, 11, true));
            }
        }
        catch (Exception ex) when (IsModuleDownloadFailure(ex))
        {
            if (!IsCurrent()) return;
            status.Text = "The GitHub catalog is unavailable. Retry or install a ZIP you already downloaded.";
            stack.Children.Add(Button("Retry", ShowModuleCatalog));
            stack.Children.Add(Button("Install from ZIP…", PickModule));
        }
    }

    private static bool IsModuleDownloadFailure(Exception ex) =>
        ex is System.Net.Http.HttpRequestException or IOException or InvalidDataException or JsonException or CryptographicException
            or ArgumentException or FormatException or OperationCanceledException;
    private string ModuleSetting(ModulePackage package, string key) => vault.Document!.ExtensionSettings.TryGetValue(package.Manifest.Id, out var settings) && settings.ValueKind == JsonValueKind.Object && settings.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private void ShowModuleSettings(ModulePackage package)
    {
        if (!vault.IsUnlocked) return;
        if (package.Manifest.Handler == "type") { ShowTypeModuleSettings(package); return; }
        var stack = Form(package.Manifest.Name, "", "settings", parent: ShowModules);
        stack.Children.Add(Text(package.Manifest.Version + (package.Official ? " · Verified Sable module" : " · Community module"), 11, true));
        var enabled = Check(stack, "Enabled", !vault.Document!.Settings.DisabledModules.Contains(package.Manifest.ContentType));
        TextBox? executable = null;
        if (package.Manifest.Handler == "putty")
        {
            var configured = ModuleSetting(package, "executable");
            executable = Input(stack, "PuTTY application", configured.Length > 0 ? configured : PuttyBroker.Detect());
            var pathBox = executable;
            stack.Children.Add(Button("Browse…", () =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "PuTTY application (*.exe)|*.exe", CheckFileExists = true };
                if (dialog.ShowDialog(this) == true) pathBox.Text = dialog.FileName;
            }));
            if (File.Exists(pathBox.Text)) stack.Children.Add(Text("✓  PuTTY found on this PC", 11, true));
            else stack.Children.Add(Button("Download PuTTY ↗", () => Process.Start(new ProcessStartInfo(PuttyBroker.DownloadUrl) { UseShellExecute = true })));
            stack.Children.Add(Text("Use the official signed PuTTY release. Password authentication is supported; SSH keys follow later.", 11, true));
        }
        stack.Children.Add(Button("Reset remembered module permissions", () => { moduleApprovals.Clear(); Audit(AuditEvent.ModulePermissionsReset); SetStatus("Module operations will ask again"); }));
        var armed = false; Button? remove = null;
        remove = Button("Uninstall module", () =>
        {
            if (!armed) { armed = true; remove!.Content = "Confirm uninstall"; return; }
            try { modules.Remove(package); ReloadModules(); Audit(AuditEvent.ModuleRemoved); ShowModules(); SetStatus("Module removed · encrypted entries preserved"); }
            catch (IOException) { ShowError("The module could not be removed."); }
        }); stack.Children.Add(remove);
        SettingsSave(stack, Button("Save settings", () =>
        {
            try
            {
                var path = executable?.Text.Trim() ?? "";
                if (path.Length > 0) { using var verified = new VerifiedPutty(path); }
                if (Change(d =>
                {
                    d.Settings.DisabledModules.Remove(package.Manifest.ContentType);
                    if (enabled.IsChecked != true) d.Settings.DisabledModules.Add(package.Manifest.ContentType);
                    d.ExtensionSettings[package.Manifest.Id] = JsonSerializer.SerializeToElement(new { executable = path });
                })) ShowModules();
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or CryptographicException or System.ComponentModel.Win32Exception) { ShowError(ex.Message); }
        }, true));
    }
    private void ShowModuleDetails(VaultItem item)
    {
        if (!vault.IsUnlocked || content.Find(item.ContentType) is not DeclarativeProvider provider || !ModuleAvailable(item) || vault.Document!.Settings.DisabledModules.Contains(item.ContentType)) return;
        var stack = Form(item.Label, "", "module-details");
        stack.Children.Add(Text(provider.Descriptor.Name, 11, true));
        if (provider.Package.Manifest.Handler == "rdp")
        {
            var profile = RdpProfile.From(item);
            stack.Children.Add(Text(profile.Host + ":" + profile.Port, 15));
            stack.Children.Add(Text(profile.Username, 12, true));
            stack.Children.Add(Text("Password  ••••••••", 12, true));
            stack.Children.Add(AsyncButton("Copy password", () => CopyEntry(item, "Password", new Button()), false));
            var actions = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
            actions.Children.Add(Button("Launch", () => RunRdp(item, false), true));
            actions.Children.Add(Button("Launch & copy", () => RunRdp(item, true)));
            PageAction(actions); pageSubmit = null;
        }
        else
        if (provider.Package.Manifest.Handler == "putty")
        {
            var profile = PuttyProfile.From(item);
            stack.Children.Add(Text(profile.Username + "@" + profile.Host + ":" + profile.Port, 15));
            stack.Children.Add(Text("Password  ••••••••", 12, true));
            stack.Children.Add(Text(profile.HostKey.Length > 0 ? "Expected server fingerprint\n" + profile.HostKey : "PuTTY will ask you to verify unknown server identities.", 11, true));
            PageAction(Button("Run PuTTY", () => RunPutty(item), true)); pageSubmit = null;
        }
        else
        {
            foreach (var field in provider.Package.Manifest.Fields)
            {
                stack.Children.Add(Text(field.Label, 11, true));
                stack.Children.Add(Text(field.Kind == "secret" || item.IsSensitive ? "••••••••" : item.Field(field.Key), 13));
            }
            foreach (var action in provider.CopyActions)
                stack.Children.Add(Button(action, () => ReviewModuleCopy(item, action)));
        }
        stack.Children.Add(Button("Edit entry", () => ShowEditor(item)));
    }
    private void ReviewModuleCopy(VaultItem item, string action)
    {
        if (!vault.IsUnlocked || content.Find(item.ContentType) is not DeclarativeProvider provider || !ModuleAvailable(item) || vault.Document!.Settings.DisabledModules.Contains(item.ContentType)) return;
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(provider.Package.Fingerprint + item.Id + action)));
        async void Copy() { if (vault.IsUnlocked && !busy) await CopyEntry(item, action, new Button()); }
        if (moduleApprovals.Contains(digest)) { Copy(); return; }
        var stack = Form("Allow copy", "", "module-permission", parent: () => ShowModuleDetails(item));
        stack.Children.Add(Text("Allow Sable to copy this entry's selected field to the clipboard?", 13));
        if (item.IsSensitive) stack.Children.Add(Text("This entry is marked sensitive. Clipboard cleanup rules will apply.", 11, true));
        var remember = Check(stack, "Don't ask again for this entry and action", false);
        var page = Body.Content; var startEpoch = epoch;
        PageAction(Button("Copy", () =>
        {
            if (epoch != startEpoch || !vault.IsUnlocked || !ReferenceEquals(page, Body.Content)) return;
            if (remember.IsChecked == true) moduleApprovals.Remember(digest);
            Copy();
        }, true)); pageSubmit = null;
    }
}
