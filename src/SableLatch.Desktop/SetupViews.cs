using System.Security.Cryptography;
using System.Text;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private sealed class SetupDraft
    {
        public string Password = "", Recovery = Crypto.NewRecoveryKey();
        public string Pin = "";
        public bool Startup = true;
        public string? ImportFile, ImportPassword;
    }
    private void ShowWelcome()
    {
        var page = new Grid(); page.RowDefinitions.Add(new RowDefinition()); page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        SetBody(page, "setup");
        BrandTitle.Visibility = Visibility.Collapsed;
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 16, 12) }; page.Children.Add(stack);
        var title = Heading("Sable"); title.FontSize = 42; title.TextAlignment = TextAlignment.Center; title.Margin = new Thickness(0, 0, 0, 8); stack.Children.Add(title);
        var caption = Text("Your secrets, shortcuts, and connections.\nReady when you are.", 12, true); caption.TextAlignment = TextAlignment.Center; caption.Margin = new Thickness(0, 0, 0, 16); stack.Children.Add(caption);
        stack.Children.Add(OnboardingScene(loop: true));
        var create = Button("Create your vault  →", () => ShowSetupPassword(new()), true); create.HorizontalContentAlignment = HorizontalAlignment.Center; create.Height = 40; stack.Children.Add(create);
        pageSubmit = create;
        create.ToolTip = "Three short steps. Your vault stays encrypted on this computer.";
        var restore = Button("Restore from backup", ShowRestore); restore.FontSize = 11; restore.Opacity = .6; restore.HorizontalContentAlignment = HorizontalAlignment.Center; restore.Margin = new Thickness(0, 0, 0, 5);
        Grid.SetRow(restore, 1); page.Children.Add(restore);
        restore.ToolTip = "Bring back a vault using an encrypted backup and its recovery key.";
        WelcomeMotion(page);
    }
    private void ShowSetupPassword(SetupDraft draft)
    {
        PasswordBox? password = null; PasswordBox? confirm = null; CheckBox? startup = null;
        var stack = SetupPage(0, "Your vault. Your key.", "One strong password protects everything you keep.", () => { draft.Password = password!.Password; draft.Startup = startup!.IsChecked == true; if (draft.ImportFile is null) ShowWelcome(); else ShowImportFile(true, draft.ImportFile); });
        password = Password(stack, "At least 12 characters"); password.Password = draft.Password; password.ToolTip = "Use a memorable passphrase of at least 12 characters. You'll need it when the app starts.";
        confirm = Password(stack, "Confirm password"); confirm.Password = draft.Password;
        startup = Check(stack, "Start quietly with Windows", draft.Startup); startup.ToolTip = "Sable waits in the tray. You can turn this off in Settings.";
        PageAction(Button("Next  →", () =>
        {
            try { Crypto.RequirePassword(password.Password); } catch (ArgumentException ex) { RejectInput(password, ex.Message); return; }
            if (password.Password != confirm.Password) { RejectInput(confirm, "Passwords do not match."); return; }
            draft.Password = password.Password; draft.Startup = startup.IsChecked == true;
            ShowSetupRecovery(draft);
        }, true));
        Dispatcher.BeginInvoke(() => password.Focus());
    }
    private void ShowSetupRecovery(SetupDraft draft) => ShowRecoveryKey(draft.Recovery,
        () => { ShowSetupPin(draft); return Task.CompletedTask; }, false, () => ShowSetupPassword(draft));

    private void ShowSetupPin(SetupDraft draft)
    {
        var stack = SetupPage(2, "Back in a moment", "Use a quick PIN between full-password sign-ins.", () => ShowSetupRecovery(draft));
        var pin = Password(stack, "PIN · 4–12 digits"); pin.MaxLength = 12; pin.Password = draft.Pin; pin.ToolTip = "Only unlocks this running app. Five wrong attempts require your full password.";
        var confirm = Password(stack, "Confirm PIN"); confirm.MaxLength = 12; confirm.Password = draft.Pin;
        async Task Finish(bool usePin)
        {
            var chosen = pin.Password;
            if (usePin)
            {
                try { PinConfiguration.RequirePin(chosen); } catch (ArgumentException ex) { RejectInput(pin, ex.Message); return; }
                if (chosen != confirm.Password) { RejectInput(confirm, "PINs do not match."); return; }
            }
            draft.Pin = usePin ? chosen : "";
            await FinishSetup(draft);
        }
        stack.Children.Add(AsyncButton("Use full password instead", () => Finish(false), false));
        PageAction(AsyncButton("Finish setup  →", () => Finish(true)));
        Dispatcher.BeginInvoke(() => pin.Focus());
    }
    private void ShowRecoveryKey(string recovery, Func<Task> complete, bool replacement, Action? previous = null)
    {
        var stack = replacement ? Form("Your recovery key", "", "recovery-key", previous is not null, parent: previous)
            : SetupPage(1, "Keep a way back", "Your recovery key works if you forget your password.", previous, "recovery-key");
        stack.Children.Add(Text(replacement ? "Older backups still need their original key." : "Save it somewhere safe, outside this vault.", 11, true));
        var keyHost = new Grid { Height = 68 };
        var key = new TextBox { Text = "", Background = Brushes.Transparent, BorderThickness = new Thickness(0), IsReadOnly = true, Visibility = Visibility.Hidden, FontFamily = new FontFamily("Consolas"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(8), VerticalContentAlignment = VerticalAlignment.Center };
        var mask = new TextBlock { Text = "••••   ••••   ••••   ••••", Opacity = .4, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        keyHost.Children.Add(key); keyHost.Children.Add(mask); stack.Children.Add(new Border { Child = keyHost, Background = (Brush)FindResource("ControlSurface"), CornerRadius = new CornerRadius(10) }); eraseEditors.Add(key.Clear);
        var actions = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        var revealed = false;
        Button? reveal = null;
        reveal = Button("Reveal key", () =>
        {
            revealed = !revealed; key.Text = revealed ? recovery : "";
            key.Visibility = revealed ? Visibility.Visible : Visibility.Hidden;
            mask.Visibility = revealed ? Visibility.Collapsed : Visibility.Visible; reveal!.Content = revealed ? "Hide key" : "Reveal key";
        });
        actions.Children.Add(reveal);
        reveal.ToolTip = "Show your recovery key only when you're ready to save it.";
        var copied = new TextBlock { Text = "✓ Copied", Opacity = 0, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(131, 171, 146)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        keyHost.Children.Add(copied);
        var copyFeedback = 0;
        async Task Feedback(string message)
        {
            var generation = ++copyFeedback; copied.Text = message;
            void Fade(UIElement element, double value) { var from = element.Opacity; element.Opacity = value; element.BeginAnimation(OpacityProperty, SystemParameters.ClientAreaAnimation ? new System.Windows.Media.Animation.DoubleAnimation(from, value, TimeSpan.FromMilliseconds(150)) { FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop } : null); }
            Fade(key, 0); Fade(copied, 1);
            await Task.Delay(900);
            if (generation != copyFeedback || !keyHost.IsLoaded) return;
            Fade(copied, 0); Fade(key, 1);
        }
        async Task CopyKey()
        {
            var bytes = Encoding.UTF8.GetBytes(recovery);
            try
            {
                clipboard.Copy(new("text/plain", bytes, true, true), new Preferences { ClipboardSeconds = 120 });
                _ = Feedback("✓ Copied");
            }
            catch { ShowError("Clipboard is busy. Try again."); return; }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            await Task.CompletedTask;
        }
        key.ToolTip = "Click the key to copy. Clears after two minutes if unchanged.";
        System.Windows.Automation.AutomationProperties.SetName(key, "Recovery key · click to copy");
        key.PreviewMouseLeftButtonDown += (_, e) => { e.Handled = true; key.Focus(); };
        key.PreviewMouseLeftButtonUp += async (_, e) => { e.Handled = true; if (key.IsMouseCaptureWithin) Mouse.Capture(null); await CopyKey(); };
        key.PreviewKeyDown += async (_, e) => { if (e.Key is Key.Enter or Key.Space) { e.Handled = true; await CopyKey(); } };
        CommandManager.AddPreviewExecutedHandler(key, async (_, e) => { if (e.Command == ApplicationCommands.Copy) { e.Handled = true; await CopyKey(); } });
        var saveKey = Button("Save key", () =>
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Save recovery key", FileName = "Sable-recovery-key.txt", Filter = "Recovery key text file|*.txt", AddExtension = true, OverwritePrompt = true };
            modalDepth++;
            try
            {
                if (dialog.ShowDialog(this) != true) return;
                File.WriteAllText(dialog.FileName, "Sable recovery key\r\nKeep this file private, outside your vault. Anyone with this key can recover the vault.\r\n\r\n" + recovery + "\r\n", new UTF8Encoding(false));
                _ = Feedback("✓ Key saved");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowError("Couldn't save the key. Choose another location."); }
            finally { modalDepth--; }
        });
        saveKey.ToolTip = "Save a readable recovery-key file. Keep it private and separate from your vault.";
        actions.Children.Add(saveKey);
        stack.Children.Add(actions);
        var saved = Check(stack, "I have saved my recovery key", false);
        saved.Tag = "quiet-validation";
        PageAction(AsyncButton("Next  →", async () => { if (saved.IsChecked != true) { RejectInput(saved, "Save your recovery key first."); return; } await complete(); }));
        // This compact step always fits; only ordinary forms can scroll.
        if (stack.Parent is ScrollViewer scroll) scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }
    private static byte[] ReadArchive(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > Transfers.MaximumBytes) throw new InvalidDataException("Choose an archive smaller than 256 MB.");
        var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes); return bytes;
    }
    private string? ChooseArchive(bool backup)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Filter = backup ? "Sable backup|*.sablebackup" : "Sable encrypted export|*.sableexport" };
        modalDepth++; try { return picker.ShowDialog(this) == true ? picker.FileName : null; } finally { modalDepth--; }
    }
    private void ShowRestore() => ShowRestoreFile(null);
    private void ShowRestoreFile(string? selected)
    {
        var stack = Form("Restore from backup", "", "restore", parent: vault.IsUnlocked ? ShowBackupSettings : ShowUnlock);
        stack.Children.Add(Text("Your backup password and its recovery key are required.", 12, true));
        var label = Text(selected is null ? "Choose your .sablebackup file" : Path.GetFileName(selected), 12, true); stack.Children.Add(label);
        stack.Children.Add(Button("Choose backup…", () => { var path = ChooseArchive(true); if (path is not null) { selected = path; label.Text = Path.GetFileName(path); } }));
        PageAction(Button("Next  →", () => { if (selected is null) { ShowError("Choose a backup first."); return; } ShowRestoreCredentials(selected); }, true));
    }
    private void ShowRestoreCredentials(string path)
    {
        var stack = Form("Unlock your backup", "", "restore", parent: () => ShowRestoreFile(path));
        var password = Password(stack, "Backup password"); var key = Input(stack, "Master recovery key", "", true);
        PageAction(AsyncButton("Next  →", async () =>
        {
            var secret = password.Password; var recovery = key.Text; RecoveryPackage? package = null;
            await RunVault(() => Task.Run(() =>
            {
                package = Backups.Open(ReadArchive(path), secret);
                // Check both credentials before moving to the new password step.
                var rk = Crypto.ParseRecoveryKey(recovery); byte[]? dk = null;
                try { dk = Crypto.Open(rk, package.RecoveryGate, $"SableLatch/1/{package.VaultId:D}/recovery"); }
                finally { CryptographicOperations.ZeroMemory(rk); if (dk is not null) CryptographicOperations.ZeroMemory(dk); }
            }), () => ShowRestorePassword(path, package!, recovery));
        }));
    }
    private void ShowRestorePassword(string path, RecoveryPackage package, string recovery)
    {
        var stack = Form("New app password", "", "restore", parent: () => ShowRestoreCredentials(path));
        var password = Password(stack, "At least 12 characters"); var confirm = Password(stack, "Confirm password");
        var startup = Check(stack, "Start quietly with Windows", true);
        PageAction(AsyncButton("Restore vault  →", async () =>
        {
            var chosen = password.Password; var start = startup.IsChecked == true;
            if (chosen != confirm.Password) { RejectInput(confirm, "Passwords do not match."); return; }
            var existing = vault.Exists;
            var target = existing ? Path.Combine(Path.GetDirectoryName(App.DataDirectory)!, "Sable-restored-" + Guid.NewGuid().ToString("N")[..8]) : App.DataDirectory;
            await RunVault(() => Task.Run(() =>
            {
                if (!existing)
                {
                    vault.Restore(package, recovery, chosen);
                    vault.Mutate(d => { d.QuickUnlock = null; d.Settings.PinPromptDismissed = false; d.Settings.BackupEnabled = false; d.Settings.CompletedSetupSteps.Clear(); d.Settings.Startup = start; });
                }
                else
                {
                    VaultStore.RestorePackage(package, recovery, chosen, target, new WindowsDeviceProtector());
                    using var restored = new VaultStore(target, new WindowsDeviceProtector()); restored.Unlock(chosen);
                    restored.Mutate(d => { d.QuickUnlock = null; d.Settings.PinPromptDismissed = false; d.Settings.BackupEnabled = false; d.Settings.CompletedSetupSteps.Clear(); d.Settings.Startup = start; });
                }
            }), () =>
            {
                if (!existing) { OnUnlocked(); return; }
                var done = Form("Vault restored", "", "restored", parent: ShowBackupSettings);
                done.Children.Add(Text("Your existing vault is preserved. The restored copy is ready.", 12, true));
                done.Children.Add(Text(target, 11, true));
                PageAction(Button("Open restored vault", () =>
                {
                    var launch = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
                    launch.ArgumentList.Add("--data-dir"); launch.ArgumentList.Add(target);
                    System.Diagnostics.Process.Start(launch); Dispatcher.BeginInvoke(ExitApp);
                }, true));
            });
        }));
    }
}
