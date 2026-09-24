# Security design and verification limits

This document describes the implemented preview, not a certification or security guarantee.

## Vault format v1

- A cryptographically random 32-byte data key encrypts the JSON metadata using AES-256-GCM, a fresh 12-byte random nonce per write, and a 16-byte tag.
- Associated data includes the format, vault identifier, and purpose. Each attachment has its own nonce/tag and associated data that includes its attachment ID.
- Argon2id derives a 32-byte password wrapping key with a random 16-byte salt, 65,536 KiB memory, three passes, and one lane. These parameters are fixed by format v1 rather than accepted unbounded from an untrusted file.
- The password wrapping key encrypts the data key. The entire password gate is then protected by **current-user DPAPI**, with app-specific entropy incorporating the Windows MachineGuid. Neither a password hash usable as a decryption bypass nor a plaintext data key is written to disk.
- A separate random 256-bit recovery key encrypts the same data key. The displayed SL1 representation contains eight groups of hexadecimal key material and an eight-character SHA-256 checksum. The checksum detects transcription errors; it adds no secret entropy.
- The recovery key is shown only at generation. It is not recoverable from the app settings. A user who authenticates normally can replace the current recovery gate. Older copies remain governed by their old gate.

DPAPI and MachineGuid provide practical Windows-user/installation binding, **not TPM-backed non-exportable hardware binding**. Windows profile/OS clones, privileged recovery, or a compromised system are outside the copied-vault-file threat model. Someone possessing the recovery key and vault can deliberately recover it elsewhere.

## Backup format v1

Backup configuration generates an RSA-3072 key pair. Its public key permits automatic encryption. The private key is encrypted with AES-GCM under an Argon2id-derived backup-password key. The vault stores the public key and encrypted private key, never the backup password or unencrypted RSA private key.

Each archive has a new random AES-256 key, wrapped with RSA-OAEP-SHA256. Its inner package contains the encrypted metadata, authenticated attachment blobs, and the recovery gate, but **no device/password gate**. The backup password is needed to unlock the RSA private key and outer archive. The master recovery key is still needed to obtain the vault data key. Restoring a backup therefore requires both independent credentials.

Older backups need their original passwords/keys. Restoring creates a new app-password gate for the destination Windows environment. Retention only deletes archives generated for the same vault ID after a successful new archive write.

## Password-only transfer exports

`SableLatchExport/1` is separate from the two-credential backup format. The export contains only entries, folders and attachment bytes inside one authenticated AES-256-GCM envelope. Its key uses Argon2id with a fresh cryptographically random 16-byte salt per export, 65,536 KiB, three passes and one lane; AES uses a fresh 12-byte nonce and 16-byte tag. Associated data binds the format, fixed KDF suite and encryption purpose. The file cannot supply arbitrary KDF costs. Only the format marker, salt and encryption envelope are readable; the password and original recovery key are not written into the file.

The whole payload is authenticated and validated before import writes. Unknown provider payloads/version numbers survive; item/folder/attachment identifiers are remapped on merge. Destination credentials/settings are preserved. Save failures may leave unreferenced encrypted attachment blobs; the existing orphan-cleanup path can remove those. No plaintext transfer temporary file is written. Serialization strings/JSON copies have the memory limitations described below. Imports/exports run only on request and have no permanent cache, service or listener.

Unique salts prevent reusable precomputed rainbow-table attacks. They do not prevent offline guessing against one file, and encrypted files cannot enforce the UI's attempt budget. A long unique passphrase is necessary; encryption does not make weak passwords impossible to guess. The recovery key cannot rescue a forgotten export password. Backups intentionally retain their separate two-credential requirement.

Reference: [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).
## Memory and clipboard

### Process-only quick PIN

The full password is required after every process start. PIN enrollment (4–12 ASCII digits) stores a random salt, configuration ID, and Argon2id verifier only inside the encrypted vault document. A successful full-password unlock creates an in-memory `QuickUnlockSession`. Its AES-GCM gate wraps the vault data key under HMAC-SHA256(random 256-bit process secret, PIN-derived verifier), with vault/configuration-specific associated data. The ticket retains a copy of the salt, encrypted gate, and process secret, never the raw PIN or verifier. It is never serialized to disk.

On quick lock, the active data key is zeroed and the document/editors are released. A PIN attempt repeats Argon2id, derives the gate key with the process secret, decrypts the ticket, then authenticates the current vault document. The configuration ID and password salt must still match. An old ticket is rejected after PIN or full-password changes. Five failed PIN attempts destroy the ticket, and a synchronized gate serializes validation and ticket disposal. The UI holds a submission latch through verification and 650 ms of feedback, clears the input, and disables typing until it is ready again. Exit or sign-out retires it. Restarting cannot reset the PIN attempt budget because no ticket survives; a full password is required to mint another one.

PIN unlock is only a convenience for reopening the running app. It cannot verify the full password for PIN changes, password changes, or recovery-key replacement, and it does not replace either backup credential. Users can disable PIN or fall back to the full password. The PIN is **not Windows Hello, TPM-backed authentication, or additional protection against a compromised process**. Someone who can read or modify the process, dump memory, or run trusted in-process extensions is outside this boundary; captured ticket material can be attacked offline. A short PIN offers substantially less entropy than a strong full password. PIN input uses the keyboard only.

Key byte arrays and temporary plaintext serialization buffers are zeroed where owned. .NET/WPF strings, JSON internals, garbage collector copies, OS clipboard copies, paging, and crash dumps cannot be guaranteed erased. The app drops references and explicitly clears editors on lock; it does not claim complete memory erasure. Argon2's temporary memory is eligible for collection at operation boundaries.

Secret copies include Windows clipboard history/cloud-exclusion formats. Cleanup checks the clipboard sequence number and, for text, a digest before clearing. A later clipboard item is left untouched. Other clipboard applications and programs running as the user can read copied values. Clipboard clearing is also attempted on lock/exit when the user has enabled it.

Native SetWindowDisplayAffinity is requested and read back for the main panel. Each WPF context menu and tooltip is protected on opening; dropdown and submenu templates attach CaptureProtection.Enabled to protect their own HWNDs. Windows 10 build 19041 and later use WDA_EXCLUDEFROMCAPTURE (0x11); earlier versions use WDA_MONITOR (1). Protection has no resident worker or polling timer. Settings report any detected application failure. Native affinity checks passed for the main panel, context menu, dropdown and tooltip on this host. This does not prove every Teams/Zoom mode is protected: neither product was directly tested. OS dialogs, independently launched apps, alternative capture methods, privileged software, cameras and a compromised process are outside the boundary. Microsoft explicitly does not guarantee strict protection for every capture method: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity.

## Files and recovery

### Audit metadata and exports

Entry creation/edit timestamps are encrypted with the vault metadata. A separate local audit journal accepts only fixed event codes and UTC timestamps, with 256-record retention and a 128 KB size limit. The Windows device protector encrypts it; it contains no labels, item identifiers, content, paths, or raw exception text. Corruption/foreign-device decryption failures preserve the existing file and surface as unavailable history on the audit screen. Logging failure never disables authentication protections.

This local history is not an externally anchored, tamper-proof forensic log. A same-user attacker, administrator, rollback, or deletion can defeat local operational history. The journal is not included in recovery backups and cannot be read on a different Windows environment merely by recovering the vault.

HTML export is explicit and **readable, unencrypted metadata**. It includes labels and dates but never accesses payload values or credentials. User labels are HTML-escaped; the report contains no scripts or external resources and applies a restrictive content-security policy. Export to the same filename refreshes the snapshot. No reporting web server, automatic export, or background reporting timer is installed.

Files are encrypted before temporary writes. A corrupt or unsupported vault is never silently reset. An old vault cannot be decrypted by deleting configuration or resetting the app. No recovery backdoor, security questions, network recovery service, or authenticator-code recovery is provided.

Logical deletion removes active references. It does not securely erase SSD history, filesystem snapshots, OS backups, or copied archives. Secret Trash retention is configurable, and expiration affects only entries already in Trash.

## Verification performed / required

The bundled suite tests actual Argon2/AES/RSA operations, wrong credentials, tampering, device-protector separation, recovery transfer, password/key changes, two-credential backups, encrypted attachment round trips, unknown provider payload preservation, atomic mutation failure, folder validation, and session/Trash policies.

The real DPAPI call failed inside the restricted build session. The visual harness uses a Debug-only device adapter with an ephemeral key. Release must be tested from a normal Windows account for real DPAPI round trips, second-computer rejection/recovery, lock/unlock and sleep behavior, clipboard integration, startup registration, hotkey collisions, multiple monitors, and Windows 10/11 edition compatibility. An independent cryptographic/application security review remains required before presenting this as a production password manager.

External executable modules are not loaded. Adding warnings to a DLL loader would not create a sandbox. A future third-party plugin host must enforce capabilities, CPU/memory budgets, extraction path checks, compatibility/signature policy, and cancellation without exposing master keys or raw store objects.

## References

- [OWASP cryptographic storage](https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html)
- [OWASP password storage](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
- [OWASP authentication and reauthentication](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [Microsoft CryptProtectData](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata)
- [Windows clipboard exclusion formats](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-formats)
- [Wasmtime sandbox/capability model](https://docs.wasmtime.dev/security.html)

Audit initialization is deferred until the first real event (or normal exit); the actual startup timestamp is buffered as one value until then. A process killed before any event/exit may leave no startup record. This avoids loading serialization/crypto solely for an unopened tray process.


## User-defined actions (0.6.0)

Advanced entries can contain application launch arguments or scripts. They are encrypted at rest, including backup/export copies, but are executable instructions once a user explicitly tests or invokes them. They are NOT sandboxed and can access resources available to the Windows account, including modifying or deleting files and making network requests. No extra elevation is requested. Do not confuse the review with a safety analysis of a script. Imports and restores never run actions automatically. Row invocation requires device-local first-run review, followed by direct launches unless Verify each time is enabled. Testing is an explicit launch, not a sandbox or static safety analysis. Cancellation, navigation or locking before a confirmed launch cancels its review. Locking after launch cannot revoke permissions from the external process.

Vault fields/master keys are not automatically inserted into command lines. Secrets manually embedded in a command may be visible in process inspection, application logs or script output while running. PowerShell encoding is transport quoting, not encryption. Actions can invoke existing scripts, subject to Windows policies. No permanent script files, launcher service or background workers are added. The public handler registration surface is for trusted bundled composition and is not an unsigned-plugin sandbox.

The 0.6.1 editor requires an initial action test before saving a newly configured enabled action. Only a successful process-launch request marks a configuration tested; it does not establish script correctness, harmlessness or completion. Test identity hashes the exact kind, target, arguments and working folder in memory; changes invalidate that identity. No plaintext command history or persistent test token is added. Direct row launches re-read the current undeleted, enabled entry. The optional VerifyEachTime value is encrypted with the action. Legacy entries now require first-run consent on this device; since 0.6.3, imported actions require device-local first-run consent instead of forcing repeat verification.

Application discovery reads registered App Paths and Start-menu shortcuts on demand in a temporary STA worker, capped at 2,000 results. It does not execute shortcuts during discovery. Choosing a shortcut can populate launch arguments; the explicit Test action executes that configuration. Detection is not a malware scan or guarantee that an application is safe.
In 0.6.2, Test action is separate from Save entry; clicking dimmed Save cannot execute anything. Failed process launches do not unlock Save. Run actions expand environment variables and use the Windows shell for executable, file, folder and URI resolution; they are not a sandbox and do not automatically invoke CMD. Control Panel uses the fixed system control.exe with separate canonical-name arguments. Application icons are read on demand and cached only for the open picker. No background discovery service or plaintext action history is added. Field-validation hint counters do not change authentication attempt limits.
## Device-local action consent (0.6.3)

Every saved action needs local approval for its entry ID and SHA-256 digest of kind, target, arguments and working folder. The record is protected by the Windows account DPAPI adapter with MachineGuid-derived entropy, plus a purpose/version and machine-name check. Approval is independent of the encrypted VerifyEachTime preference. Successful explicit review grants consent; cancelling or failing the launch does not. New locally authored entries can be approved by the required explicit test before saving. An existing unapproved entry's test first displays a review overlay.

Imports remap item IDs, and backups/exports never include the consent file. Copying the entire portable folder to another computer cannot reuse the device-protected file. Missing, malformed, oversized or unprotectable consent fails closed. Up to 2,048 digests are retained; eviction requires a new review. Changes to executable file contents at the same path are not detected: approval covers the launch configuration, not the integrity or safety of the target binary. This is an accidental-execution safeguard, not a sandbox against malicious code already running as the same Windows user.

Old builds forced VerifyEachTime on imported entries. Existing true values are retained because they cannot be distinguished from deliberate user preferences. Users can turn that option off after upgrading; device-local first-run review remains required independently.
## Run and copy (0.6.5)

CopyOnRun is encrypted with the action and preserved through encrypted transfers. It defaults off for older entries and is included in the local approval fingerprint. Built-in Text, Secret and Login actions expose this option. Ordinary text retains its ordinary clipboard policy; secrets retain the sensitive policy. A saved Run and copy invocation copies after a successful launch request, using the content provider's sensitive clipboard value and existing clearing/history rules. Login copies the password. Tests, failed launches and cancelled reviews do not copy. No secret is interpolated into process arguments or displayed in review text. A launched program is not sandboxed and, like other applications running as that Windows user, may access the clipboard; use only actions you trust. Copy-mode changes invalidate previous local approval.

The eye swaps the masked/revealed editor and clears the hidden control. Lock/navigation still clears both. Managed strings cannot be guaranteed physically erased from memory; this change does not alter that limitation.
## Multiline secret editing and terminal lifetime

MaskedSecretBox keeps the WPF text document limited to bullets and original line breaks. Text input and paste update the private value and repaint the mask; copy, cut, undo, IME composition and drag/drop are disabled in the masked field. Its automation peer reports a password control without Value/Text patterns. The separate reveal editor disables undo and is cleared when hidden. Navigation/locking clears both controls. This reduces incidental UI exposure; it does not secure decrypted process memory from local malware or guarantee physical erasure of .NET strings.

KeepTerminalOpen is encrypted action metadata and is included in consent identity when enabled. With it disabled, existing fingerprints remain valid for backward compatibility. Changing it invalidates a tested configuration. /k and -NoExit affect process lifetime only, without changing the account privileges or sandboxing status. External terminals and their displayed output are not protected by SableLatch capture exclusion.

Automatic backups remain off for new/legacy preferences and after recovery to a new computer. The setup checklist no longer preselects them. Existing explicitly saved backup preferences are preserved.
## Declarative ZIP modules and PuTTY (0.7.0)

External ZIPs are data, not executable plugins. A flat allowlist, bounded decompression, strict JSON, a versioned schema and a closed set of host operations reject native code, scripts, XAML and arbitrary network operations. ECDSA P-256/SHA-256 signatures authenticate the exact official manifest bytes and the catalog payload against an embedded public key. Downloaded official ZIPs also need the SHA-256 in the signed catalog. Community packages never inherit official status from their names or author strings.

Only host code owns fields, decrypted documents, clipboard operations and process launching. A module never receives VaultItem or IExtensionContext. Secret fields cannot be selected by declarative copy actions. Entries bind their module schema; incompatible replacement/downgrade attempts cannot turn a stored secret into ordinary text. Deleting a module does not delete encrypted entries. Layout, toolbar and executable extension interfaces remain trusted-built-in-only.

PuTTY is a reviewed host capability, not code embedded in the ZIP. The chosen local executable must have a valid cached Windows Authenticode trust result, Simon Tatham publisher identity, PuTTY original filename and a version supporting -pwfile. Its file is held open without write/delete sharing through launch and transfer. Consent binds the module manifest, entry ID, target/options and executable SHA-256. Changed destinations, binaries, packages and newly imported entries require review. Approvals are bounded, device-protected and excluded from exports and backups.

The pipe uses 256 random name bits, one instance, first-instance exclusivity, a current-user-only protected DACL and PIPE_REJECT_REMOTE_CLIENTS. The server holds the launched process handle and checks GetNamedPipeClientProcessId before sending any password bytes. A wrong client aborts the operation; there is no insecure fallback. The password is a single UTF-8 line, never an argument, environment value, clipboard value, log entry or temporary file. Host-owned mutable JSON-transfer and pipe-write buffers are zeroed, including failure/cancellation paths. This does not guarantee physical erasure of the unlocked document, .NET strings, kernel buffers or PuTTY memory.

A temporary non-secret PuTTY session isolates each connection from existing logging, local proxy commands, remote commands, agent forwarding and connection-sharing settings. It is removed after the handoff. Crash leftovers contain no password. Session cancellation and a 15-second timeout close the pipe and stop only the pending launched child; already established connections are independent. PuTTY still performs server host-key verification. The expected host fingerprint can be pinned per entry. This design does not protect a compromised Windows account, injected PuTTY process, administrator or kernel attacker, and does not prevent capture of PuTTY's own window.

The catalog is queried only on demand. No polling, module service or third-party module process is added. Registry writes for the isolated PuTTY session and real device protection could not be tested in this restricted execution environment; see VERIFICATION.md.

## Formatted clipboard content
Text HTML is stored inside the encrypted payload. AngleSharp parses it as data only; SableLatch does not create a browsing context, script engine or HTML renderer. Output is reconstructed from an element/style allowlist. Scripts, event handlers, remote images and active embedding are discarded. The clipboard writer sanitizes imported HTML again. Plain-text and HTML representations travel together; the receiving application's paste behavior is outside SableLatch's control.
