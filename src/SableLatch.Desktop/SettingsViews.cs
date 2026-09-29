namespace SableLatch.Desktop;

public partial class MainWindow
{
    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        if (busy || !vault.IsUnlocked) return;
        NavigateSettings(() => { if (screen is "settings" or "about" or "audit" or "pin-setup") ShowList(); else ShowSettings(); });
    }
    private void ShowSettings()
    {
        if (!vault.IsUnlocked) { SetStatus("Unlock to change settings"); return; }
        var stack = Form("Settings", "", "settings", parent: ShowList);
        AddSetupProgress(stack);
        stack.Children.Add(Text("EVERYDAY", 10, true));
        stack.Children.Add(Button("Security & locking  ›", ShowSecuritySettings));
        stack.Children.Add(Button("Clipboard & copying  ›", ShowClipboardSettings));
        stack.Children.Add(Button("App preferences  ›", ShowAppearanceSettings));
        stack.Children.Add(Button("Plugins & modules  ›", ShowModules));
        var advanced = Button("Advanced  ›", ShowAdvancedSettings); advanced.Margin = new Thickness(0, 14, 0, 0); stack.Children.Add(advanced);
        foreach (var button in stack.Children.OfType<Button>()) { button.Height = 32; button.Margin = new Thickness(0); button.Padding = new Thickness(10, 7, 10, 7); }
        advanced.Margin = new Thickness(0, 8, 0, 0);
        var footer = new DockPanel();
        var owner = Body.Content; var confirmQuit = false; Button? quit = null;
        void ResetQuit() { confirmQuit = false; if (quit is null) return; quit.Content = "Quit"; quit.Opacity = .65; quit.Background = Brushes.Transparent; }
        quit = Button("Quit", () =>
        {
            if (busy || !ReferenceEquals(Body.Content, owner) || !quit!.IsVisible) return;
            if (confirmQuit) { ExitApp(); return; }
            confirmQuit = true; quit.Content = "Confirm quit"; quit.Opacity = 1; quit.Background = new SolidColorBrush(Color.FromRgb(70, 43, 49));
        });
        quit.IsVisibleChanged += (_, _) => { if (!quit.IsVisible) ResetQuit(); };
        quit.FontSize = 11; quit.Width = 100; quit.Opacity = .65; quit.Margin = new Thickness(0, 8, 0, 0); DockPanel.SetDock(quit, Dock.Right); footer.Children.Add(quit);
        var about = Button("ⓘ  About SableLatch", () => OpenAbout(this, new RoutedEventArgs())); about.FontSize = 11; about.Opacity = .65; about.Margin = new Thickness(0, 8, 0, 0); footer.Children.Add(about); PageAction(footer); pageSubmit = null;
    }
    private void ShowAdvancedSettings()
    {
        var stack = Form("Advanced", "", "settings", parent: ShowSettings);
        stack.Children.Add(Text("YOUR VAULT", 10, true));
        stack.Children.Add(Button("Backups & restore  ›", ShowBackupSettings));
        stack.Children.Add(Button("Import & export  ›", ShowTransfers));
        stack.Children.Add(Button("Trash & retention  ›", ShowDeletionSettings));
        var label = Text("TOOLS & DETAILS", 10, true); label.Margin = new Thickness(0, 17, 0, 6); stack.Children.Add(label);

        stack.Children.Add(Button("Activity & audit  ›", ShowAudit));

        foreach (var button in stack.Children.OfType<Button>()) { button.Height = 34; button.Margin = new Thickness(0); button.Padding = new Thickness(10, 7, 10, 7); }
    }
    private void ShowSecuritySettings()
    {
        var s = vault.Document!.Settings;
        var stack = Form("Security.", "The master recovery key is never stored in readable form or shown again.", "settings");
        var minutes = Input(stack, "Session duration · minutes (1–1,440)", s.SessionMinutes.ToString());
        var every = Check(stack, "Lock on every opening when unpinned", s.LockEveryOpen);
        SettingsSave(stack, Button("Save lock settings", () =>
        {
            if (!int.TryParse(minutes.Text, out var n)) { ShowError("Enter a whole number of minutes."); return; }
            if (Change(d => { d.Settings.SessionMinutes = n; d.Settings.LockEveryOpen = every.IsChecked == true; })) { ShowSettings(); Tick(); }
        }, true));
        stack.Children.Add(Button(vault.Document!.QuickUnlock is null ? "Set up quick PIN" : "Change quick PIN", () => NavigateSettings(() => ShowPinSetup(true))));
        stack.Children.Add(Button("Change app password", () => NavigateSettings(ShowChangePassword)));
        stack.Children.Add(Button("Generate replacement recovery key", () => NavigateSettings(ShowRotateRecovery)));
        stack.Children.Add(Text(CaptureProtection.HasFailure ? "Screen capture protection is unavailable for one or more windows in this Windows session." : "Screen capture protection is active for supported Windows capture tools.", 11, true));
        stack.Children.Add(Text("Other recording methods and external cameras cannot be blocked.", 11, true));
    }
    private void ShowChangePassword()
    {
        var stack = Form("Change password.", "This updates normal access. Your recovery key remains the same.", "settings", parent: ShowSecuritySettings);
        var current = Password(stack, "Current app password"); var next = Password(stack, "New password · at least 12 characters"); var confirm = Password(stack, "Confirm new password");
        SettingsSave(stack, AsyncButton("Change password", async () =>
        {
            if (next.Password != confirm.Password) { RejectInput(confirm, "Passwords do not match."); return; }
            var a = current.Password; var b = next.Password;
            await RunVault(() => Task.Run(() => { vault.VerifyPassword(a); vault.ChangePassword(b); }), () => { Audit(AuditEvent.PasswordChanged); RefreshQuickUnlock(); ShowSettings(); SetStatus("Password changed"); });
        }));
    }
    private void ShowRotateRecovery()
    {
        var stack = Form("Replace recovery key.", "Only do this if you need a replacement. Existing backups and older vault copies still require their original key. The new key is shown once.", "settings", parent: ShowSecuritySettings);
        var password = Password(stack, "Current app password");
        SettingsSave(stack, AsyncButton("Generate replacement", async () =>
        {
            var value = password.Password; string? replacement = null;
            await RunVault(() => Task.Run(() => { vault.VerifyPassword(value); replacement = vault.RotateRecoveryKey(); }), () => { Audit(AuditEvent.RecoveryChanged); ShowRecoveryKey(replacement!, () => { ShowSettings(); return Task.CompletedTask; }, true); });
        }));
    }
    private void ShowClipboardSettings()
    {
        var s = vault.Document!.Settings; var stack = Form("Copying.", "Secret copies are excluded from Windows clipboard history and cloud sync. Other clipboard tools may still read them.", "settings");
        var clear = Check(stack, "Automatically clear secret copies", s.ClearSecretClipboard);
        var seconds = Input(stack, "Clear after · seconds (1–3,600)", s.ClipboardSeconds.ToString());
        var close = Check(stack, "Close after copying when unpinned", s.CloseAfterCopy);
        stack.Children.Add(Text("A login's username keeps the panel open until you copy its password. Clipboard clearing leaves newer copies untouched.", 12, true));
        SettingsSave(stack, Button("Save copying settings", () =>
        {
            if (!int.TryParse(seconds.Text, out var n)) { ShowError("Enter a whole number of seconds."); return; }
            if (Change(d => { d.Settings.ClearSecretClipboard = clear.IsChecked == true; d.Settings.ClipboardSeconds = n; d.Settings.CloseAfterCopy = close.IsChecked == true; })) ShowSettings();
        }, true));
    }
    private void ShowDeletionSettings()
    {
        var s = vault.Document!.Settings; var stack = Form("Trash & retention", "", "settings", parent: ShowAdvancedSettings);
        var normal = Check(stack, "Move ordinary entries to encrypted Trash", s.TrashNormal);
        var secrets = Check(stack, "Move secrets and logins to encrypted Trash", s.TrashSecrets);
        var expire = Check(stack, "Automatically remove expired trashed entries", s.ExpireTrash);
        var days = Input(stack, "Trash retention · days (1–365)", s.TrashDays.ToString());
        stack.Children.Add(Text("Cleanup runs while unlocked, or on the next unlock. Earlier backups can retain deleted entries.", 11, true));
        SettingsSave(stack, Button("Save deletion settings", () =>
        {
            if (!int.TryParse(days.Text, out var n)) { ShowError("Enter a whole number of days."); return; }
            if (Change(d => { d.Settings.TrashNormal = normal.IsChecked == true; d.Settings.TrashSecrets = secrets.IsChecked == true; d.Settings.ExpireTrash = expire.IsChecked == true; d.Settings.TrashDays = n; })) { nextMaintenance = default; ShowSettings(); }
        }, true));
        stack.Children.Add(Button("Open encrypted Trash", () => NavigateSettings(ShowTrash)));
    }
    private void ShowBackupSettings()
    {
        var s = vault.Document!.Settings; var stack = Form("Backups & restore", "Restoring requires both the backup password and that backup's recovery key.", "settings", parent: ShowAdvancedSettings);
        var enabled = Check(stack, "Back up automatically when unlocked and changed", s.BackupEnabled);
        var directory = Input(stack, "Backup folder", s.BackupDirectory);
        stack.Children.Add(Button("Choose folder…", () =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog(); modalDepth++; try { if (dialog.ShowDialog(this) == true) directory.Text = dialog.FolderName; } finally { modalDepth--; }
        }));
        var hours = Input(stack, "Minimum interval · hours (1–720)", s.BackupHours.ToString());
        var keep = Input(stack, "Keep latest backups (1–100)", s.BackupKeep.ToString());
        var password = Password(stack, vault.Document.Backup is null ? "Set backup password · at least 12 characters" : "New backup password · leave blank to keep current");
        var confirm = Password(stack, "Confirm backup password if changing");
        stack.Children.Add(Text(s.LastBackupAt is null ? "No backup has been recorded yet." : "Last backup: " + s.LastBackupAt.Value.ToLocalTime().ToString("g"), 11, true));
        SettingsSave(stack, AsyncButton("Save backup settings", async () =>
        {
            if (!int.TryParse(hours.Text, out var h) || !int.TryParse(keep.Text, out var k) || h is < 1 or > 720 || k is < 1 or > 100) { ShowError("Use a valid interval and retention count."); return; }
            var pwd = password.Password; var path = directory.Text.Trim(); var enable = enabled.IsChecked == true;
            if (pwd != confirm.Password) { ShowError("The backup passwords do not match."); return; }
            if (enable && (string.IsNullOrWhiteSpace(path) || vault.Document.Backup is null && pwd.Length == 0)) { ShowError("Choose a backup folder and set its password first."); return; }
            await RunVault(() => Task.Run(() =>
            {
                BackupCapability? capability = pwd.Length > 0 ? Backups.Configure(pwd) : null;
                vault.Mutate(d => { if (capability is not null) d.Backup = capability; d.Settings.BackupDirectory = path; d.Settings.BackupEnabled = enable; d.Settings.BackupHours = h; d.Settings.BackupKeep = k; });
            }), () => { nextMaintenance = default; ShowBackupSettings(); SetStatus("Backup settings saved"); });
        }));
        if (vault.Document.Backup is not null && s.BackupDirectory.Length > 0) stack.Children.Add(AsyncButton("Back up now", () => MakeBackup(true)));
        stack.Children.Add(Button("Restore a backup", () => NavigateSettings(ShowRestore)));
    }
    private void ShowAppearanceSettings()
    {
        var s = vault.Document!.Settings; var stack = Form("App preferences", "", "settings");
        var taskbarOptions = new Dictionary<string, string> { ["Only when pinned"] = "pinned", ["Always"] = "always", ["Never · tray only"] = "never" };
        var taskbar = Select(stack, "Taskbar & Alt+Tab", taskbarOptions.Keys, taskbarOptions.FirstOrDefault(p => p.Value == s.TaskbarMode).Key ?? "Only when pinned");
        var startup = Check(stack, "Start quietly with Windows", s.Startup);
        var shortcut = ShortcutRecorder(stack, s.Shortcut);
        var theme = Select(stack, "Theme preset", ThemeCatalog.All().Select(t => t.Name), s.Theme);
        var language = Select(stack, "Language", new[] { "English" }, "English");
        language.IsEnabled = false;
        
        
        SettingsSave(stack, Button("Save preferences", () =>
        {
            var key = shortcut(); var choice = theme.SelectedItem as string ?? "Graphite"; var start = startup.IsChecked == true;
            try
            {
                Native.ParseShortcut(key); SetShortcut(key);
                if (!Change(d => { d.Settings.TaskbarMode = taskbarOptions[(string)taskbar.SelectedItem]; d.Settings.Theme = choice; d.Settings.Shortcut = key; d.Settings.Startup = start; })) { SetShortcut(s.Shortcut); return; }
                Native.SetStartup(start, Environment.ProcessPath!, App.DataDirectory); File.WriteAllBytes(Path.Combine(App.DataDirectory, "host.json"), Codec.Encode(key)); ApplyTheme(choice); ShowSettings();
            }
            catch (Exception ex) { try { SetShortcut(s.Shortcut); } catch { } ShowError(ex.Message); }
        }, true), () => shortcut() != s.Shortcut);
    }
    private static void ApplyTheme(string theme)
    {
        var preset = ThemeCatalog.All().FirstOrDefault(t => t.Name == theme) ?? new ThemePreset();
        var colors = new[] { preset.Surface, preset.Control, preset.Hover };
        var resources = Application.Current.Resources;
        resources["Surface"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[0]));
        resources["ControlSurface"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[1]));
        resources["HoverSurface"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[2]));
        resources["Accent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(preset.Accent));
    }
    private void ShowModules() => ShowPluginManager();
    private void OpenAbout(object sender, RoutedEventArgs e)
    {
        var stack = Form("SableLatch.", "A little space. Only yours.", "about", parent: ShowSettings);
        stack.Children.Add(Text("VERSION 0.8.5 · PREVIEW", 11, true));
        stack.Children.Add(Text("Portable for Windows 10 and 11 (64-bit). Built with native Windows UI, without a browser engine or background network service."));
        stack.Children.Add(Text("Local encryption: AES-256-GCM\nPassword protection: Argon2id\nDevice protection: Windows DPAPI\nBackup wrapping: RSA-OAEP + AES-GCM", 12, true));
        stack.Children.Add(Text("This first build has automated checks, but has not received an independent security audit. An unlocked or compromised computer can expose displayed and copied data.", 11, true));
        stack.Children.Add(Button("Open app folder", () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", AppContext.BaseDirectory) { UseShellExecute = true })));
        stack.Children.Add(Button("Exit SableLatch", ExitApp));
    }
}
