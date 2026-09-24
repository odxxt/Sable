# Architecture

## Boundaries

`SableLatch.Core` has no WPF, tray, registry, or clipboard dependency. It owns the versioned item schema, folder validation, deletion rules, session policy, authenticated encryption, storage transactions, encrypted attachments, backup envelopes, and provider contracts.

`SableLatch.Desktop` composes the core with Windows DPAPI, native session messages, NotifyIcon, global shortcut registration, clipboard adapters, and WPF views. Authentication, entry views/editors, settings, platform code, presentation catalogs, and the lifecycle are separate source units. The main window is currently the composition root and serializes core mutations; external plugins must not be handed this object or the store.

`SableLatch.Tests` is a dependency-light executable test suite. It injects an isolated test device protector and clock, exercises real Argon2/AES/RSA code, and compiles the screenshot provider example against the core interfaces.

## Extensible data

`VaultItem` contains an ID, content-type ID, content version, label, sensitivity flag, folder ID, ordering value, optional deletion timestamp, arbitrary JSON payload, and attachment IDs. Content-specific schema belongs to the provider. Core code never needs an image column or note-specific table to preserve a new type.

Binary content is stored in separate authenticated encrypted blobs. No plaintext thumbnail cache or attachment file is generated. Unknown or disabled providers retain their payloads and attachments and show an unavailable-provider placeholder. Folders, backup, Trash, and ordering work independently of a provider's implementation.

## Transactions and concurrency

Each mutation clones the current document, applies the change, validates it, serializes it, encrypts it, and writes a same-directory temporary file with a disk flush. An atomic replacement publishes it. The in-memory document changes only after success. A file digest prevents silently overwriting external edits. The desktop additionally holds an exclusive file lease and a named per-vault mutex.

Attachment writes precede metadata references; a crash may leave an encrypted orphan but does not publish a reference to a partially written blob. Orphan cleanup runs after successful deletion. Backups authenticate their attachments during restore before publishing a vault. Restore only targets an empty destination and preserves existing vaults. Filesystem rollback and old backup copies cannot be cryptographically revoked by local software.

## Session lifecycle

Password derivation and backups run off the UI thread, one at a time. A session generation counter prevents an in-flight unlock from reopening a vault after a Windows lock notification. Editors, list item sources, and the document reference are dropped on lock; the mutable data-key buffer is explicitly zeroed. Extension actions receive a session cancellation token.

Time-based locking uses a monotonic clock. The default one-hour absolute session is suspended while pinned; its elapsed age is considered immediately on unpin. Windows session lock/unlock, sign-out, suspend/resume, and explicit locking take precedence over pinning. Native session-notification registration must succeed before the release app allows vault operations.

Quick-PIN enrollment and the disposable process ticket live in `QuickUnlock.cs`, without a Windows/UI dependency. The desktop keeps this encrypted ticket separately from the unlocked store; quick lock still destroys the active data key and drops the document. Full-password startup mints a ticket, and PIN re-unlock cannot mint one while the store is locked. Password changes and sign-out invalidate tickets. `QuickUnlockViews.cs` provides centered typed PIN input, input feedback, and shortcut recording. The core exposes the remaining attempt count; rebuilding the view cannot reset the five-attempt budget.

`AuditJournal` stores fixed event codes through `IDeviceProtector`, with no credentials, identifiers, labels, paths, or exception messages. It retains at most 256 events, refuses files above 128 KB, and atomically replaces its protected file after a successful append. There is no reporting service, timer, or resident log cache. `EntryMetadata.MarkSaved` is applied by the editor composition layer so new entries receive creation/edit times while legacy creation times remain unknown. `AuditReport` renders a static, escaped HTML snapshot using metadata only; it never visits payload fields. Export is an explicit desktop action and emits readable labels/timestamps.

`RowInteractions.cs` owns hover, pending-login, and drag-insertion state. Login copying uses the provider's existing username/password actions and `KeepPanelOpen` contract; it does not introduce a second login page or alter the stored payload. `IClipboardService` separates OS clipboard effects from view orchestration, allowing the Debug fixture to test real row handlers with an isolated clipboard. Both the test clipboard and test device protector are excluded from Release. Unsaved editor drafts are cleared when leaving the editor or locking.

## Scaling path

The current storage implementation favors inspectability and a small feature surface: one encrypted metadata snapshot plus blobs. It is not a database for millions of entries. New storage implementations can retain the envelope/item/provider interfaces while introducing paged records and chunked backups. A public plugin ABI, broker, migration policy, and compatibility tests should be stabilized before distributing third-party extensions.

Audit initialization is deferred until the first real event (or normal exit); the actual startup timestamp is buffered as one value until then. A process killed before any event/exit may leave no startup record. This avoids loading serialization/crypto solely for an unopened tray process.


## Transfers and page composition (0.4)

`Transfers` owns the versioned password-only archive format independently of `Backups`. `TransferContents` carries only items/folders/attachments; it excludes host policy and credentials. `TransferViews` handles picker/review actions; `SetupViews` composes welcome, password, masked recovery, optional PIN and restoration steps. First-run restoration uses the active empty `VaultStore`, alongside its app lease. An existing vault is never replaced by restoration.

`Form` provides fixed navigation/heading/footer and a separately scrolling content region. `PageAction` supplies the standard editor Save or wizard Next action. Content/editor/provider registries remain independent of this page frame. Native Alt+Tab style follows the real pin state, not the temporary login-copy hold. PIN tickets synchronize validation and destruction; a UI submission latch spans verification and feedback.
## Page affordances and setup guide (0.5)

`ScrollAffordances` attaches an event-driven `ScrollEdgeOverlay` to visible WPF ScrollViewers. It listens to scroll/layout lifecycle events, draws only necessary edges, excludes the scrollbar strip, and removes overlays/handlers on unload so old pages are not retained. The scrollbar Track binds orientation/range/value/viewport to its ScrollBar; its thumb uses the real viewport proportion.

`SettingsDraft` snapshots ordinary control values and observes field changes. It keeps no additional password copy. `SettingsSave` installs a persistent footer and `NavigateSettings` implements the one-prompt discard flow. `SetBody` resets draft tracking before releasing the old page. Security locking bypasses navigation prompts.

`SetupGuideViews` composes only backup preferences, Trash retention and shortcut recording. Reviewed step IDs are persisted in encrypted Preferences; restore resets them for the destination environment. Folder icon IDs are opaque metadata on VaultFolder with a safe built-in vector fallback. No external icon files or executable extensions are loaded.

## Shared form submission (0.5.1)

PageAction registers the page's primary button, including SettingsSave footers. Welcome and lock forms explicitly register their primary button. The window's preview-key handler routes unmodified Enter to that button's existing click handler. SetBody clears the previous registration. Modifier combinations, focused buttons, open pickers, modal dialogs and shortcut capture keep their native behavior; busy operations, PIN feedback and key repeats cannot cause duplicate page submissions. Existing validation/authentication handlers are reused unchanged.

Bundled theme surface alpha is F2 (242/255, approximately 95%). Text opacity is not reduced. This adds no backdrop-blur renderer, composition dependency or extra process. Microsoft notes that acrylic surfaces can increase GPU/power usage: https://learn.microsoft.com/en-us/windows/apps/design/style/acrylic.
## Explicit advanced-entry actions (0.6.0)

AdvancedEntry is optional content-independent metadata on VaultItem. Old entries deserialize with no advanced settings. Color and EntryAction travel through the same AES-GCM document, mutation, Trash, backup and transfer paths as existing entries; payload schemas and clipboard providers do not change. Action strings have bounded lengths and reject null characters. Unknown kind IDs are preserved as inert data until a trusted matching handler exists.

IEntryActionHandler.Prepare returns a ProcessStartInfo. EntryActions.Register admits trusted handlers through explicit composition, refuses duplicate IDs, and does not install code. Four bundled handlers provide executable paths, Windows Settings/Control Panel destinations, PowerShell and CMD. The review coordinator checks unlock epoch, current item existence and an unchanged action snapshot before launch. Only Run action launches a process; form Enter is deliberately not registered on the review. Imported metadata never triggers execution. Existing erase callbacks clear command review/editor fields on navigation/lock.

Application paths must be fully qualified existing .exe files and are launched without a command shell; configured arguments remain separate. Windows destination syntax admits only ms-settings addresses or canonical Microsoft.* Control Panel names. System tools use absolute System32 paths. PowerShell receives Unicode Base64 with NoProfile and no plaintext temporary script. CMD uses /d /s /c, preserves the user-authored command, and never interpolates vault values. No elevation is requested, but these are ordinary account processes, not sandboxed extensions. The encrypted bounded audit records only an EntryActionStarted code.

WPF ShowInTaskbar and native tool/app-window styles are updated together; three encrypted preference modes are cached for the current session. Each list now has a local AdornerDecorator, with one overflow overlay attached after template layout and removed on unload. Folder shell height follows measured list content (252-pixel maximum viewport), with its bottom position retained across form/list transitions. Settings navigation has an everyday root and a separate Advanced hub.

References: https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/cmd and https://learn.microsoft.com/en-us/windows/win32/shell/taskbar

## Fixed panel and action editing (current behavior)

Window size is fixed at 342 × 411 logical pixels. Removed SetPanelHeight and content-measure-driven repositioning; all forms share the fixed panel. The list footer uses the remaining star row. ScrollEdgeOverlay uses a smooth vertical gradient multiplied by a horizontal opacity mask, with zero opacity on the sides and vertical ends, no blur effects or continuous animation timers.

Advanced is a compact header switch beside New/Edit entry, separate from the content-type choices. AdvancedEditorState owns capture, field-level validation and a SHA-256 identity of the last tested execution configuration. The right footer control stays Save entry, visually dimmed and guarded until the configuration has launched; a separate Test action sits left. Type switches preserve editor drafts and test identity, while action-kind switches intentionally clear target/arguments/folder. Label/color/confirmation changes do not force a redundant launch test. Disabled or absent actions do not require tests. Existing saved actions can be edited without retesting unchanged execution settings.

LaunchEntryAction is shared by testing, optional confirmation and direct row actions. Row invocation re-reads the current document by ID and checks deletion/module state. VerifyEachTime is encrypted metadata; false is the local default. Transfers.Import preserves this repeat-review preference. First-run review is independently enforced by device-local ActionApprovals for each entry ID and exact action fingerprint. No actions run during imports, backups, unlock or startup.

ApplicationCatalog is a bounded on-demand STA inventory of App Paths and Start-menu .lnk metadata. COM objects are released, no whole-drive scan is performed, and no process or cache stays resident after discovery. The highlighted overlay picker focuses search at the top, filters results below and offers Browse. Its async completion is checked against the current page and unlock epoch before touching the UI.

Plugin settings moved to the main Settings page after App preferences. Layout selection is omitted until an additional layout has registered and is saved with module settings; About is fixed in the main Settings footer.
## Local execution approvals and settings catalog (0.6.3)

ActionApprovals is a Core service using IDeviceProtector and atomic writes. It stores only bounded digests, a purpose/version and machine identity in actions.sableconsent, outside vault documents, recovery packages and transfers. Approval lookup fails closed. Row review and existing-entry test review grant approval only after Windows accepts a launch. New entries can be approved after their explicit successful test and save. Queued clicks from cancelled review surfaces cannot execute; the callbacks check the active page identity and unlock epoch.

WindowsSettingsCatalog supplies 208 built-in destinations with source category, MDL2 glyph and keywords. It is created only for an open picker; filtering needs no persistent index, scan or service. Six common suggestions precede any query. Selection opens the corresponding Windows URI or fixed control.exe canonical destination. The control-panel handler remains registered for older stored entries, while the editor uses one combined Windows category.

Closed dropdown wheels scroll the enclosing form; open dropdown wheels scroll the menu. Neither route changes the selection. An action-kind selection schedules its picker only if the same page, selection and unlock state still apply.
## Secret controls (0.6.4)

AdvancedEditorState captures colors independently from action enablement. The header switch controls the action, and its initial activation opens the kind menu with an empty selection instead of preselecting Application. Existing disabled actions retain their configuration. CopyOnRun is an optional encrypted EntryAction flag, included in ActionApprovals.Fingerprint. CompleteEntryRun routes successful saved Secret/Login invocations through the same CopyEntry path used by normal secret copies. Testing is execution-only. RowModel adds a copy-badge visibility binding without changing the row action button's dimensions. The main Settings footer calls the existing ExitApp lifecycle through Quit.
## 0.6.5 controls and platform protection

MaskedSecretBox replaces PasswordBox only in entry content editors. It keeps a bullet-only WPF text document, handles selection replacement/input/paste privately, suppresses undo and value automation, and retains the existing explicit eye-reveal and editor-erasure lifecycle. Authentication still uses PasswordBox. Field storage and content providers already accept line breaks; the vault format is unchanged.

Advanced controls separate type selection from its settings. A RadioButton segment group stores CopyOnRun for all built-in types. KeepTerminalOpen is optional encrypted metadata; the launch handler maps it to /k or -NoExit. Approval identity retains old hashes when the flag is false and includes it when true. Right-click and row actions share ReviewEntryAction and CompleteEntryRun, preserving review, failure and sensitive-copy rules.

CaptureProtection uses window display affinity on the main HWND and each menu/tooltip opening. Popup templates attach its Enabled property for their independent HWNDs. Every call checks the native result/readback; a failure stays visible in Security settings for the process session. There are no background processes, capture listeners or polling timers. Plugins that add custom Popup templates must attach the same property; external process windows are outside this policy.

## API 1 external packages (0.7.0)

Modules.cs owns declarative manifests, bounded ZIP inspection, signature hooks, schema identity and atomic package replacement. ModuleStore persists public packages separately from vault contents. ModuleApprovals owns machine-local consent digests. ModuleTrust owns the embedded publisher public key and signed, on-demand catalog downloads. ModuleViews owns installation/review/settings/type selection. PuttyViews is the protected host editor and consent coordinator; PuttyBroker owns Authenticode validation, controlled process launch and native credential-pipe handles. SecretBuffers avoids adding an immutable plaintext string copy during credential transfer.

No external package is loaded as code or handed the internal IContentProvider/IExtensionContext contracts. DeclarativeProvider is host code interpreting validated definitions. Built-in composition interfaces remain separate. VaultItem.ModuleSchema records field-security/handler identity; unknown, disabled or incompatible modules cannot edit or reinterpret their entries. Backups and transfers preserve that field through the existing item serialization. The manifest remains public while fields and extension settings remain under existing vault encryption.

Text entries may carry an optional HTML field alongside plain text. The desktop adapter parses it with AngleSharp without a browsing context, permits a small formatting allowlist, and writes CF_HTML plus UnicodeText. Clipboard offsets are UTF-8 byte offsets. Import output is re-sanitized. No HTML is rendered or executed in the app. The plain-text editor invalidates the HTML representation on editing.
