# Remote Desktop launch — 0.8.5

SableLatch opens the Windows system Remote Desktop Connection application (mstsc.exe). It no longer hosts an RDP session window. The signed declarative RDP ZIP remains compatible; this change is implemented by trusted host code.

## Entry behavior

Clicking the row copies only the password with the existing sensitive clipboard policy. The play button uses the entry's Launch / Launch & copy preference. Right-click and Connection details offer both actions explicitly. Launch & copy copies the password after local startup, not after confirmed remote authentication. Pinning and close-after-copy preferences still apply. Remote-session clipboard sharing is enabled for normal copy/paste in both directions. Legacy clipboard-off fields are ignored; there is no separate SableLatch redirection toggle. Remote clipboard sharing and the Launch & copy button are separate behaviors.

## Display behavior

Start in full screen is enabled by default; turn it off in the entry's Advanced options to start in a normal window. The launch file selects only the initial screen mode. It no longer imposes desktopwidth, desktopheight, window placement or smart sizing. Windows chooses the initial desktop dimensions from the local display and owns all subsequent resizing and full-screen controls. Existing entries ignore their legacy width/height values and need no recreation or module reinstall.

Native mstsc supports smart sizing (scaling the existing desktop) but not dynamic resolution renegotiation as its window is resized. SableLatch does not pretend to enable that unsupported feature with a dynamic-resolution flag. Users retain the native client's behavior; a restored window may use scrollbars unless Smart sizing is selected in the Remote Desktop window menu.

## Credential lifecycle

1. Keep the connection settings and password encrypted in the existing vault. No permanent RDP file is stored. The unlocked vault still contains decrypted JSON in memory.
2. Review first launch on this Windows account, including the destination and the temporary-file security tradeoff. Approvals bind the public profile, package, entry, system client hash, launch mode and new delivery-method identifier. Old helper approvals cannot silently authorize the new method.
3. Convert bounded UTF-8 password bytes to UTF-16LE without an additional plaintext string. Protect them using Windows DPAPI CurrentUser with no application entropy, as required by the RDP password-51 format.
4. Create a randomly named .rdp file in %LOCALAPPDATA%/SableLatch/RdpLaunch/<vault-id>. A protected current-user directory ACL limits access by other ordinary accounts. Reject network paths and reparse-point directories. The file holds public settings and the DPAPI blob; it never contains a plaintext password or SableLatch master key. Clear owned plaintext conversion/transfer arrays immediately after protection.
5. Launch the fixed system mstsc.exe directly with only the settings-file path as an argument. No shell, DLL injection, Credential Manager write, environment password or automatic clipboard injection is used. Explicit Copy and Launch & copy are the only password clipboard operations.
6. Wait for GUI initialization, then a 1.5-second grace period, bounded by a 15-second startup wait. Dispose the file and retry failed deletion briefly. Lock cancellation and launch failures also clean up. Startup and normal exit sweep only matching host-generated files for that vault. No permanent cleanup service is installed.

## Limits

mstsc has no supported file-read or authentication acknowledgment. Startup/grace timing is best effort; deletion may be too early on an unusual or slow installation. Process startup does not establish successful login. A crash can leave a DPAPI-protected file until the next startup, and filesystem denial can prevent cleanup. Deletion is not secure erasure.

DPAPI binds the blob to the Windows account, not to SableLatch: software running as that user can capture and decrypt it. A captured copy remains useful after SableLatch deletes its copy. This is convenience protection, not password withholding from the local user or administrator. Passwords also exist in Windows/client memory; SableLatch cannot wipe those copies.

The user reported a working real-server connection with 0.8.2. Automatic sign-in with password-51 files has not been independently tested here across Windows 10/11 or organizational policies and may still be rejected by policy. The app keeps NLA/CredSSP enabled, respects Windows policy and never silently falls back to Credential Manager. Server certificate verification uses warning mode, with the decision to proceed left to the user in Windows. Interactive prompts may still appear. Test against a disposable RDP target before relying on this feature.

The generated file uses authentication level 2: Windows warns if the server cannot be authenticated and lets the user choose whether to connect, subject to policy. This replaces the strict refusal in 0.8.1; it does not silently accept an invalid certificate. Accepting a warning risks connecting to an impersonated server. CredSSP stays enabled. Unsigned-file publisher warnings are a separate Windows check and still appear. The generated file enables clipboard redirection and no longer overrides drive, printer, port, smart-card, device, audio, reconnection, cache or gateway settings. These follow mstsc defaults and Windows/server policy; this does not automatically redirect every local device or import settings from a separately saved connection. The first-use review is refreshed for this resource-policy change. Windows owns the separate session, its credential dialogs, saving UI and any later user changes; SableLatch does not control or capture-protect that external window. Existing sessions may survive vault locking or exit.

## Verification

Core checks cover profile validation, injected-newline rejection, IPv6 formatting and host-owned sensitive password copying. Windows tests cover file ACLs, protected-blob-only serialization, launch arguments, cleanup on success/failure/cancellation, bounded secret handling and scoped stale-file cleanup. A deterministic protector is used for lifecycle tests; an independent real DPAPI test reports blocked when the execution account cannot use DPAPI. No test uses real credentials. See [verification](../VERIFICATION.md).

## References

- [Windows mstsc interface](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/mstsc)
- [Windows DPAPI and user binding](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata)
- [Public RDP password-51 implementation](https://github.com/RedAndBlueEraser/rdp-file-password-encryptor)

- [Supported RDP properties and defaults](https://learn.microsoft.com/en-us/azure/virtual-desktop/rdp-properties)
- [Microsoft client comparison: dynamic resolution versus smart sizing](https://learn.microsoft.com/en-us/previous-versions/remote-desktop-client/compare-remote-desktop-clients)
