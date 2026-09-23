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
            extensions.RegisterEditor(new FieldEditorFactory(provider.Descriptor.Id, (item, save, _) => package.Manifest.Handler == "putty" ? BuildPuttyFields(provider, item, save) : BuildFields(provider, item, save)));
        }
    }
    private bool ModuleAvailable(VaultItem item) => content.Find(item.ContentType) is not DeclarativeProvider provider || item.ModuleSchema == provider.Package.Manifest.SchemaFingerprint();
    private void ShowMoreTypes()
    {
        if (!vault.IsUnlocked) return;
        if (captureEditorDraft is not null) { var draft = captureEditorDraft(); editorDrafts[draft.ContentType] = draft; }
        var stack = Form("More types", "", "editor", parent: () => ShowEditor());
        foreach (var provider in content.All.OfType<DeclarativeProvider>().Where(p => !vault.Document!.Settings.DisabledModules.Contains(p.Descriptor.Id)))
            stack.Children.Add(Button(provider.Descriptor.Name + "  ›", () => ShowEditor(selectedType: provider.Descriptor.Id)));
        stack.Children.Add(Button("Install a module  ›", ShowModules));
    }
    private void ShowPluginManager()
    {
        if (!vault.IsUnlocked) return;
        var stack = Form("Plugins & modules", "", "settings", parent: ShowSettings);
        var top = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        top.Children.Add(Button("Install ZIP", PickModule)); top.Children.Add(Button("Catalog", ShowModuleCatalog)); stack.Children.Add(top);
        if (rejectedModules > 0) stack.Children.Add(Text($"{rejectedModules} invalid package(s) were blocked.", 11, true));
        foreach (var package in installedModules)
        {
            stack.Children.Add(Button(package.Manifest.Name + "  ›", () => ShowModuleSettings(package)));
            stack.Children.Add(Text(package.Official ? "SableLatch · verified signature" : "Community · not reviewed by SableLatch", 10, true));
        }
        if (installedModules.Count == 0) stack.Children.Add(Text("Add entry types and actions. Modules run only when you use them.", 12, true));
        stack.Children.Add(Text("Built-in types", 11, true));
        var choices = content.All.Where(p => p is not DeclarativeProvider).ToDictionary(p => p.Descriptor.Id, p => Check(stack, p.Descriptor.Name, !vault.Document!.Settings.DisabledModules.Contains(p.Descriptor.Id)));
        SettingsSave(stack, Button("Save module settings", () =>
        {
            if (Change(d => { foreach (var pair in choices) { d.Settings.DisabledModules.Remove(pair.Key); if (pair.Value.IsChecked != true) d.Settings.DisabledModules.Add(pair.Key); } })) ShowSettings();
        }, true));
    }
    private void PickModule()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "SableLatch module (*.zip)|*.zip", Title = "Install module", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > ModulePackages.MaxBytes) throw new InvalidDataException("Module ZIPs must be under 256 KB.");
            ReviewModule(ModulePackages.Read(File.ReadAllBytes(dialog.FileName), ModuleTrust.Verify));
        }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or CryptographicException or NullReferenceException) { ShowError("This module is invalid, unsupported or has an unverifiable signature."); }
    }
    private void ReviewModule(ModulePackage package)
    {
        if (!vault.IsUnlocked) return;
        var stack = Form("Install module", "", "settings", parent: ShowModules);
        stack.Children.Add(Text(package.Manifest.Name, 17));
        stack.Children.Add(Text(package.Manifest.Author + " · " + package.Manifest.Version, 11, true));
        stack.Children.Add(Text(package.Manifest.Description, 12));
        stack.Children.Add(Text(package.Official ? "Verified SableLatch module" : "Not reviewed by SableLatch. Install only packages whose purpose you understand.", 12, !package.Official));
        stack.Children.Add(Text(package.Manifest.Handler == "putty" ? "Can request a PuTTY connection to the host you configure. Passwords stay in SableLatch's protected launcher." : "Can define fields and request copying of its own non-secret fields. It cannot read other entries.", 12, true));
        stack.Children.Add(Text("No executable code, background activity or direct internet access.", 11, true));
        var reviewedPage = Body.Content; var startEpoch = epoch;
        PageAction(Button("Install", () =>
        {
            if (!vault.IsUnlocked || epoch != startEpoch || !ReferenceEquals(Body.Content, reviewedPage)) return;
            try
            {
                var old = vault.Document!.Items.Where(i => i.ContentType == package.Manifest.ContentType);
                if (old.Any(i => i.ModuleSchema != package.Manifest.SchemaFingerprint())) throw new InvalidDataException("This package does not match the preserved entries' schema.");
                modules.Install(package); ReloadModules(); Audit(AuditEvent.ModuleInstalled); ShowModules(); SetStatus("Module installed");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { ShowError(ex.Message); }
        }, true));
        pageSubmit = null;
    }
    private void ShowModuleCatalog()
    {
        if (!vault.IsUnlocked) return;
        var stack = Form("Module catalog", "", "settings", parent: ShowModules);
        stack.Children.Add(Text("Official modules", 12, true));
        stack.Children.Add(Button("PuTTY SSH  ›", () => ReviewModule(ModulePackages.Read(ModuleTrust.Resource("putty.zip"), ModuleTrust.Verify))));
        stack.Children.Add(Text("Included with this app. Installs without a download.", 11, true));
        var page = Body.Content; var startEpoch = epoch;
        stack.Children.Add(AsyncButton("Check GitHub catalog", async () =>
        {
            try
            {
                var bytes = await ModuleTrust.Download(ModuleTrust.CatalogUrl, 64 * 1024, sessionCancellation.Token);
                if (startEpoch != epoch || !ReferenceEquals(page, Body.Content)) return;
                var catalog = ModuleTrust.ReadCatalog(bytes);
                var list = Form("GitHub catalog", "", "settings", parent: ShowModuleCatalog);
                foreach (var entry in catalog)
                {
                    var catalogPage = Body.Content;
                    list.Children.Add(AsyncButton(entry.Name + "  ↓", async () =>
                    {
                        try
                        {
                            var zip = await ModuleTrust.Download(entry.Url, ModulePackages.MaxBytes, sessionCancellation.Token);
                            if (startEpoch != epoch || !ReferenceEquals(catalogPage, Body.Content)) return;
                            if (!Convert.ToHexString(SHA256.HashData(zip)).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException();
                            var package = ModulePackages.Read(zip, ModuleTrust.Verify);
                            if (!package.Official) throw new InvalidDataException();
                            ReviewModule(package);
                        }
                        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or IOException or JsonException or CryptographicException or ArgumentException or OperationCanceledException) { if (startEpoch == epoch && vault.IsUnlocked) ShowError("The module could not be downloaded and verified."); }
                    }, false));
                    list.Children.Add(Text(entry.Description, 11, true));
                }
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or IOException or JsonException or CryptographicException or FormatException or OperationCanceledException)
            { if (startEpoch == epoch && vault.IsUnlocked && ReferenceEquals(page, Body.Content)) ShowError("The signed GitHub catalog is unavailable. You can still install the included PuTTY module or a local ZIP."); }
        }, false));
    }
    private string ModuleSetting(ModulePackage package, string key) => vault.Document!.ExtensionSettings.TryGetValue(package.Manifest.Id, out var settings) && settings.ValueKind == JsonValueKind.Object && settings.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private void ShowModuleSettings(ModulePackage package)
    {
        if (!vault.IsUnlocked) return;
        var stack = Form(package.Manifest.Name, "", "settings", parent: ShowModules);
        stack.Children.Add(Text(package.Manifest.Version + (package.Official ? " · Verified SableLatch module" : " · Community module"), 11, true));
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
            stack.Children.Add(Button("Download PuTTY ↗", () => Process.Start(new ProcessStartInfo(PuttyBroker.DownloadUrl) { UseShellExecute = true })));
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
        stack.Children.Add(Text("Allow SableLatch to copy this entry's selected field to the clipboard?", 13));
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
