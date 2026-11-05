namespace SableLatch.Desktop;

public partial class MainWindow
{
    private static readonly string[] GuideSteps = ["backups", "trash", "shortcut"];
    private int CompletedGuideSteps => GuideSteps.Count(id => vault.Document!.Settings.CompletedSetupSteps.Contains(id));
    private static void MarkGuideStep(VaultDocument doc, string id)
    {
        if (!doc.Settings.CompletedSetupSteps.Contains(id)) doc.Settings.CompletedSetupSteps.Add(id);
    }
    private void AddSetupProgress(StackPanel stack)
    {
        var count = CompletedGuideSteps; if (count == GuideSteps.Length) return;
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = count == 0 ? "Finish setting up" : "Continue setup", FontSize = 13, FontWeight = FontWeights.Medium });
        content.Children.Add(new TextBlock { Text = $"{count} of 3 complete · Your everyday essentials", FontSize = 10, Opacity = .55, Margin = new Thickness(0, 5, 0, 7) });
        var progress = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var id in GuideSteps) progress.Children.Add(new Border { Height = 2, CornerRadius = new CornerRadius(1), Margin = new Thickness(0, 0, 4, 0), Background = vault.Document!.Settings.CompletedSetupSteps.Contains(id) ? Brushes.LightGray : (Brush)FindResource("HoverSurface") });
        content.Children.Add(progress);
        var action = Button("Continue setup", ShowSetupGuide); action.Content = content; action.HorizontalContentAlignment = HorizontalAlignment.Stretch; action.Padding = new Thickness(10, 12, 10, 12); action.Margin = new Thickness(0, 0, 0, 10);
        // Keep the compact settings-row styling from resizing this progress card.
        var host = new Border { Child = action, Background = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)), CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 0, 10) }; stack.Children.Add(host);
    }
    private void ShowSetupGuide()
    {
        var stack = Form("A few essentials", "", "settings");
        stack.Children.Add(Text($"{CompletedGuideSteps} of 3 complete. Pick up wherever you left off.", 12, true));
        var titles = new[] { "Protect a backup", "Set Trash retention", "Make it a shortcut" };
        var descriptions = new[] { "Keep a recovery copy of your vault.", "Choose when deleted items expire.", "Open Sable without reaching for the tray." };
        for (var i = 0; i < GuideSteps.Length; i++)
        {
            var id = GuideSteps[i]; var row = new StackPanel();
            row.Children.Add(new TextBlock { Text = (vault.Document!.Settings.CompletedSetupSteps.Contains(id) ? "✓  " : (i + 1) + "  ") + titles[i], FontSize = 13 });
            row.Children.Add(new TextBlock { Text = descriptions[i], FontSize = 11, Opacity = .55, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20, 5, 0, 0) });
            var action = Button(titles[i], () => ShowGuideStep(id)); action.Content = row; action.Padding = new Thickness(10, 14, 10, 14); stack.Children.Add(action);
        }
    }
    private void ShowGuideStep(string id)
    {
        if (id == "backups") ShowGuideBackup(); else if (id == "trash") ShowGuideTrash(); else ShowGuideShortcut();
    }
    private void ContinueGuide()
    {
        var next = GuideSteps.FirstOrDefault(id => !vault.Document!.Settings.CompletedSetupSteps.Contains(id));
        if (next is not null) { ShowGuideStep(next); return; }
        var stack = Form("You’re all set", "", "settings");
        stack.Children.Add(Text("Your backup preference, Trash retention and shortcut are ready. You can change them in Settings anytime.", 13, true));
        PageAction(Button("Back to settings", ShowSettings, true));
    }
    private void ShowGuideBackup()
    {
        var s = vault.Document!.Settings;
        var stack = Form("Protect a backup", "", "settings", parent: ShowSetupGuide);
        stack.Children.Add(Text("Restore with your backup password and recovery key. Keep both somewhere safe.", 11, true));
        var enabled = Check(stack, "Enable automatic backups", s.BackupEnabled);
        var directory = Input(stack, "Backup folder", s.BackupDirectory);
        stack.Children.Add(Button("Choose folder…", () =>
        {
            var picker = new Microsoft.Win32.OpenFolderDialog(); modalDepth++;
            try { if (picker.ShowDialog(this) == true) directory.Text = picker.FolderName; } finally { modalDepth--; }
        }));
        var password = vault.Document.Backup is null ? Password(stack, "Backup password · at least 12 characters") : null;
        var confirm = password is null ? null : Password(stack, "Confirm backup password");
        if (password is null) stack.Children.Add(Text("Your existing backup password will be used.", 11, true));
        SettingsSave(stack, AsyncButton("Save & continue", async () =>
        {
            var enable = enabled.IsChecked == true; var target = directory.Text.Trim(); var chosen = password?.Password ?? "";
            if (enable && target.Length == 0) { RejectInput(directory, "Choose a backup folder."); return; }
            if (enable && password is not null)
            {
                try { Crypto.RequirePassword(chosen); } catch (ArgumentException ex) { RejectInput(password, ex.Message); return; }
            }
            if (enable && password is not null && chosen != confirm!.Password) { RejectInput(confirm, "Passwords do not match."); return; }
            await RunVault(() => Task.Run(() =>
            {
                var capability = enable && password is not null ? Backups.Configure(chosen) : null;
                vault.Mutate(d => { if (capability is not null) d.Backup = capability; d.Settings.BackupEnabled = enable; d.Settings.BackupDirectory = target; MarkGuideStep(d, "backups"); });
            }), () => { Audit(AuditEvent.Changed); nextMaintenance = default; ContinueGuide(); });
        }));
    }
    private void ShowGuideTrash()
    {
        var s = vault.Document!.Settings;
        var stack = Form("Set Trash retention", "", "settings", parent: ShowSetupGuide);
        stack.Children.Add(Text("Only trashed items expire. Your saved entries stay until you delete them.", 12, true));
        var enabled = Check(stack, "Clean up encrypted Trash automatically", s.ExpireTrash);
        var days = Input(stack, "Keep trashed items for · days", s.TrashDays.ToString());
        SettingsSave(stack, Button("Save & continue", () =>
        {
            if (!int.TryParse(days.Text, out var n) || n is < 1 or > 365) { RejectInput(days, "Use 1–365 days."); return; }
            if (Change(d => { d.Settings.ExpireTrash = enabled.IsChecked == true; d.Settings.TrashDays = n; MarkGuideStep(d, "trash"); })) { nextMaintenance = default; ContinueGuide(); }
        }, true));
    }
    private void ShowGuideShortcut()
    {
        var s = vault.Document!.Settings;
        var stack = Form("Make it a shortcut", "", "settings", parent: ShowSetupGuide);
        stack.Children.Add(Text("Bring up your vault from anywhere. Press to record a combination that feels natural.", 12, true));
        var shortcut = ShortcutRecorder(stack, s.Shortcut);
        SettingsSave(stack, Button("Save & continue", () =>
        {
            var chosen = shortcut();
            try
            {
                Native.ParseShortcut(chosen); if (!App.PreviewMode) SetShortcut(chosen);
                if (!Change(d => { d.Settings.Shortcut = chosen; MarkGuideStep(d, "shortcut"); })) { if (!App.PreviewMode) SetShortcut(s.Shortcut); return; }
                if (!App.PreviewMode) File.WriteAllBytes(Path.Combine(App.DataDirectory, "host.json"), Codec.Encode(chosen));
                ContinueGuide();
            }
            catch { ShowError("That shortcut is unavailable. Try another combination."); }
        }, true), () => shortcut() != s.Shortcut);
    }
}
