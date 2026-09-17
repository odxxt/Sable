using System.Diagnostics;
using System.Text;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private void Audit(AuditEvent code)
    {
        try
        {
            // Defer serializer/crypto initialization while the app is merely idle in the tray.
            if (!auditStarted) { audit.Record(AuditEvent.Started, auditStartedAt); auditStarted = true; }
            if (code != AuditEvent.Started) audit.Record(code);
            auditUnavailable = false;
        }
        catch { auditUnavailable = true; } // Logging failures never weaken authentication or overwrite unreadable history.
    }
    private void ShowAudit()
    {
        if (!vault.IsUnlocked) return;
        var stack = Form("Audit report", "", "audit", parent: ShowAdvancedSettings);
        stack.Children.Add(Text("Entry creation and modification dates, with the latest 256 security events.", 12, true));
        stack.Children.Add(Text("The HTML file contains labels and timestamps. Values stay hidden. Export again to update it.", 12, true));
        if (auditUnavailable) stack.Children.Add(Text("Local event history is unavailable on this Windows account. Its encrypted file is preserved.", 12, true));
        PageAction(Button("Save HTML report", () =>
        {
            var picker = new Microsoft.Win32.SaveFileDialog { Filter = "HTML report|*.html", FileName = "SableLatch-audit.html", OverwritePrompt = true };
            modalDepth++;
            try
            {
                if (picker.ShowDialog(this) != true || !vault.IsUnlocked) return;
                IReadOnlyList<AuditRecord> events = [];
                try { events = audit.Read(); auditUnavailable = false; } catch { auditUnavailable = true; }
                var html = AuditReport.Build(vault.Document!, events, !auditUnavailable);
                File.WriteAllText(picker.FileName, html, new UTF8Encoding(false));
                Audit(AuditEvent.ReportExported);
                var done = Form("Report saved", "", "audit", parent: ShowAudit);
                done.Children.Add(Button("Open in browser", () => Process.Start(new ProcessStartInfo(picker.FileName) { UseShellExecute = true }), true));
            }
            catch { ShowError("Couldn't save the report."); }
            finally { modalDepth--; }
        }, true));
    }
}
