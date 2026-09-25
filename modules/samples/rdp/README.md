# Remote Desktop

Requires SableLatch 0.8.0 or later and Microsoft's RDP ActiveX control on Windows 10/11. Install from the included catalog, create a Remote Desktop entry, and click its row to connect. A separate SableLatch window hosts each session; no additional RDP installation is needed on the client. The target must support incoming RDP.

A trusted server certificate and matching host name are required. Clipboard sharing is opt-in; drives, printers and devices are disabled. This declarative ZIP never receives credentials. Trusted host code sends the chosen connection over a single-use local pipe to a dedicated session process. Passwords are handled in memory; this does not protect against malware or administrators controlling either endpoint. No proxy/server deployment or .rdp file is used.
