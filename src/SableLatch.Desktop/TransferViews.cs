namespace SableLatch.Desktop;

public partial class MainWindow
{
    private void ShowTransfers()
    {
        var stack = Form("Import & export", "", "settings", parent: ShowAdvancedSettings);
        stack.Children.Add(Text("Encrypted exports open with their export password. Backups also require your recovery key.", 12, true));
        stack.Children.Add(Button("Export entries & folders  →", ShowExport));
        stack.Children.Add(Button("Import encrypted export  →", () => ShowImportFile(false)));
    }
    private void ShowExport()
    {
        var stack = Form("Encrypted export", "", "settings", parent: ShowTransfers);
        var password = Password(stack, "Export password · at least 12 characters");
        var confirm = Password(stack, "Confirm export password");
        stack.Children.Add(Text("Includes entries, encrypted Trash, folders and attachments. Keep the export password safe.", 11, true));
        PageAction(AsyncButton("Save export…", async () =>
        {
            var chosen = password.Password;
            try { Crypto.RequirePassword(chosen); } catch (ArgumentException ex) { RejectInput(password, ex.Message); return; }
            if (chosen != confirm.Password) { RejectInput(confirm, "Passwords do not match."); return; }
            var picker = new Microsoft.Win32.SaveFileDialog { Filter = "Sable encrypted export|*.sableexport", DefaultExt = ".sableexport", FileName = "Sable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") };
            modalDepth++; bool? accepted; try { accepted = picker.ShowDialog(this); } finally { modalDepth--; }
            if (accepted != true) return;
            await RunVault(() => Task.Run(() => Transfers.Save(picker.FileName, Transfers.Create(vault, chosen))), () => { Audit(AuditEvent.ExportSaved); ShowTransfers(); });
        }));
    }
    private void ShowImportFile(bool firstRun, string? selected = null)
    {
        var stack = Form("Import encrypted export", "", firstRun ? "setup" : "settings", parent: firstRun ? ShowWelcome : ShowTransfers);
        var label = Text(selected is null ? "Choose your .sableexport file" : Path.GetFileName(selected), 12, true); stack.Children.Add(label);
        stack.Children.Add(Button("Choose export…", () => { var path = ChooseArchive(false); if (path is not null) { selected = path; label.Text = Path.GetFileName(path); } }));
        var password = Password(stack, "Export password");
        PageAction(AsyncButton("Next  →", async () =>
        {
            if (selected is null) { ShowError("Choose an export first."); return; }
            var path = selected; var chosen = password.Password; int count = 0;
            await RunVault(() => Task.Run(() => { using var imported = Transfers.Open(ReadArchive(path), chosen); count = imported.Items.Count; }), () =>
            {
                if (firstRun) { ShowSetupPassword(new() { ImportFile = path, ImportPassword = chosen }); return; }
                var review = Form("Ready to import", "", "settings", parent: () => ShowImportFile(false, path));
                review.Children.Add(Text($"Add {count} entries, with their folders and attachments. Existing entries stay as they are. Importing again adds another copy.", 12, true));
                PageAction(AsyncButton("Import entries", async () => await RunVault(() => Task.Run(() =>
                {
                    using var imported = Transfers.Open(ReadArchive(path), chosen); Transfers.Import(vault, imported);
                }), () => { Audit(AuditEvent.Imported); ShowList(); })));
            });
        }));
    }
}
