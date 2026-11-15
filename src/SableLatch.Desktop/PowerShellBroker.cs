using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace SableLatch.Desktop;

internal static class PowerShellBroker
{
    // The command line contains only this fixed loader and a random pipe name.
    // Values and user script are a JSON message, never interpolated into executable code.
    internal static ProcessStartInfo Prepare(EntryAction action, string pipeName, bool hidden = false)
    {
        var loader = """
        & {
          $ErrorActionPreference = 'Stop'
          $pipe = [System.IO.Pipes.NamedPipeClientStream]::new('.', 'PIPE_NAME', [System.IO.Pipes.PipeDirection]::In, [System.IO.Pipes.PipeOptions]::None, [System.Security.Principal.TokenImpersonationLevel]::Identification)
          try {
            $pipe.Connect(15000)
            $reader = [System.IO.StreamReader]::new($pipe, [System.Text.Encoding]::UTF8)
            $line = $reader.ReadLine()
            $packet = ConvertFrom-Json -InputObject $line
            $reader.Dispose(); $pipe.Dispose(); $line = $null
            $name = [string]$packet.values.name
            $value1 = [string]$packet.values.value1; $value2 = [string]$packet.values.value2
            $value3 = [string]$packet.values.value3; $value4 = [string]$packet.values.value4
            $value5 = [string]$packet.values.value5; $value6 = [string]$packet.values.value6
            $value7 = [string]$packet.values.value7; $value8 = [string]$packet.values.value8
            $value9 = [string]$packet.values.value9; $value10 = [string]$packet.values.value10
            $value11 = [string]$packet.values.value11; $value12 = [string]$packet.values.value12
            $value13 = [string]$packet.values.value13; $value14 = [string]$packet.values.value14
            $value15 = [string]$packet.values.value15; $value16 = [string]$packet.values.value16
            $value17 = [string]$packet.values.value17; $value18 = [string]$packet.values.value18
            $value19 = [string]$packet.values.value19; $value20 = [string]$packet.values.value20
            $script = [scriptblock]::Create([string]$packet.script)
            $packet = $null
            & $script
          } finally {
            if ($null -ne $pipe) { $pipe.Dispose() }
            $name = $null; $script = $null; $packet = $null; $line = $null
            $value1 = $value2 = $value3 = $value4 = $value5 = $value6 = $value7 = $value8 = $value9 = $value10 = $null
            $value11 = $value12 = $value13 = $value14 = $value15 = $value16 = $value17 = $value18 = $value19 = $value20 = $null
          }
        }
        """;
        if (!pipeName.StartsWith("Sable-", StringComparison.Ordinal) || pipeName.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new ArgumentException("Invalid pipe name.");
        var start = new ProcessStartInfo(EntryActions.SystemFile(@"WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = action.RunAsAdministrator, Verb = action.RunAsAdministrator ? "runas" : "",
            WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
            WorkingDirectory = action.WorkingDirectory.Length > 0 ? action.WorkingDirectory : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        start.ArgumentList.Add("-NoLogo"); start.ArgumentList.Add("-NoProfile");
        if (action.KeepTerminalOpen) start.ArgumentList.Add("-NoExit");
        start.ArgumentList.Add("-EncodedCommand"); start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(loader.Replace("PIPE_NAME", pipeName, StringComparison.Ordinal))));
        return start;
    }
    internal static async Task Run(EntryAction action, VaultItem item, CancellationToken cancellation, bool hidden = false)
    {
        var values = action.UseEntryVariables ? ScriptBindings.For(item) : new Dictionary<string, string>();
        var payload = Codec.Encode(new { script = action.Target, values }); values.Clear();
        Process? child = null;
        try
        {
            using var pipe = new CredentialPipe();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(30));
            cancellation.ThrowIfCancellationRequested();
            child = Process.Start(Prepare(action, pipe.Name, hidden)) ?? throw new InvalidOperationException("PowerShell could not start.");
            await pipe.Deliver(child, payload, timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            if (child is not null) try { if (!child.HasExited) child.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
            throw;
        }
        finally { CryptographicOperations.ZeroMemory(payload); child?.Dispose(); }
    }
}
