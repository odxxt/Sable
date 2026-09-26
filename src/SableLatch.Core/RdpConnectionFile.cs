using System.Globalization;

namespace SableLatch.Core;

// Public launch settings only. The Windows adapter adds a CurrentUser DPAPI blob, never plaintext.
public static class RdpConnectionFile
{
    public static string Create(RdpProfile profile, bool promptForPassword)
    {
        profile.Validate();
        var host = profile.Host.Contains(':') ? "[" + profile.Host.Trim('[', ']') + "]" : profile.Host;
        var account = profile.Domain.Length > 0 ? profile.Domain + "\\" + profile.Username : profile.Username;
        return string.Join("\r\n", new[]
        {
            "full address:s:" + host + ":" + profile.Port.ToString(CultureInfo.InvariantCulture),
            "username:s:" + account,
            // Leave resolution, scaling and window placement to the native client.
            "screen mode id:i:" + (profile.FullScreen ? "2" : "1"), "session bpp:i:32",
            "authentication level:i:2", "enablecredsspsupport:i:1", "negotiate security layer:i:1",
            "prompt for credentials:i:" + (promptForPassword ? "1" : "0"),
            "redirectclipboard:i:" + (profile.Clipboard ? "1" : "0"),
            "redirectdrives:i:0", "drivestoredirect:s:", "redirectprinters:i:0", "redirectcomports:i:0",
            "redirectsmartcards:i:0", "devicestoredirect:s:", "usbdevicestoredirect:s:",
            "audiocapturemode:i:0", "audiomode:i:2", "autoreconnection enabled:i:0",
            "bitmapcachepersistenable:i:0", "gatewayusagemethod:i:0", "gatewayprofileusagemethod:i:1", ""
        });
    }
}
