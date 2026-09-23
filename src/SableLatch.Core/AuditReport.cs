using System.Net;
using System.Text;

namespace SableLatch.Core;

public static class AuditReport
{
    private static string Date(DateTimeOffset? value) => value?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") ?? "Not recorded";
    public static string EventName(AuditEvent code) => code switch
    {
        AuditEvent.Started => "App started", AuditEvent.PasswordUnlocked => "Full-password unlock",
        AuditEvent.PinUnlocked => "PIN unlock", AuditEvent.PasswordRejected => "Password rejected",
        AuditEvent.PinRejected => "PIN rejected", AuditEvent.PinExhausted => "Five PIN attempts exhausted",
        AuditEvent.Locked => "Vault locked", AuditEvent.Changed => "Vault updated",
        AuditEvent.PasswordChanged => "Password changed", AuditEvent.PinChanged => "PIN settings changed",
        AuditEvent.RecoveryChanged => "Recovery key changed", AuditEvent.BackupSaved => "Backup saved",
        AuditEvent.OperationFailed => "Operation failed", AuditEvent.ReportExported => "Audit report exported",
        AuditEvent.Exited => "App exited", AuditEvent.ExportSaved => "Encrypted export saved",
        AuditEvent.Imported => "Encrypted export imported", AuditEvent.EntryActionStarted => "Entry action started",
        AuditEvent.ModuleInstalled => "Module installed", AuditEvent.ModuleRemoved => "Module removed",
        AuditEvent.ModulePermissionsReset => "Module permissions reset", _ => "Unknown event"
    };
    public static string Build(VaultDocument document, IReadOnlyList<AuditRecord> events, bool historyAvailable = true)
    {
        var html = new StringBuilder("""
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'"><title>SableLatch audit</title><style>
:root{color-scheme:dark}body{margin:0;background:#111114;color:#ebebf0;font:14px/1.6 system-ui,sans-serif}main{max-width:1200px;margin:auto;padding:48px 28px}h1{font-size:30px;margin:0}h2{font-size:19px;margin:38px 0 12px}p{color:#a6a6b1}.summary{display:flex;gap:12px;margin:28px 0}.summary div{padding:14px 22px;border-radius:12px;background:#202024}.summary strong{display:block;font-size:24px}table{width:100%;border-collapse:collapse;text-align:left}th{font-size:11px;text-transform:uppercase;letter-spacing:.06em;color:#92929f}td,th{padding:13px 14px;border-bottom:1px solid #29292f}tbody tr:hover{background:#1c1c21}td{overflow-wrap:anywhere}small{display:block;color:#777786}time{font-size:12px;white-space:nowrap}.table{overflow:auto;border-radius:12px;background:#17171b}.type{color:#b8b8c6}footer{margin-top:34px;color:#777786;font-size:12px}@media print{body{background:white;color:black}p,th,footer,small{color:#444}.summary div,.table{background:#f3f3f3}main{padding:12px}td,th{border-color:#ddd}}
</style><main><h1>SableLatch audit</h1><p>Entry metadata and recent security events. Values, usernames, passwords, PINs and recovery keys are excluded.</p>
""");
        html.Append("<p>Report updated ").Append(Date(DateTimeOffset.UtcNow)).Append(" · Local snapshot</p><div class=summary><div><strong>")
            .Append(document.Items.Count(i => i.DeletedAt is null)).Append("</strong>Active entries</div><div><strong>")
            .Append(document.Items.Count(i => i.IsSensitive && i.DeletedAt is null)).Append("</strong>Sensitive entries</div><div><strong>")
            .Append(document.Items.Count(i => i.DeletedAt is not null)).Append("</strong>In trash</div></div><h2>Entries</h2><div class=table><table><thead><tr><th>Entry</th><th>Type</th><th>Created</th><th>Modified</th><th>Status</th></tr></thead><tbody>");
        foreach (var item in document.Items.OrderBy(i => i.DeletedAt is not null).ThenBy(i => i.Order))
        {
            var type = item.ContentType switch { "sable.login" => "Login · sensitive", "sable.secret" => "Secret", "sable.text" => "Text", _ => item.IsSensitive ? "Extension · sensitive" : "Extension" };
            html.Append("<tr><td>").Append(WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(item.Label) ? "Unlabelled entry" : item.Label))
                .Append("<small>").Append(item.Id.ToString("D")).Append("</small></td><td class=type>").Append(type)
                .Append("</td><td><time>").Append(Date(item.CreatedAt)).Append("</time></td><td><time>").Append(Date(item.UpdatedAt))
                .Append("</time></td><td>").Append(item.DeletedAt is null ? "Active" : "Trashed<small>" + Date(item.DeletedAt) + "</small>").Append("</td></tr>");
        }
        html.Append("</tbody></table></div><p>Creation times for entries saved before timestamp tracking are not known.</p><h2>Recent security events</h2>");
        if (!historyAvailable) html.Append("<p>Local history could not be read. Its encrypted file has been preserved.</p>");
        html.Append("<div class=table><table><thead><tr><th>Time</th><th>Event</th></tr></thead><tbody>");
        foreach (var entry in events.Reverse()) html.Append("<tr><td><time>").Append(Date(entry.At)).Append("</time></td><td>").Append(EventName(entry.Event)).Append("</td></tr>");
        return html.Append("</tbody></table></div><footer>History is capped at the most recent 256 events. This is a readable export, not a tamper-proof forensic record. Export again from SableLatch to refresh it. No server, scripts, or external resources are used.</footer></main></html>").ToString();
    }
}
