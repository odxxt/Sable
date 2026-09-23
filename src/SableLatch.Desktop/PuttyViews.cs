using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private FrameworkElement BuildPuttyFields(DeclarativeProvider provider, VaultItem? existing, Action<VaultItem> save)
    {
        var stack = new StackPanel();
        var label = Input(stack, "Name", existing?.Label ?? "");
        var host = Input(stack, "Host", existing?.Field("host") ?? "");
        var username = Input(stack, "Username", existing?.Field("username") ?? "");
        stack.Children.Add(Text("Password", 11, true));
        var secretHost = new Grid { Margin = new Thickness(0, 0, 0, 13) }; stack.Children.Add(secretHost);
        var password = new PasswordBox { Password = existing?.Field("password") ?? "", MaxLength = 1024, MinHeight = 36, Padding = new Thickness(10, 8, 38, 8) };
        var visible = new TextBox { Visibility = Visibility.Collapsed, MaxLength = 1024, IsUndoEnabled = false, MinHeight = 36, Padding = new Thickness(10, 8, 38, 8) };
        System.Windows.Automation.AutomationProperties.SetName(password, "SSH password");
        System.Windows.Automation.AutomationProperties.SetName(visible, "SSH password revealed");
        secretHost.Children.Add(password); secretHost.Children.Add(visible); eraseEditors.Add(password.Clear); eraseEditors.Add(visible.Clear);
        var eyeGraphic = new Grid { Width = 16, Height = 16, IsHitTestVisible = false };
        eyeGraphic.Children.Add(new TextBlock { Text = "\uE890", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 });
        var slash = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 3,13 L 13,3"), Stroke = Brushes.White, StrokeThickness = 1.35, Visibility = Visibility.Collapsed }; eyeGraphic.Children.Add(slash);
        var eye = Button("", () =>
        {
            if (visible.Visibility == Visibility.Visible) { password.Password = visible.Text; visible.Clear(); visible.Visibility = Visibility.Collapsed; password.Visibility = Visibility.Visible; slash.Visibility = Visibility.Collapsed; }
            else { visible.Text = password.Password; password.Clear(); password.Visibility = Visibility.Collapsed; visible.Visibility = Visibility.Visible; slash.Visibility = Visibility.Visible; }
        });
        eye.Content = eyeGraphic; eye.Style = (Style)FindResource("RowIcon"); eye.HorizontalAlignment = HorizontalAlignment.Right; eye.VerticalAlignment = VerticalAlignment.Top; eye.Opacity = .55; eye.ToolTip = "Reveal or hide password";
        secretHost.Children.Add(eye);
        var advancedFields = new StackPanel { Visibility = advancedEditor ? Visibility.Visible : Visibility.Collapsed };
        stack.Children.Add(advancedFields);
        toggleAdvancedEditor = () => { advancedEditor = !advancedEditor; advancedFields.Visibility = advancedEditor ? Visibility.Visible : Visibility.Collapsed; };
        var port = Input(advancedFields, "Port", existing?.Field("port") is { Length: > 0 } portValue ? portValue : "22");
        var fingerprint = Input(advancedFields, "Expected server fingerprint · optional", existing?.Field("hostKey") ?? "");
        var family = Select(advancedFields, "Network", new[] { "Automatic", "IPv4", "IPv6" }, existing?.Field("addressFamily") switch { "ipv4" => "IPv4", "ipv6" => "IPv6", _ => "Automatic" });
        var compression = Check(advancedFields, "Compress SSH traffic", existing?.Field("compression") == "true");
        var keepalive = Input(advancedFields, "Keepalive seconds · 0 disables", existing?.Field("keepalive") is { Length: > 0 } ping ? ping : "30");
        var font = Select(advancedFields, "Terminal font", new[] { "Consolas", "Cascadia Mono", "Courier New" }, existing?.Field("font") is { Length: > 0 } face ? face : "Consolas");
        var fontSize = Input(advancedFields, "Terminal text size", existing?.Field("fontSize") is { Length: > 0 } size ? size : "11");
        advancedFields.Children.Add(Text("Password authentication. SSH keys and additional connection options follow in a later module update.", 11, true));
        string ReadPassword() => visible.Visibility == Visibility.Visible ? visible.Text : password.Password;
        VaultItem Capture()
        {
            var item = existing is null ? new VaultItem { ContentType = provider.Descriptor.Id, FolderId = folder, Order = vault.Document!.Items.Count } : Codec.Clone(existing);
            item.Label = label.Text.Trim(); item.IsSensitive = true; item.ModuleSchema = provider.Package.Manifest.SchemaFingerprint();
            item.Payload = JsonSerializer.SerializeToElement(new Dictionary<string, string>
            {
                ["host"] = host.Text.Trim(), ["username"] = username.Text.Trim(), ["password"] = ReadPassword(), ["port"] = port.Text.Trim(),
                ["hostKey"] = fingerprint.Text.Trim(), ["addressFamily"] = family.SelectedItem as string == "IPv4" ? "ipv4" : family.SelectedItem as string == "IPv6" ? "ipv6" : "auto", ["compression"] = compression.IsChecked == true ? "true" : "false",
                ["keepalive"] = keepalive.Text.Trim(), ["font"] = (string)font.SelectedItem, ["fontSize"] = fontSize.Text.Trim()
            }); return item;
        }
        captureEditorDraft = Capture;
        PageAction(Button("Save entry", () =>
        {
            if (!vault.IsUnlocked) return;
            if (label.Text.Trim().Length is < 1 or > 300) { RejectInput(label, "Use a name from 1 to 300 characters."); return; }
            if (Uri.CheckHostName(host.Text.Trim()) == UriHostNameType.Unknown) { RejectInput(host, "Enter a valid host name or IP address."); return; }
            if (username.Text.Trim().Length is < 1 or > 128 || username.Text.Any(char.IsWhiteSpace)) { RejectInput(username, "Enter a username without spaces."); return; }
            var secret = ReadPassword();
            if (secret.Length == 0 || secret.IndexOfAny(['\r', '\n', '\0']) >= 0 || Encoding.UTF8.GetByteCount(secret) > 4096) { RejectInput(password, "Enter a single-line password."); return; }
            if (!int.TryParse(port.Text, out var number) || number is < 1 or > 65535) { advancedFields.Visibility = Visibility.Visible; port.BringIntoView(); RejectInput(port, "Use a port from 1 to 65535."); return; }
            if (!int.TryParse(keepalive.Text, out var seconds) || seconds is < 0 or > 600) { advancedFields.Visibility = Visibility.Visible; keepalive.BringIntoView(); RejectInput(keepalive, "Use 0 to 600 seconds."); return; }
            if (!int.TryParse(fontSize.Text, out var points) || points is < 8 or > 24) { advancedFields.Visibility = Visibility.Visible; fontSize.BringIntoView(); RejectInput(fontSize, "Use a text size from 8 to 24."); return; }
            try { var item = Capture(); PuttyProfile.From(item).Validate(); save(item); }
            catch (InvalidDataException ex) { RejectInput(fingerprint, ex.Message); }
        }, true));
        Dispatcher.BeginInvoke(() => label.Focus()); return stack;
    }
    private void RunPutty(VaultItem item)
    {
        if (!vault.IsUnlocked || busy || moduleConnecting || content.Find(item.ContentType) is not DeclarativeProvider provider || !ModuleAvailable(item) || vault.Document!.Settings.DisabledModules.Contains(item.ContentType)) return;
        var current = vault.Document.Items.FirstOrDefault(i => i.Id == item.Id && i.DeletedAt is null);
        if (current is null) return;
        item = current;
        try
        {
            var profile = PuttyProfile.From(item);
            var configured = ModuleSetting(provider.Package, "executable");
            var path = configured.Length > 0 ? configured : PuttyBroker.Detect();
            if (path.Length == 0) { ShowModuleSettings(provider.Package); ShowError("Install PuTTY first, then choose its application here."); return; }
            string hash;
            using (var executable = new VerifiedPutty(path)) hash = executable.Hash;
            var digest = profile.Fingerprint(provider.Package.Fingerprint, hash, item.Id);
            var startEpoch = epoch;
            async Task Launch(bool remember)
            {
                if (!vault.IsUnlocked || epoch != startEpoch || moduleConnecting || busy) return;
                moduleConnecting = true;
                try
                {
                    var latest = vault.Document!.Items.FirstOrDefault(i => i.Id == item.Id && i.DeletedAt is null);
                    if (latest is null || !ModuleAvailable(latest) || vault.Document.Settings.DisabledModules.Contains(latest.ContentType)) throw new InvalidOperationException("This entry or module is no longer available.");
                    using var verified = new VerifiedPutty(path);
                    if (PuttyProfile.From(latest).Fingerprint(provider.Package.Fingerprint, verified.Hash, latest.Id) != digest) throw new InvalidOperationException("The connection changed. Review it again.");
                    var secret = SecretBuffers.Utf8(latest, "password");
                    SetStatus("Opening PuTTY…");
                    try { await PuttyBroker.Connect(verified, profile, secret, sessionCancellation.Token); }
                    finally { CryptographicOperations.ZeroMemory(secret); }
                    if (epoch != startEpoch || !vault.IsUnlocked) return;
                    if (remember) moduleApprovals.Remember(digest);
                    Audit(AuditEvent.EntryActionStarted); ShowList(); SetStatus("PuTTY opened"); if (!session.IsPinned) Hide();
                }
                catch (OperationCanceledException) { if (epoch == startEpoch && vault.IsUnlocked) ShowError("The password handoff timed out or was cancelled. Try again."); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or CryptographicException or System.ComponentModel.Win32Exception)
                { if (epoch == startEpoch && vault.IsUnlocked) ShowError(ex is InvalidOperationException ? ex.Message : "PuTTY could not complete the protected password handoff."); }
                finally { moduleConnecting = false; }
            }
            if (moduleApprovals.Contains(digest)) { _ = Launch(false); return; }
            var stack = Form("Connect with PuTTY", "", "module-permission", parent: () => ShowModuleDetails(item));
            stack.Children.Add(Text(profile.Username + "@" + profile.Host + ":" + profile.Port, 16));
            stack.Children.Add(Text("SableLatch will deliver this entry's password only to the verified PuTTY process it starts.", 12, true));
            stack.Children.Add(Text("Confirm the SSH server identity in PuTTY before authenticating. Existing SSH sessions remain open when SableLatch locks.", 11, true));
            if (!provider.Package.Official) stack.Children.Add(Text("Community module · not reviewed by SableLatch", 11, true));
            var remember = Check(stack, "Don't ask again for this connection", false);
            var reviewPage = Body.Content;
            PageAction(AsyncButton("Connect", async () => { if (ReferenceEquals(reviewPage, Body.Content) && epoch == startEpoch) await Launch(remember.IsChecked == true); }));
            pageSubmit = null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or CryptographicException or System.ComponentModel.Win32Exception) { ShowError(ex.Message); }
    }
}
