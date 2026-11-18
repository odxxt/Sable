# Sable architecture — 0.9.7 preview

The public app name is Sable. Existing `SableLatch` namespaces, cryptographic purposes, module IDs and Windows binding identifiers are compatibility details, not a second product. The current architecture is below; historical changes belong in [CHANGELOG.md](CHANGELOG.md).

## Boundaries

| Project | Responsibility |
| --- | --- |
| `SableLatch.Core` | Versioned item/folder schema, session policy, encryption, atomic storage, attachments, backup/transfer formats, approvals and provider contracts; no WPF or registry dependency |
| `SableLatch.Desktop` | WPF views, Windows DPAPI, tray/global shortcut, clipboard, session messages, native process launch and module composition |
| `SableLatch.Tests` | Dependency-light core tests with isolated device protection and clocks, real crypto, and compiled extension examples |
| `SableLatch.ModuleTests` | Windows credential-pipe, native launcher and file-lifecycle checks; environment-dependent results are recorded separately |

`MainWindow` is the desktop composition root and serializes mutations. Its partial source units separate editors, actions, settings, onboarding and platform interaction. It and `VaultStore` are never handed to external modules. There is one tray process; launched applications, including mstsc sessions, have their own lifetimes. No embedded browser, resident service or module worker is introduced.

## Encrypted data and transactions

`VaultItem` carries identity, provider ID/version, label, sensitivity, folder/order, timestamps, optional deletion timestamp, arbitrary JSON payload and attachment references. Additive encrypted properties hold Description, ClickBehavior, ScriptValues and Advanced action/color metadata. `VaultFolder.Layout` stores its own row/grid preference; the root uses the document preference.

Binary attachments are separate authenticated encrypted blobs, referenced by UUID. There is no plaintext thumbnail cache. Unknown, disabled or schema-incompatible providers preserve their payload and attachment references; the UI marks them unavailable rather than reinterpreting them. Legacy Text becomes Secret after authenticated loading, preserving identity/content and persisting on the next write.

Each mutation clones, changes, validates, serializes and encrypts the document, flushes a same-directory temporary file and atomically replaces the old file. Memory changes only after success. A digest rejects external changes, and the desktop also holds a per-vault mutex and exclusive file lease. Attachment writes precede metadata references; failures can leave encrypted orphans, not committed references to partial blobs. Cleanup checks surviving references after a successful deletion.

Backup and transfer formats are distinct. Two-credential backups retain the recovery gate and authenticate attachments before restoration to an empty destination. Password-only transfer exports carry entries/folders/attachments, remap IDs on import and omit host credentials/settings/approvals. Rollback copies and independent archives cannot be cryptographically revoked. See [Security](SECURITY.md) for algorithm parameters and threat boundaries.

`PortableData` uses an explicit data directory when supplied; otherwise an existing `SableLatch-data` takes precedence over fresh `Sable-data`. Window position and global-shortcut metadata remain outside the encrypted vault so the app can open while locked.

## Sessions and window modes

Password derivation and backup work run off the UI thread, serialized. Unlock completion checks a session generation counter so an in-flight operation cannot reopen after a Windows lock. Lock releases the document, editors, lists and drafts, zeroes the owned data-key buffer and cancels pending host operations. Native session-notification registration must succeed before Release allows vault operations.

`SessionPolicy.Mode` separates visibility from stacking and authentication:

| Mode | KeepsPanelOpen | Topmost | Timed lock |
| --- | --- | --- | --- |
| Tray | false | Only for a pending username/password copy sequence | Enforced |
| AlwaysOnTop | true | true | Enforcement suspended |
| Window | true | false | Enforced |

Time uses a monotonic clock. Cycling modes never resets the deadline; leaving Always on top applies an overdue lock immediately. Windows lock/unlock, suspend/resume, sign-out and explicit Lock take precedence. `IsPinned` remains a compatibility accessor for AlwaysOnTop. Modes start in Tray for each process and are not persisted.

`PanelModes` centralizes mode cycling, deactivation and flags. `KeepsPanelOpen` governs copy and SSH/RDP completion; `NeedsTopmost` allows the temporary login hold only in Tray. Distinct static tray/pin/window drawings share a fixed 16-pixel drawing area/28-pixel button. The compatible taskbar preference `pinned` now means both kept-open modes; `always` and `never` override it. WPF and native taskbar/tool-window flags update together, and capture protection is reapplied.

`QuickUnlock` retains a process-only encrypted ticket after full-password unlock. Quick lock still destroys the active data key/document. Five failed PINs destroy the ticket; synchronized validation plus a UI submission latch prevents overlapping attempts or view reconstruction from resetting the budget. Password/PIN changes invalidate it. The typed lock input receives focus after layout/activation without stealing focus for a background timeout.

## Entry composition and interaction

`EntryChoices` presents described starter rows: Secret or login, Shortcut, installed Connections and other field modules. `EditorViews` retains in-session drafts while changing starters and combines the existing Secret/Login providers through an optional username. Provider IDs and payload formats are unchanged. Leaving or locking clears drafts/callbacks.

`EntryDetails` owns color, tooltip description and applicable primary-click preferences. Secret/Login saves use Copy; Advanced → Include a shortcut independently enables Run or Run and copy. Login Run and copy launches once, copies username and waits for the next row copy to deliver the password. Existing entries retain legacy behavior until edited. Shortcut/Connection primary behavior remains configurable.

Connection placement derives only from validated installed `putty`/`rdp` handlers and enabled-module preferences. Field modules remain their own starters. No package executes UI code or grants itself a new capability. `GroupedOptions` supplies padded disclosure sections for Advanced, More options and Color & description.

`Form` owns fixed navigation/footer and a scrolling content region in the fixed 342 × 411 panel. Enter routes to the registered primary button, except during native picker/button handling, shortcut recording or busy feedback. Shared gutters, centered back controls and padded icon slots avoid layout shifts. Brief opacity/press transitions honor Windows animation preferences and stop on unload. `SettingsDraft` implements one reminder before Back discards changes; locking bypasses prompts.

`RowInteractions`, `GridEntries` and `TwoColumnPanel` share entry models, copy/run handlers and context menus. The grid virtualizes visible tiles plus a small buffer. Direct dragging displays insertion feedback; no reorder mode is required. The entry menu confirms deletion in place, then invokes the existing permanent/Trash policy.

Both layouts use 56px visible cards on a 60px pitch. Entry identities render at 18px with a 12px text gap; Run renders at 16px within a 28 × 32px target inset 8px from the card edge. `EntryIdentity` supplies its 20px default through property metadata so templates can override it. Explicit layout changes use one 140ms opacity transition, remove its clock on completion/unload, and respect the Windows animation preference. No dimensions, background timers or additional processes are animated or introduced.

`FolderOperations` validates destinations and rejects cycles. `FolderInteractions` accepts only the active local drag and commits on drop. Edges reorder siblings; centers nest. `FolderRail` uses natural-width capped chips with overflow scrolling, and `FolderDragPreview` follows the pointer. `FolderConfirmationAdorner` overlays a check while fading the mounted label, outside layout measurement. `FolderDeletion` requires two deliberate presses and removes the selected subtree including Trash only after confirmation. Independent backups are unaffected.

`ItemOperations.DuplicateDraft` deep-clones a fresh ID, label and timestamps only on Save. Immutable attachment references can be shared; cleanup checks all surviving references. Action approvals are never copied.

## Explicit actions and PowerShell

Trusted `IEntryActionHandler` implementations prepare launches; registration is application composition, not an external code loader. Application, file/folder, Windows/Control Panel, Run, PowerShell and CMD are offered for new actions. Legacy diagnostic handlers remain readable/editable for compatibility.

`AdvancedEditorState` validates fields and tracks the exact tested action fingerprint, including copy behavior. Successful launch acceptance unlocks Save, not script correctness/completion. Action-kind changes clear old target/arguments/folder. The coordinator rechecks the active session, entry, deletion/module state and action snapshot before launch. Explicit first-use consent is device-local; VerifyEachTime is a separate encrypted preference. Import, unlock and startup never execute actions.

`ActionApprovals` stores at most 2,048 entry/action digests in device-protected `actions.sableconsent`, excluded from backups/exports. Fingerprints include execution options such as terminal lifetime, copy mode, elevation and variable access. Missing/corrupt/foreign data fails closed. Ordinary action consent does not attest to the target binary's safety or contents; PuTTY has separate executable verification.

`PowerShellBroker` uses the native `CredentialPipe`: random name, first-instance/local-only/current-user restriction, live launched-process handle and PID verification, deadline and lock cancellation. A JSON packet carries the script and explicitly bound `$name`/numbered fields; the command line has a fixed loader and pipe name. Values are data, never script substitution. CMD has no secret binding. Optional elevation uses Windows UAC; another account cannot use the original user's pipe. Owned transfer buffers are zeroed, but managed strings, child memory and auditing remain exposure boundaries. Established child processes survive later lock. See [Entry model](docs/ENTRY-MODEL.md).

`ApplicationCatalog` performs bounded on-demand App Paths/Start-menu discovery on an STA worker and releases COM objects. `WindowsSettingsCatalog` provides a small built-in searchable destination list. Neither maintains a background index. Pickers reject stale async completion after navigation/lock.

## Clipboard and presentation

`IClipboardService` separates Windows effects from orchestration. Secret copies request history/cloud exclusion; clearing checks ownership before touching later content. A fake clipboard is available only to Debug fixtures. `MaskedSecretBox` keeps the WPF document bullet-only, disables copy/cut/undo/value automation and handles multiline input privately. Reveal uses a separate transient editor; lock/navigation clear both. This does not protect decrypted process memory from same-user malware.

Formatted secrets keep sanitized HTML beside plain text. AngleSharp parses data without a browsing context. The host reconstructs an allowlisted CF_HTML representation with correct UTF-8 offsets, sanitizes imported output again, and never fetches resources. Changing the value invalidates its old formatting.

`ClipboardSuggestions` listens while the unlocked All items list is visible and not minimized, bounds text reads, skips self-owned/protected formats and holds only a parsed target. Folders skip analysis and rendering; returning to All items evaluates only the current clipboard. One-time dismissal explanation state is an encrypted preference; dismissed targets use only a sequence number and slot. `ShortcutTargets` rejects invalid hosts/ports/shell syntax. Built-in diagnostics use a bounded 512-character linear scanner independently of saved entries. It extracts IPs and canonical inline/labelled ports, caps three targets, skips oversized allocations and only invokes the trusted fixed launch handler after a click. Existing saved diagnostics remain compatible. Hiding/navigation/lock clears suggestions. No history, network inference, automatic execution or arbitrary script substitution is added.

`DiagnosticTerminal` tags the launched trusted console command with a random non-secret window title, grants its process foreground permission and makes a bounded native handoff. It supports classic console and delegated Windows Terminal window classes, stops if the user switches elsewhere, and has no idle listener or permanent topmost flag. It does not alter arbitrary scripts or credential broker paths.

`EntryIdentity` caches frozen native vector parts with optical keylines and a uniform 1.25px stroke. Rows, grid tiles, Run controls and matching starter/welcome identities reuse them. The eight approved entry/action identities animate only on entry hover; other contexts remain static. A finite 460ms native clock uses the approved easing, snapshots its current pose on interruption, and is removed at completion. Hiding/unloading resets motion and unsubscribes the owning button. Windows reduced motion disables movement. No icon web runtime, frame subscription, polling loop or idle animation is bundled. Launch-only grid tiles omit a redundant secondary Run button; copying entries keep a distinct Run action. Copy/run feedback replaces the left grid identity in a fixed slot.

`CaptureProtection` requests and checks native display affinity for the main HWND and popup HWNDs. Windows 10 uses an opaque rounded configuration; Windows 11 keeps transparency. Older builds/failures may fall back to blank-content protection. No polling or capture worker is used; native dialogs and launched applications remain outside the policy. [Security](SECURITY.md#screen-capture) records the limits.

## Modules and remote connections

`Modules` validates API 1 JSON, bounded flat ZIPs, versions, signatures and schema identity. `ModuleStore` atomically retains public packages outside the vault. `ModuleTrust` verifies the embedded publisher key and signed, on-demand GitHub catalog. `ModuleApprovals` owns device-local consent. `DeclarativeProvider` is trusted host interpretation; packages never receive `VaultItem`, `VaultStore`, keys or `IExtensionContext`. Payloads/settings remain encrypted and missing modules preserve data.

`PuttyViews` owns the protected editor/review; `PuttyBroker` validates the signed executable, isolates non-secret session settings and sends a password over a random PID-verified pipe. It cancels pending delivery on lock/failure, not established sessions. No private-key authentication is shipped.

`RdpProfile` validates settings; `RdpConnectionFile` emits an allowlist; `NativeRdpSettings` creates a scoped current-user scratch file containing a DPAPI CurrentUser password blob. `NativeRdpExecutable` resolves system mstsc; `NativeRdpLauncher` coordinates startup and best-effort cleanup. There is no embedded RDP window, Credential Manager write, proxy or RDP password-pipe protocol. Same-user software can capture/decrypt the temporary file. [RDP design](docs/RDP-DESIGN.md) details lifecycle and native defaults.

Internal content/editor/presenter/clipboard/layout/toolbar interfaces support trusted composition. They are not the public ZIP API or a sandbox. See [Extensions](EXTENSIONS.md) and [module authoring](modules/README.md).

## Setup, audit and scaling

`TypeClipboardText` bounds/validates text, removes trailing navigation keys and normalizes CRLF to one input unit. `TypeClipboardRunner` sends one scalar/key at a time with cancellable delays and destination checks. `NativeTyping` owns operation-scoped mouse/keyboard/foreground hooks, cursor restoration and Unicode/layout-based SendInput; disposal removes every hook. It neither opens the vault nor logs text. `TypeClipboardReader` reads bounded CF_UNICODETEXT. The optional signed `type` manifest appears under Extras and in the tray. Global non-secret `type-host.json` preferences permit locked tray use; entry overrides remain encrypted. UI dialogs explain first use/multiline navigation without previewing clipboard content. Foreground checks reduce accidental delivery but are not an atomic secure input channel. See [Type](docs/TYPE-CLIPBOARD.md).

Module categorization is optional metadata (`auto`, `connection`, `extras`); handler/field security fingerprints stay stable. The root chooser sizes its rows for three or four categories; utilities are grouped on a separate Extras page. New protocols/capabilities still require reviewed host code. New public Type identities use the Sable name.

`SetupViews` composes password, hidden recovery key and optional PIN with preserved Back drafts. `OnboardingScene` loops native animation only while visible; reduced motion settles it. Recovery feedback overlays the key and releases textbox capture. Save key deliberately writes a readable key file. After creation, `FirstEntryTour` guides the real editor; its pending flag is encrypted. `SetupGuideViews` appears after three active entries and records only backup/Trash/shortcut review progress, not proof of a backup.

`AuditJournal` stores fixed codes and UTC times through device protection, with a 256-event/128-KB cap and no payloads/labels/paths. Initialization is deferred until an event or normal exit; a killed unopened process may leave no startup record. `AuditReport` explicitly exports escaped metadata as static readable HTML, not a live reporting service or forensic ledger.

Storage remains one encrypted metadata snapshot plus blobs. Virtualized views bound UI realization, but metadata is decrypted as a whole and mutations rewrite it. Paged storage/chunked transfer, additional protocols and executable extensions require separate implementations and compatibility/security work. No zero-memory-use or unlimited-scale claim is made. See [Verification](VERIFICATION.md) for current checks and platform gaps.
