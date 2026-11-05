using System.Diagnostics;
using System.Text;

namespace SableLatch.Desktop;

public interface IEntryActionHandler
{
    string Kind { get; }
    ProcessStartInfo Prepare(EntryAction action);
}

// Explicit registration only; importing an entry never installs or runs code.
public static class EntryActions
{
    private static readonly Dictionary<string, IEntryActionHandler> Handlers = new IEntryActionHandler[]
    { new NetworkEntryAction("ping"), new NetworkEntryAction("tcp"), new NetworkEntryAction("lookup"), new FileEntryAction(), new ApplicationEntryAction(), new WindowsEntryAction(), new WindowsEntryAction("control-panel"), new RunEntryAction(), new ScriptEntryAction("powershell"), new ScriptEntryAction("cmd") }.ToDictionary(h => h.Kind);
    public static void Register(IEntryActionHandler handler)
    {
        if (string.IsNullOrWhiteSpace(handler.Kind) || handler.Kind.Length > 80) throw new ArgumentException("Invalid action kind.");
        Handlers.Add(handler.Kind, handler);
    }
    public static ProcessStartInfo Prepare(EntryAction action)
    {
        action.Validate();
        if (!action.Enabled || !Handlers.TryGetValue(action.Kind, out var handler)) throw new InvalidOperationException("This action is disabled or its handler is unavailable.");
        var start = handler.Prepare(action);
        if (action.WorkingDirectory.Length > 0)
        {
            if (!Path.IsPathFullyQualified(action.WorkingDirectory) || !Directory.Exists(action.WorkingDirectory)) throw new InvalidOperationException("Choose an existing, full working-folder path.");
            start.WorkingDirectory = action.WorkingDirectory;
        }
        else start.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return start;
    }
    internal static string SystemFile(string relative) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), relative);
}

internal sealed class ApplicationEntryAction : IEntryActionHandler
{
    public string Kind => "application";
    public ProcessStartInfo Prepare(EntryAction action)
    {
        if (!Path.IsPathFullyQualified(action.Target) || !File.Exists(action.Target) || !Path.GetExtension(action.Target).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose an existing application (.exe) using its full path.");
        return new ProcessStartInfo(action.Target) { UseShellExecute = false, Arguments = action.Arguments };
    }
}

internal sealed class WindowsEntryAction(string kind = "windows") : IEntryActionHandler
{
    public string Kind => kind;
    public ProcessStartInfo Prepare(EntryAction action)
    {
        if (action.Target == "control") return new ProcessStartInfo(EntryActions.SystemFile("control.exe")) { UseShellExecute = false };
        if (action.Target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase) && action.Target.All(c => char.IsAsciiLetterOrDigit(c) || c is ':' or '-' or '?' or '=' or '&' or '%' or '.'))
            return new ProcessStartInfo(action.Target) { UseShellExecute = true };
        if (!action.Target.StartsWith("Microsoft.", StringComparison.Ordinal) || !action.Target.All(c => char.IsAsciiLetterOrDigit(c) || c == '.'))
            throw new InvalidOperationException("Use a Windows settings address, such as ms-settings:display, or a Control Panel name, such as Microsoft.Mouse.");
        var start = new ProcessStartInfo(EntryActions.SystemFile("control.exe")) { UseShellExecute = false };
        start.ArgumentList.Add("/name"); start.ArgumentList.Add(action.Target); return start;
    }
}

internal sealed class RunEntryAction : IEntryActionHandler
{
    public string Kind => "run";
    public ProcessStartInfo Prepare(EntryAction action)
    {
        var command = Environment.ExpandEnvironmentVariables(action.Target.Trim());
        if (command.Length is 0 or > 8000 || command.Contains('\r') || command.Contains('\n')) throw new InvalidOperationException("Enter one Run command, up to 8,000 characters.");
        string target; string arguments;
        if (File.Exists(command) || Directory.Exists(command)) { target = command; arguments = ""; }
        else if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end < 2) throw new InvalidOperationException("Close the quotes around the application path.");
            target = command[1..end]; arguments = command[(end + 1)..].TrimStart();
        }
        else
        {
            var end = command.IndexOfAny([' ', '\t']);
            target = end < 0 ? command : command[..end]; arguments = end < 0 ? "" : command[(end + 1)..].TrimStart();
        }
        // Windows resolves executables, registered files/folders and URI schemes. No implicit CMD wrapper.
        return new ProcessStartInfo(target) { Arguments = arguments, UseShellExecute = true };
    }
}

internal sealed class ScriptEntryAction(string kind) : IEntryActionHandler
{
    public string Kind => kind;
    public ProcessStartInfo Prepare(EntryAction action)
    {
        if (string.IsNullOrWhiteSpace(action.Target)) throw new InvalidOperationException("Add a script first.");
        if (kind == "powershell")
        {
            if (action.Target.Length > 12000) throw new InvalidOperationException("Use at most 12,000 characters for a PowerShell action, or invoke an existing .ps1 file.");
            // No plaintext script file, profile loading, execution-policy override or secret interpolation.
            var start = new ProcessStartInfo(EntryActions.SystemFile(@"WindowsPowerShell\v1.0\powershell.exe")) { UseShellExecute = false };
            start.ArgumentList.Add("-NoLogo"); start.ArgumentList.Add("-NoProfile");
            if (action.KeepTerminalOpen) start.ArgumentList.Add("-NoExit");
            start.ArgumentList.Add("-EncodedCommand");
            start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(action.Target))); return start;
        }
        if (action.Target.Length > 7000 || action.Target.Contains('\n') || action.Target.Contains('\r')) throw new InvalidOperationException("CMD accepts one command line, up to 7,000 characters. Use & to chain commands or call a .cmd file.");
        return new ProcessStartInfo(EntryActions.SystemFile("cmd.exe")) { UseShellExecute = false, Arguments = "/d /s " + (action.KeepTerminalOpen ? "/k" : "/c") + " \"" + action.Target + "\"" };
    }
}

public partial class MainWindow
{
    private void RememberAction(Guid id, EntryAction action)
    {
        try { actionApprovals.Remember(id, action); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or System.ComponentModel.Win32Exception)
        { SetStatus("Approval could not be saved. This action will ask again."); }
    }
    private void ReviewFirstTest(EntryAction action, Action run)
    {
        if (Body.Content is not FrameworkElement previous || !vault.IsUnlocked) return;
        var startEpoch = epoch; var previousVisibility = previous.Visibility;
        Body.Content = null; previous.Visibility = Visibility.Hidden;
        var host = new Grid(); host.Children.Add(previous);
        var panel = new Grid { Margin = new Thickness(12) };
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto }); panel.RowDefinitions.Add(new()); panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.Children.Add(Text("First run on this PC", 17));
        var body = new StackPanel(); Grid.SetRow(body, 1);
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(scroll, 1); panel.Children.Add(scroll);
        body.Children.Add(Text("Review this action before testing it.", 12, true));
        var command = new TextBox { IsReadOnly = true, Text = ActionDescription(action), TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
        body.Children.Add(command);
        body.Children.Add(Text("Runs with your Windows account's permissions.", 11, true));
        var footer = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 }; Grid.SetRow(footer, 2); panel.Children.Add(footer);
        host.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(33, 33, 39)), CornerRadius = new CornerRadius(14), Child = panel });
        Body.Content = host; modalDepth++;
        void Close()
        {
            if (!ReferenceEquals(Body.Content, host)) return;
            closeChoicePicker = null; modalDepth--; command.Clear(); host.Children.Remove(previous); previous.Visibility = previousVisibility; Body.Content = previous;
        }
        closeChoicePicker = Close;
        footer.Children.Add(Button("Cancel", Close));
        footer.Children.Add(Button("Test action", () => { if (!ReferenceEquals(Body.Content, host) || startEpoch != epoch || !vault.IsUnlocked || busy) return; Close(); run(); }, true));
    }
    private static string ActionDescription(EntryAction action) => action.Target + (action.Arguments.Length > 0 ? "\nArguments: " + action.Arguments : "") + (action.WorkingDirectory.Length > 0 ? "\nFolder: " + action.WorkingDirectory : "") + (action.CopyOnRun ? "\nOn run: copy the entry value (username first for a login)" : "") + (action.KeepTerminalOpen ? "\nKeep terminal open when finished" : "");
#if DEBUG
    private Action<ProcessStartInfo>? previewActionLaunch = null;
#endif
    private bool LaunchEntryAction(EntryAction action, Action<string>? reject = null)
    {
        if (!vault.IsUnlocked || busy) return false;
        try
        {
            var start = EntryActions.Prepare(action);
#if DEBUG
            if (App.PreviewMode)
            {
                if (previewActionLaunch is null) throw new InvalidOperationException("Action execution is disabled in this preview fixture.");
                previewActionLaunch(start); return true;
            }
#endif
            using var process = Process.Start(start);
            Audit(AuditEvent.EntryActionStarted); SetStatus("Action started"); return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception or IOException)
        { if (reject is null) ShowError(ex.Message); else reject(ex.Message); return false; }
    }
    private async Task CompleteEntryRun(VaultItem item, EntryAction action)
    {
        if (!vault.IsUnlocked) return;
        if (screen == "entry-action") ShowList();
        if (!action.CopyOnRun || item.ContentType is not ("sable.text" or "sable.secret" or "sable.login")) return;
        var row = entryList?.Items.Cast<RowModel>().FirstOrDefault(r => r.Item.Id == item.Id);
        var container = row is null ? null : entryList!.ItemContainerGenerator.ContainerFromItem(row) as ListBoxItem;
        var source = container is null ? new Button { DataContext = row } : Descendants<Button>(container).FirstOrDefault(b => b.Tag as string == "entry-action") ?? new Button { DataContext = row };
        await CopyEntry(item, item.ContentType == "sable.login" ? "Username" : "Copy", source);
    }
    private async void ReviewEntryAction(VaultItem item, ShortcutTarget? suggestedTarget = null)
    {
        if (!vault.IsUnlocked || modalDepth > 0) return;
        var saved = vault.Document!.Items.FirstOrDefault(i => i.Id == item.Id && i.DeletedAt is null);
        if (saved?.Advanced?.Action is not { Enabled: true } action || vault.Document.Settings.DisabledModules.Contains(saved.ContentType)) return;
        item = saved;
        var configured = Codec.Clone(action);
        if (suggestedTarget is not null)
        {
            if (saved.ContentType != "sable.shortcut" || !action.SuggestFromClipboard || !ShortcutTargets.Matches(action.Kind, suggestedTarget)) return;
            action = Codec.Clone(action); action.Target = suggestedTarget.Display;
        }
        var firstRun = !actionApprovals.Contains(item.Id, configured);
        if (!action.VerifyEachTime && !firstRun) { if (LaunchEntryAction(action)) await CompleteEntryRun(item, action); return; }
        var snapshot = Codec.Clone(action); var startEpoch = epoch;
        var stack = Form("Run", "", "entry-action");
        var reviewPage = Body.Content;
        stack.Children.Add(Text(item.Label, 12, true));
        stack.Children.Add(Text(firstRun ? "First run on this PC" : "Verify action", 15));
        var review = new TextBox { Text = ActionDescription(snapshot), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MaxHeight = 155, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        stack.Children.Add(review); eraseEditors.Add(review.Clear);
        stack.Children.Add(Text("Runs with your Windows account's permissions. Only run actions you understand and trust. Vault values are never inserted automatically.", 11, true));
        var run = Button("Run action", async () =>
        {
            if (!ReferenceEquals(Body.Content, reviewPage) || !vault.IsUnlocked || startEpoch != epoch || busy) return;
            try
            {
                var current = vault.Document!.Items.FirstOrDefault(i => i.Id == item.Id && i.DeletedAt is null)?.Advanced?.Action;
                if (current is null || !Codec.Encode(current).SequenceEqual(Codec.Encode(configured))) throw new InvalidOperationException("This action changed. Review it again.");
                if (LaunchEntryAction(snapshot)) { RememberAction(item.Id, configured); await CompleteEntryRun(item, snapshot); }
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception or IOException) { ShowError(ex.Message); }
        }, true);
        PageAction(run);
        // Reviewing content must not accidentally execute it via a queued Enter.
        pageSubmit = null;
    }
}
