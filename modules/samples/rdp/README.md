# Remote Desktop module

This API 1 package requests Sable's trusted `rdp` handler; it contains no executable code and never receives a password. The official `sablelatch.*` ID and signature are compatibility identifiers. For a community derivative, use your own namespace and omit the official signature.

In Sable 0.9.1, install from Settings → Plugins & modules → Add a connection, then use **+ → New entry → Connection → Remote Desktop**. Clicking launches by default. The primary behavior can be Copy, Run or Run and copy; the context menu offers explicit Launch / Launch & copy. Right-click → Edit changes the connection, with domain, port and initial full-screen preference below credentials in **More options**.

The host opens native Windows `mstsc.exe` through a temporary `.rdp` file containing a DPAPI-encrypted password, then attempts cleanup after startup. Passwords remain encrypted in the vault at rest. Same-user software can capture/decrypt the temporary file; this is not password withholding from the local user. There is no Credential Manager write, proxy or plaintext password file. See [RDP design](../../../docs/RDP-DESIGN.md) and [Security](../../../SECURITY.md).

Native full screen is the default; windowed start is optional. Windows owns resizing, certificate prompts, resource defaults and server policy. Remote clipboard sharing is enabled independently from Launch & copy. Established sessions can survive Sable locking/exiting.

The manifest schema supports host 0.8.0+, but behavior belongs to the host: 0.8.0 used an embedded ActiveX window; 0.8.1 introduced native mstsc and copy-on-click; 0.8.5 restored native display/resource defaults and clipboard sharing; 0.9 changed the default click to launch. Current installation instructions are in [the module guide](../../README.md).
