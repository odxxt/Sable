using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private FrameworkElement BuildRdpFields(DeclarativeProvider provider, VaultItem? existing, Action<VaultItem> save)
    {
        var stack = new StackPanel();
        var label = Input(stack, "Name", editorIsNew ? editorSharedLabel : existing?.Label ?? "");
        var host = Input(stack, "Host", existing?.Field("host") ?? "");
        var username = Input(stack, "Username", existing?.Field("username") ?? "");
        stack.Children.Add(Text("Password", 11, true));
        var secretHost = new Grid { Margin = new Thickness(0, 0, 0, 13) }; stack.Children.Add(secretHost);
        var password = new PasswordBox { Password = existing?.Field("password") ?? "", MaxLength = 1024, MinHeight = 36, Padding = new Thickness(10, 8, 38, 8) };
        var visible = new TextBox { Visibility = Visibility.Collapsed, MaxLength = 1024, IsUndoEnabled = false, MinHeight = 36, Padding = new Thickness(10, 8, 38, 8) };
        System.Windows.Automation.AutomationProperties.SetName(password, "RDP password"); System.Windows.Automation.AutomationProperties.SetName(visible, "RDP password revealed");
        secretHost.Children.Add(password); secretHost.Children.Add(visible); eraseEditors.Add(password.Clear); eraseEditors.Add(visible.Clear);
        var graphic = new Grid { Width = 16, Height = 16, IsHitTestVisible = false };
        graphic.Children.Add(new TextBlock { Text = "\uE890", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 });
        var slash = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 3,13 L 13,3"), Stroke = Brushes.White, StrokeThickness = 1.35, Visibility = Visibility.Collapsed }; graphic.Children.Add(slash);
        var eye = Button("", () =>
        {
            if (visible.Visibility == Visibility.Visible) { password.Password = visible.Text; visible.Clear(); visible.Visibility = Visibility.Collapsed; password.Visibility = Visibility.Visible; slash.Visibility = Visibility.Collapsed; }
            else { visible.Text = password.Password; password.Clear(); password.Visibility = Visibility.Collapsed; visible.Visibility = Visibility.Visible; slash.Visibility = Visibility.Visible; }
        });
        eye.Content = graphic; eye.Style = (Style)FindResource("RowIcon"); eye.HorizontalAlignment = HorizontalAlignment.Right; eye.VerticalAlignment = VerticalAlignment.Top; eye.Opacity = .55; eye.ToolTip = "Reveal or hide password"; secretHost.Children.Add(eye);
        var advancedFields = new StackPanel { Visibility = advancedEditor ? Visibility.Visible : Visibility.Collapsed }; stack.Children.Add(advancedFields);
        toggleAdvancedEditor = () => { advancedEditor = !advancedEditor; advancedFields.Visibility = advancedEditor ? Visibility.Visible : Visibility.Collapsed; };
        string Value(string key, string fallback) => existing?.Field(key) is { Length: > 0 } value ? value : fallback;
        var domain = Input(advancedFields, "Domain · optional", Value("domain", ""));
        var port = Input(advancedFields, "Port", Value("port", "3389"));
        var width = Input(advancedFields, "Desktop width", Value("width", "1280"));
        var height = Input(advancedFields, "Desktop height", Value("height", "800"));
        var clipboard = Check(advancedFields, "Share clipboard with remote computer", Value("clipboard", "false") == "true");
        advancedFields.Children.Add(Text("Clipboard sharing exposes your local clipboard to the remote session. Drives, printers and devices stay disconnected.", 11, true));
        string ReadPassword() => visible.Visibility == Visibility.Visible ? visible.Text : password.Password;
        VaultItem Capture()
        {
            var item = existing is null ? new VaultItem { ContentType = provider.Descriptor.Id, FolderId = folder, Order = vault.Document!.Items.Count } : Codec.Clone(existing);
            item.Label = label.Text.Trim(); item.IsSensitive = true; item.ModuleSchema = provider.Package.Manifest.SchemaFingerprint();
            item.Payload = JsonSerializer.SerializeToElement(new Dictionary<string, string>
            {
                ["host"] = host.Text.Trim(), ["username"] = username.Text.Trim(), ["password"] = ReadPassword(), ["domain"] = domain.Text.Trim(), ["port"] = port.Text.Trim(),
                ["width"] = width.Text.Trim(), ["height"] = height.Text.Trim(), ["clipboard"] = clipboard.IsChecked == true ? "true" : "false"
            }); return item;
        }
        captureEditorDraft = Capture;
        void RejectAdvanced(TextBox input, string message) { advancedFields.Visibility = Visibility.Visible; input.BringIntoView(); RejectInput(input, message); }
        PageAction(Button("Save entry", () =>
        {
            if (!vault.IsUnlocked) return;
            if (label.Text.Trim().Length is < 1 or > 300) { RejectInput(label, "Use a name from 1 to 300 characters."); return; }
            if (Uri.CheckHostName(host.Text.Trim()) == UriHostNameType.Unknown) { RejectInput(host, "Enter a host name or IP address without a port or URL."); return; }
            if (username.Text.Trim().Length is < 1 or > 256 || username.Text.Any(char.IsControl)) { RejectInput(username, "Enter a Windows username, DOMAIN\\user or user@domain."); return; }
            var secret = ReadPassword();
            if (secret.Length == 0 || secret.Contains('\0') || Encoding.UTF8.GetByteCount(secret) > 4096) { RejectInput(visible.Visibility == Visibility.Visible ? visible : password, "Enter a password of up to 4,096 UTF-8 bytes."); return; }
            if (!int.TryParse(port.Text, out var p) || p is < 1 or > 65535) { RejectAdvanced(port, "Use a port from 1 to 65535."); return; }
            if (!int.TryParse(width.Text, out var w) || w is < 640 or > 3840) { RejectAdvanced(width, "Use a width from 640 to 3840."); return; }
            if (!int.TryParse(height.Text, out var h) || h is < 480 or > 2160) { RejectAdvanced(height, "Use a height from 480 to 2160."); return; }
            try { var item = Capture(); RdpProfile.From(item).Validate(); save(item); } catch (InvalidDataException ex) { RejectAdvanced(domain, ex.Message); }
        }, true));
        Dispatcher.BeginInvoke(() => label.Focus()); return stack;
    }
    private void RunRdp(VaultItem item)
    {
        if (!vault.IsUnlocked || busy || moduleConnecting || content.Find(item.ContentType) is not DeclarativeProvider provider || !ModuleAvailable(item) || vault.Document!.Settings.DisabledModules.Contains(item.ContentType)) return;
        var current = vault.Document.Items.FirstOrDefault(i => i.Id == item.Id && i.DeletedAt is null); if (current is null) return; item = current;
        try
        {
            var profile = RdpProfile.From(item); string hash;
            using (var executable = new RdpExecutable()) hash = executable.Hash;
            var digest = profile.Fingerprint(provider.Package.Fingerprint, hash, item.Id); var startEpoch = epoch;
            async Task Launch(bool remember)
            {
                if (!vault.IsUnlocked || epoch != startEpoch || moduleConnecting || busy) return;
                moduleConnecting = true; modalDepth++;
                try
                {
                    var latest = vault.Document!.Items.FirstOrDefault(i => i.Id == item.Id && i.DeletedAt is null);
                    if (latest is null || !ModuleAvailable(latest) || vault.Document.Settings.DisabledModules.Contains(latest.ContentType)) throw new InvalidOperationException("This entry or module is no longer available.");
                    using var executable = new RdpExecutable();
                    if (RdpProfile.From(latest).Fingerprint(provider.Package.Fingerprint, executable.Hash, latest.Id) != digest) throw new InvalidOperationException("The connection changed. Review it again.");
                    var secret = SecretBuffers.Utf8(latest, "password");
                    try { SetStatus("Opening Remote Desktop…"); await RdpBroker.Connect(executable, profile, secret, sessionCancellation.Token); }
                    finally { CryptographicOperations.ZeroMemory(secret); }
                    if (epoch != startEpoch || !vault.IsUnlocked) return;
                    if (remember) moduleApprovals.Remember(digest);
                    Audit(AuditEvent.EntryActionStarted); ShowList(); SetStatus("Remote Desktop opened"); if (!session.IsPinned) Hide();
                }
                catch (OperationCanceledException) { if (epoch == startEpoch && vault.IsUnlocked) ShowError("The RDP handoff timed out or was cancelled. Try again."); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or CryptographicException or System.ComponentModel.Win32Exception)
                { if (epoch == startEpoch && vault.IsUnlocked) ShowError(ex is InvalidOperationException ? ex.Message : "The RDP window could not receive this connection. Check your Windows Remote Desktop components."); }
                finally { moduleConnecting = false; modalDepth--; }
            }
            if (moduleApprovals.Contains(digest)) { _ = Launch(false); return; }
            var stack = Form("Connect with Remote Desktop", "", "module-permission", parent: () => ShowModuleDetails(item));
            stack.Children.Add(Text(profile.Host + ":" + profile.Port, 16));
            stack.Children.Add(Text(profile.Domain.Length > 0 ? profile.Domain + "\\" + profile.Username : profile.Username, 12, true));
            stack.Children.Add(Text("SableLatch will send this entry's password to its local RDP window and authenticate with this computer.", 12, true));
            stack.Children.Add(Text("Use a trusted server certificate and matching host name. Sessions stay open when the vault locks. Close the RDP window to disconnect.", 11, true));
            if (profile.Clipboard) stack.Children.Add(Text("Clipboard sharing is enabled for this connection.", 12, true));
            if (!provider.Package.Official) stack.Children.Add(Text("Community module · not reviewed by SableLatch", 11, true));
            var remember = Check(stack, "Don't ask again for this connection", true); var page = Body.Content;
            PageAction(AsyncButton("Connect", async () => { if (ReferenceEquals(page, Body.Content) && epoch == startEpoch) await Launch(remember.IsChecked == true); })); pageSubmit = null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or CryptographicException or System.ComponentModel.Win32Exception) { ShowError(ex.Message); }
    }
}
