using System.Diagnostics;

namespace SableLatch.Desktop;

internal sealed class FileEntryAction : IEntryActionHandler
{
    public string Kind => "file";
    public ProcessStartInfo Prepare(EntryAction action)
    {
        var path = Environment.ExpandEnvironmentVariables(action.Target);
        if (!Path.IsPathFullyQualified(path) || (!File.Exists(path) && !Directory.Exists(path))) throw new InvalidOperationException("Enter the full path of an existing file or folder.");
        return new(path) { UseShellExecute = true };
    }
}
internal sealed class NetworkEntryAction(string kind) : IEntryActionHandler
{
    public string Kind => kind;
    public ProcessStartInfo Prepare(EntryAction action)
    {
        var target = ShortcutTargets.Require(kind, action.Target);
        // Host parsing excludes quotes, operators, whitespace, switches and interpolation.
        var script = kind switch
        {
            "ping" => $"& '{EntryActions.SystemFile("ping.exe").Replace("'", "''")}' -n 4 '{target.Host}'",
            "lookup" => $"& '{EntryActions.SystemFile("nslookup.exe").Replace("'", "''")}' '{target.Host}'",
            "tcp" => $"Test-NetConnection -ComputerName '{target.Host}' -Port {target.Port}",
            _ => throw new InvalidOperationException("Unknown diagnostic.")
        };
        return new ScriptEntryAction("powershell").Prepare(new EntryAction { Kind = "powershell", Target = script, KeepTerminalOpen = action.KeepTerminalOpen });
    }
}
