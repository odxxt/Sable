using System.Security.Cryptography;
using System.Text;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private void ShowUnlock()
    {
        if (quickUnlock?.Available == true && !vault.IsUnlocked) { ShowPinUnlock(); return; }
        var exists = vault.Exists;
        if (!exists) { ShowWelcome(); return; }
        var stack = new StackPanel();
        {
            var locked = LockForm("unlock", false); stack = locked.Stack; var password = locked.Input;
            async Task Unlock()
            {
                if (DateTimeOffset.UtcNow < nextUnlockAttempt) { ShowError("Please wait a few seconds before trying again."); return; }
                var value = password.Password; password.Clear(); var startEpoch = epoch;
                var ok = await RunVault(() => Task.Run(() => vault.Unlock(value)), () => OnUnlocked());
                if (!ok && startEpoch == epoch) { Audit(AuditEvent.PasswordRejected); failedUnlocks++; nextUnlockAttempt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(30, Math.Pow(2, Math.Min(5, failedUnlocks)))); }
            }
            var unlock = AsyncButton("Unlock  →", Unlock); stack.Children.Add(unlock); pageSubmit = unlock;
            stack.Children.Add(Button("Use a recovery key", ShowRecover));
            Dispatcher.BeginInvoke(() => password.Focus());
        }
        stack.Children.Add(Button("Restore an encrypted backup", ShowRestore));
        if (exists) foreach (var button in stack.Children.OfType<Button>()) button.HorizontalContentAlignment = HorizontalAlignment.Center;
    }
    private void OnUnlocked(bool fullPassword = true)
    {
        Audit(fullPassword ? AuditEvent.PasswordUnlocked : AuditEvent.PinUnlocked);
        failedUnlocks = 0; nextUnlockAttempt = default; session.Unlock(); nextMaintenance = default;
        heartbeat.Start();
        if (tray is not null) tray.Text = "Sable";
        var s = vault.Document!.Settings;
        ApplyTheme(s.Theme);
        if (fullPassword)
        {
            RefreshQuickUnlock();
            if (!App.PreviewMode)
            {
                try { SetShortcut(s.Shortcut); } catch { SetStatus("Shortcut unavailable · change it in Settings"); }
                try { Native.SetStartup(s.Startup, Environment.ProcessPath!, App.DataDirectory); } catch { SetStatus("Startup setting could not be applied"); }
                try { File.WriteAllBytes(Path.Combine(App.DataDirectory, "host.json"), Codec.Encode(s.Shortcut)); } catch { SetStatus("Shortcut could not be saved for startup"); }
            }
        }
        if (fullPassword && vault.Document!.QuickUnlock is null && !s.PinPromptDismissed) ShowPinSetup(false);
        else if (s.FirstEntryTourPending) ShowSetupReady(); else ShowList();
    }
    private void ShowRecover()
    {
        var stack = Form("Recover your vault.", "Use your master recovery key to set a new password and bind this vault to this computer.", "recover");
        var key = Input(stack, "Master recovery key", "", true);
        var password = Password(stack, "New password · at least 12 characters");
        var confirm = Password(stack, "Confirm new password");
        PageAction(AsyncButton("Recover vault", async () =>
        {
            if (password.Password != confirm.Password) { RejectInput(confirm, "Passwords do not match."); return; }
            var recovery = key.Text; var chosen = password.Password;
            await RunVault(() => Task.Run(() => vault.Recover(recovery, chosen)), () => OnUnlocked());
        }));
    }
}
