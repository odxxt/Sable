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
        var fullScreen = Check(advancedFields, "Start in full screen", Value("fullScreen", "true") != "false");
        stack.Children.Add(Text("When clicked", 11, true));
        var modes = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        var group = "rdp-mode-" + Guid.NewGuid().ToString("N");
        var launchOnly = new RadioButton { Content = "Launch", GroupName = group, Style = (Style)FindResource("SegmentChoice"), IsChecked = Value("copyOnLaunch", "false") != "true" };
        var launchCopy = new RadioButton { Content = "Launch & copy", GroupName = group, Style = (Style)FindResource("SegmentChoice"), IsChecked = Value("copyOnLaunch", "false") == "true" };
        modes.Children.Add(launchOnly); modes.Children.Add(launchCopy);
        stack.Children.Add(new Border { Background = (Brush)FindResource("ControlSurface"), CornerRadius = new CornerRadius(10), Padding = new Thickness(3), Margin = new Thickness(0, 0, 0, 8), Child = modes });
        stack.Children.Add(Text("Click the shortcut to open Remote Desktop. Launch & copy also copies its password.", 11, true));
        string ReadPassword() => visible.Visibility == Visibility.Visible ? visible.Text : password.Password;
        VaultItem Capture()
        {
            var item = existing is null ? new VaultItem { ContentType = provider.Descriptor.Id, FolderId = folder, Order = vault.Document!.Items.Count } : Codec.Clone(existing);
            item.Label = label.Text.Trim(); item.IsSensitive = true; item.ModuleSchema = provider.Package.Manifest.SchemaFingerprint();
            item.Payload = JsonSerializer.SerializeToElement(new Dictionary<string, string>
            {
                ["host"] = host.Text.Trim(), ["username"] = username.Text.Trim(), ["password"] = ReadPassword(), ["domain"] = domain.Text.Trim(), ["port"] = port.Text.Trim(),
                ["fullScreen"] = fullScreen.IsChecked == true ? "true" : "false", ["copyOnLaunch"] = launchCopy.IsChecked == true ? "true" : "false"
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
            try { var item = Capture(); RdpProfile.From(item).Validate(); save(item); } catch (InvalidDataException ex) { RejectAdvanced(domain, ex.Message); }
        }, true));
        Dispatcher.BeginInvoke(() => label.Focus()); return stack;
    }
#if DEBUG
    private Func<RdpProfile, Task>? previewRdpLaunch;
#endif
    private void RunRdp(VaultItem item, bool? copyOnLaunch = null)
    {
        if (!vault.IsUnlocked || busy || moduleConnecting || content.Find(item.ContentType) is not DeclarativeProvider provider || !ModuleAvailable(item) || vault.Document!.Settings.DisabledModules.Contains(item.ContentType)) return;
        var current = vault.Document.Items.FirstOrDefault(i => i.Id == item.Id && i.DeletedAt is null); if (current is null) return; item = current;
        try
        {
            var profile = RdpProfile.From(item); var copy = copyOnLaunch ?? profile.CopyOnLaunch; string hash;
            using (var executable = new NativeRdpExecutable()) hash = executable.Hash;
            var digest = profile.Fingerprint(provider.Package.Fingerprint + ":native-dpapi-v3-native-resources:" + copy, hash, item.Id); var startEpoch = epoch;
            async Task Launch(bool remember)
            {
                if (!vault.IsUnlocked || epoch != startEpoch || moduleConnecting || busy) return;
                moduleConnecting = true; modalDepth++;
                try
                {
                    var latest = vault.Document!.Items.FirstOrDefault(i => i.Id == item.Id && i.DeletedAt is null);
                    if (latest is null || !ModuleAvailable(latest) || vault.Document.Settings.DisabledModules.Contains(latest.ContentType)) throw new InvalidOperationException("This entry or module is no longer available.");
                    using var executable = new NativeRdpExecutable();
                    if (RdpProfile.From(latest).Fingerprint(provider.Package.Fingerprint + ":native-dpapi-v3-native-resources:" + copy, executable.Hash, latest.Id) != digest) throw new InvalidOperationException("The connection changed. Review it again.");
                    var secret = SecretBuffers.Utf8(latest, "password");
                    try
                    {
                        SetStatus("Opening Remote Desktop…");
#if DEBUG
                        if (App.PreviewMode)
                        {
                            if (previewRdpLaunch is null) throw new InvalidOperationException("RDP launch is disabled in this preview fixture.");
                            await previewRdpLaunch(profile);
                        }
                        else
#endif
                        await NativeRdpLauncher.Connect(executable, NativeRdpLauncher.DirectoryFor(App.DataDirectory), profile, secret, sessionCancellation.Token);
                    }
                    finally { CryptographicOperations.ZeroMemory(secret); }
                    if (epoch != startEpoch || !vault.IsUnlocked) return;
                    if (remember) moduleApprovals.Remember(digest);
                    Audit(AuditEvent.EntryActionStarted); ShowList(); SetStatus("Remote Desktop opened");
                    if (copy) await CopyEntry(latest, "Password", new Button());
                    else if (!session.IsPinned) Hide();
                }
                catch (OperationCanceledException) { if (epoch == startEpoch && vault.IsUnlocked) ShowError("The RDP handoff timed out or was cancelled. Try again."); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or CryptographicException or System.ComponentModel.Win32Exception)
                { if (epoch == startEpoch && vault.IsUnlocked) ShowError(ex is InvalidOperationException ? ex.Message : "Remote Desktop could not start or prepare its encrypted connection file. Check Windows permissions and try again."); }
                finally { moduleConnecting = false; modalDepth--; }
            }
            if (moduleApprovals.Contains(digest)) { _ = Launch(false); return; }
            var stack = Form("Connect with Remote Desktop", "", "module-permission", parent: () => ShowModuleDetails(item));
            stack.Children.Add(Text(profile.Host + ":" + profile.Port, 16));
            stack.Children.Add(Text(profile.Domain.Length > 0 ? profile.Domain + "\\" + profile.Username : profile.Username, 12, true));
            stack.Children.Add(Text("Opens Windows Remote Desktop using a temporary .rdp file with a Windows-encrypted password. The file is removed after startup; other software running as you can capture and decrypt it.", 12, true));
            stack.Children.Add(Text("Windows will let you review an unverified server certificate and choose whether to connect. Windows policy may still restrict connections. Sessions stay open when the vault locks.", 11, true));
            if (copy) stack.Children.Add(Text("Also copies the password. Your sensitive clipboard cleanup settings apply.", 12, true));
            stack.Children.Add(Text("Copy/paste is enabled. Other resource settings follow Windows Remote Desktop defaults and policy.", 12, true));
            if (!provider.Package.Official) stack.Children.Add(Text("Community module · not reviewed by Sable", 11, true));
            var remember = Check(stack, "Don't ask again for this connection", true); var page = Body.Content;
            PageAction(AsyncButton(copy ? "Launch & copy" : "Launch", async () => { if (ReferenceEquals(page, Body.Content) && epoch == startEpoch) await Launch(remember.IsChecked == true); })); pageSubmit = null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or CryptographicException or System.ComponentModel.Win32Exception) { ShowError(ex.Message); }
    }
}
