using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;

namespace SableLatch.Desktop;

internal sealed record NetworkAdapterChoice(Guid Id, string Name);

internal static class NetworkProfileBroker
{
    internal static NetworkAdapterChoice[] ListAdapters() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(adapter => adapter.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            && adapter.Supports(NetworkInterfaceComponent.IPv4))
        .Where(adapter => Guid.TryParse(adapter.Id, out _))
        .Select(adapter => new NetworkAdapterChoice(Guid.Parse(adapter.Id), adapter.Name))
        .OrderBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();

    // The payload contains only typed network settings. The script is fixed host code.
    // Resolve the saved adapter GUID again after UAC, before every write. All writes
    // use IPv4 commands, so DNS-only profiles never change addressing or IPv6.
    private const string ApplyScript = """
        $ErrorActionPreference = 'Stop'
        $profile = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__PAYLOAD__')) | ConvertFrom-Json
        $netsh = [IO.Path]::Combine([Environment]::SystemDirectory, 'netsh.exe')
        function AdapterIndex {
            foreach ($adapter in [Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()) {
                if ([string]::Equals($adapter.Id.Trim('{}'), $profile.adapter, [StringComparison]::OrdinalIgnoreCase) -and
                    $adapter.NetworkInterfaceType -notin @('Loopback', 'Tunnel') -and
                    $adapter.Supports([Net.NetworkInformation.NetworkInterfaceComponent]::IPv4)) {
                    $index = $adapter.GetIPProperties().GetIPv4Properties().Index
                    if ($index -gt 0) { return $index }
                }
            }
            throw 'Adapter unavailable'
        }
        function ChangeNetwork([int] $onFailure, [string[]] $change) {
            try { $index = AdapterIndex } catch { exit $(if ($onFailure -eq 12) { 11 } else { $onFailure }) }
            try {
                $parameters = @('interface', 'ipv4', $change[0], $change[1], "name=$index") + @($change | Select-Object -Skip 2)
                & $netsh @parameters 2>&1 | Out-Null
                if ($LASTEXITCODE -ne 0) { exit $onFailure }
            } catch { exit $onFailure }
        }
        if ($profile.addressMode -eq 'Automatic') {
            ChangeNetwork 12 @('set', 'address', 'source=dhcp')
        } elseif ($profile.addressMode -eq 'Manual') {
            ChangeNetwork 12 @('set', 'address', 'source=static', "address=$($profile.address)/$($profile.prefix)", "gateway=$($profile.gateway)")
        }
        if ($profile.dnsMode -eq 'Automatic') {
            ChangeNetwork 13 @('set', 'dnsservers', 'source=dhcp')
        } elseif ($profile.dnsMode -eq 'Manual') {
            ChangeNetwork 13 @('set', 'dnsservers', 'source=static', "address=$($profile.primary)", 'validate=no')
            if ($profile.secondary) {
                ChangeNetwork 14 @('add', 'dnsservers', "address=$($profile.secondary)", 'index=2', 'validate=no')
            }
        }
        exit 0
        """;

    internal static ProcessStartInfo Prepare(NetworkProfile profile)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            adapter = profile.AdapterId.ToString("D"),
            addressMode = profile.AddressMode.ToString(),
            address = profile.Address?.ToString(),
            prefix = profile.PrefixLength,
            gateway = profile.Gateway?.ToString() ?? "none",
            dnsMode = profile.DnsMode.ToString(),
            primary = profile.PrimaryDns?.ToString(),
            secondary = profile.SecondaryDns?.ToString()
        });
        var script = ApplyScript.Replace("__PAYLOAD__", Convert.ToBase64String(payload), StringComparison.Ordinal);
        var start = new ProcessStartInfo(EntryActions.SystemFile(@"WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Environment.SystemDirectory,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) })
            start.ArgumentList.Add(argument);
        return start;
    }

    internal static async Task<int> Apply(NetworkProfile profile)
    {
        // ShellExecute waits for the UAC decision. Keep the Sable UI thread free.
        using var process = await Task.Run(() => Process.Start(Prepare(profile)))
            ?? throw new Win32Exception("Windows PowerShell did not start.");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
