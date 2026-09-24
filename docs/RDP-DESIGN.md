# RDP module design — approved direction, not shipped

Research date: 24 September 2026. The user selected a separate SableLatch RDP window and saved usernames/passwords. This document is an implementation plan; there is no RDP module or RDP launcher in v0.7.2.

## Recommended approach

Use Microsoft's Windows Remote Desktop ActiveX control inside a dedicated SableLatch connection helper. This gives a native remote desktop window without asking the user to open an .rdp file. The ZIP module defines the entry and requests a reviewed host capability. It never receives the password or a vault handle. The helper is trusted application code, not executable code supplied in the module ZIP.

The password remains encrypted in the existing vault. On an explicit Run:

1. Validate the saved destination, port, username/domain and enabled module. Review first use on this PC and changed connection settings, as with PuTTY.
2. Start the packaged RDP helper directly, with only a random IPC identifier in arguments. Validate the helper binary and bind the one-use local channel to the actual launched process, with a current-user ACL, remote-client rejection and a delivery timeout.
3. Send only this connection's data to the helper. It sets server, username/domain and the Microsoft control's write-only `IMsTscNonScriptable.ClearTextPassword` property in memory, then calls Connect.
4. Zero buffers owned by SableLatch after delivery and dispose the channel. Do not use CMD/PowerShell, clipboard injection, environment credentials, cmdkey, Credential Manager writes, password files or an .rdp file.
5. Keep the helper only for the lifetime of that visible RDP session; no resident RDP service. Each helper receives one connection, never the whole vault. Disconnect destroys the control and exits the helper.

A native connection window is not a security boundary against an administrator or malware in the user's session. The password is briefly plaintext in client memory and Microsoft may retain internal copies that SableLatch cannot zero. Write-only means the public API has no getter; it does not mean memory cannot be inspected. Standard password-based RDP also exposes credentials to the destination through the authentication system. Authentication must use a trusted destination.

## Connection defaults and behavior

- Enable NLA/CredSSP and require successful server authentication. Do not silently ignore invalid certificates, downgrade security, or disable Windows policy. Certificate trust/onboarding requires a deliberate UX and integration tests.
- Use the supplied username/password without the local credential prompt in the successful path; disable credential-saving UI and persistent bitmap caching.
- Invalid passwords, expired credentials, MFA and server policy can still require interaction. Do not promise that every server can log in without any prompt. Fail back to a clear connection error rather than reveal a saved password or silently retry indefinitely.
- Disable clipboard, drive, printer and device redirection by default; expose deliberate per-entry options later. Keep initial options to host, port, account/domain, password, window size and fullscreen.
- Locking SableLatch cancels unfinished credential delivery. An already established session is separate from vault unlock state, as with PuTTY; a new/reconnected session must not request fresh credentials from a locked vault.
- Apply the existing supported capture-exclusion policy to the helper, with the same OS/recording-method limitations as the main panel.
- Launch tests should use a disposable Windows RDP target and fictional credentials. Test valid and invalid server identity, NLA, wrong/expired password, cancellation, Windows lock, timeout, wrong pipe client, helper tampering, parallel connections, and no residual credential/file/argument/log artifacts. Runtime compatibility needs testing on both target Windows versions.

## Alternatives considered

| Method | Assessment |
| --- | --- |
| Microsoft RDP control in SableLatch helper | Recommended for saved credentials: supported write-only API, native engine already supplied by Windows, no password launch file or saved credential required. |
| mstsc + cmdkey | Avoid. The common command pattern exposes the password in command arguments and creates a Windows saved credential. |
| mstsc + CredWrite API | Avoid as the default. It avoids a password command argument but still places the secret in Windows' credential store and adds cleanup/race concerns. |
| FreeRDP with private stdin | Viable alternative; stdin avoids password arguments, but adds another RDP engine and its distribution, patching and compatibility burden. Not necessary for this Windows-only app. |
| Remote Credential Guard | Stronger protection from a compromised destination where Kerberos and policy requirements are met; not the first implementation for arbitrary saved server accounts selected by the user. |
| Enterprise RDP proxy | Can keep the target password off the end-user PC, but needs a trusted server/service and changes SableLatch's local portable architecture. |

Delinea documents click-to-launch RDP and hiding the view-password permission. That establishes the desired user experience, not proof of its private implementation. A downloaded .rdp file is also not inherently evidence that a product exposes the real target password; enterprise products may connect through a proxy.

## Primary references

- [Microsoft write-only password API](https://learn.microsoft.com/en-us/windows/win32/termserv/imstscnonscriptable-cleartextpassword)
- [Microsoft credential prompt control](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientnonscriptable3-promptforcredentials)
- [Microsoft credential-saving UI control](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientnonscriptable4-allowcredentialsaving)
- [Microsoft server authentication levels](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings4-authenticationlevel)
- [Microsoft mstsc command interface](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/mstsc)
- [Microsoft Remote Credential Guard](https://learn.microsoft.com/en-us/windows/security/identity-protection/remote-credential-guard)
- [Delinea launch behavior](https://docs.delinea.com/online-help/secret-server/launcher-protocol-handler/launchers/procedures/launching-sessions/index.htm)
- [FreeRDP client command interface](https://github.com/FreeRDP/FreeRDP/blob/master/client/common/cmdline.h)
