# Sable user guide — 0.9.7 preview

Sable keeps secrets, logins, useful commands and server connections in a compact Windows tray app. Everything saved in the vault is encrypted, including shortcuts, descriptions and folder names. It needs no online account.

## First run

Extract the **entire portable ZIP** to a writable folder and open `Sable.exe`. The runtime is included; keep all accompanying files together. The welcome page offers **Create your vault** and **Restore from backup**. Encrypted import is available in Settings after opening a vault.

Setup guides you through a password of at least 12 characters, a recovery key and an optional 4–12 digit PIN. Back keeps your drafts, and Enter activates Next. The key starts hidden. Reveal it when ready; click it to copy with an in-place green confirmation, or press **Save key** to save a **readable, unencrypted file**. Keep it private and separate from the vault. Confirm that you saved it before continuing. The key is shown only when generated; Settings can replace it after password verification, not reveal it again.

After setup, **Create my first secret** guides you through the actual + menu, starter chooser and editor. Saving creates a real entry; clicking it copies the saved value. Skip dismisses the guide. The pending guide can resume after locking or restarting. The welcome illustration loops only while visible; motion honors the Windows animation preference.

## Create an entry

Open **+ → New entry**, then choose a described starter:

| Starter | Use it for | Default click |
| --- | --- | --- |
| **Secret or login** | A saved value, or a username and password | Copy; a login copies username, then password |
| **Shortcut** | An application, file, folder, Windows setting or command | Run |
| **Connection** | Installed SSH or Remote Desktop modules | Launch |
| **Extras** | Installed utility modules, starting with Type clipboard | Run the selected tool |

Connection appears only when an enabled connection module is installed. Other field modules are grouped under Extras by default; a manifest can choose a supported category. All available categories fit without scrolling. Module installation stays in Settings. [The entry-model guide](ENTRY-MODEL.md) includes a diagram and all click-behavior details.

Without a username, the input says **Secret value**. Use **Add username** above it to reveal Username and change the value caption to Password. **Remove username** returns to a secret. Updated fields briefly fade in. Values survive toggling while editing; a removed username is not saved. **Change** preserves each starter's unfinished fields and shares the latest name until you leave the editor or lock.

Use **Shift+Enter** or paste to enter multiple lines. The eye at the right reveals the value temporarily; a slash indicates that it is revealed. **Color & description** contains small color swatches and an optional description for the entry tooltip. Tooltips never show saved secrets. Save stays at the bottom while fields scroll.

### Secrets with an optional shortcut

Expand **Advanced → Include a shortcut** to add an independent **Run / Run and copy** button. The main secret row still copies. For a login, Run and copy launches once and copies the username; the next row click copies the password without launching again. Failed launches and cancelled reviews do not copy. Test action tests execution only.

### Configure a shortcut

Available actions are Application, File or folder, Windows & Control Panel, Run, PowerShell and CMD. Selecting Application or Windows settings opens its focused picker. Cancel returns to the editor. Changing action type clears its old target, arguments and starting folder. Mouse-wheel scrolling does not change dropdown selections.

- **Application:** select a detected desktop app or Browse for an `.exe`. Optional arguments stay separate from the executable path. Discovery is on demand, not a whole-drive scan.
- **File or folder:** open a selected destination using its associated Windows application.
- **Windows & Control Panel:** search the combined built-in catalog. Availability depends on your Windows build, installed software and policy; it is not an exhaustive live inventory.
- **Run:** use Windows+R-style resolution, such as `notepad`, `control printers` or `%APPDATA%`. Quote executable paths that contain spaces when adding arguments.
- **PowerShell / CMD:** write a script or invoke a script file. **Keep terminal open when finished** is on by default. Disable it to close on completion; an explicit script exit can still close the terminal. Optional **Run as administrator** requests Windows UAC for supported actions.

**Starting folder · optional** controls relative paths: a script reading `report.txt` can start in `C:\Reports`. Leave it blank for the Windows user folder. Most actions do not need it.

When using Copy or Run and copy on a shortcut, enter its required `$value1` under Copy value (or Script fields for PowerShell). A missing value is focused and highlighted directly. A green check briefly replaces Run when a launch is accepted; it confirms startup, not command completion.

Press **Test action** before saving a new or changed enabled action. The separate Save button stays dimmed until its current configuration has launched successfully. Testing runs the action; it does not prove that a script is safe or completes successfully. Changed execution settings, including copy mode, require another test. Names/colors alone do not.

Saved actions require first-use review on this Windows environment unless explicitly tested and approved locally. Imported actions need fresh approval; **Verify each time** optionally asks on every run. Commands are not sandboxed. Locking cancels pending launches, but does not stop established external programs.

PowerShell can explicitly receive `$name` and encrypted `$value1` … `$value20` through **Pass this entry's fields to PowerShell**, off by default. Custom fields appear only where relevant. See [variable mappings, examples and exposure limits](ENTRY-MODEL.md#script-fields). CMD has no secret-field binding.

### Preserve formatting

Paste HTML-formatted content into an empty secret, or select its entire value before pasting. Sable retains sanitized bold/italic text, lists, links, common styles and tables alongside plain text. Supporting email and document apps choose the formatted representation when you copy; plain-text apps get plain text.

The masked editor shows **Formatting kept**. This is preservation, not a rich-text composer. Typing or partially replacing a value clears its old formatting; paste the whole source again to restore it. Login fields are plain text. Scripts, forms, remote images and embedded objects are removed; no external resources are fetched. Complex layouts and RTF-only sources are not preserved exactly.

## Type clipboard

Install **Type clipboard** from Settings → Plugins & modules → Add an extra. Create a reusable entry under **+ → New entry → Extras → Type clipboard**, or right-click the tray → **Type clipboard**. The tray action also works while the vault is locked. It always types the current clipboard, not a saved entry value.

Sable hides, the cursor becomes a crosshair, and you click the destination. The default delay is 500 ms, followed by 25 ms between characters. Esc cancels remaining input. A different foreground window or a Windows/session lock cancels the operation. Already typed text remains. Internal line breaks/tabs are sent; trailing Enter/Tab characters are stripped and no extra Enter is added. Multiline confirmation is on by default.

Module settings control global timing, input mode and multiline confirmation. Entries default to **Use module defaults**; turn it off for independent overrides. The tray uses global defaults. Automatic mode is now the default: it supplies virtual keys and scan codes for ordinary characters and uses Unicode for characters unavailable in the local destination layout. Unicode text and strict keyboard compatibility remain selectable. Existing default-Unicode preferences migrate once, keeping timing. Type prompts appear at the bottom right of the pointer's monitor. See [Type clipboard](TYPE-CLIPBOARD.md) for limitations and sensitive text handling.

## Open, pin and lock

Open from the tray or use **Ctrl+Alt+Space**. A locked panel immediately focuses PIN/password input, including when already visible. Change the shortcut with the recorder in **Settings → App preferences**. Drag the header to move Sable; double-click its name to dock at the bottom right of the monitor it is currently on, including a secondary display. Saved window positions on available secondary monitors are preserved.

The pin cycles these modes in a fixed-size toolbar slot:

| Mode | Stays open after focus changes/copy | Always on top | Taskbar / Alt+Tab by default | Session timeout |
| --- | --- | --- | --- | --- |
| **Tray** | No; copy preferences apply | Only temporarily between login copies | Hidden | Active |
| **Always on top** | Yes | Yes | Shown | Enforcement paused |
| **Normal window** | Yes | No | Shown | Active |

The mode button shows a **window with a minus sign** for Tray mode, an **upright pin** for Always on top, or a **window with a title bar** for Normal window. These static symbols share the same toolbar slot. Normal window stays behind other applications, including during login copying. Its taskbar button supports normal minimize/restore behavior; the tray and global shortcut also restore a minimized panel. Panel dimensions remain fixed. **Settings → App preferences → Taskbar & Alt+Tab** offers When kept open, Always and Never; explicit Always/Never overrides the table. Modes are session-local and start in Tray when the process starts.

The default session limit is 60 minutes. Changing modes never resets its clock. Leaving Always on top immediately applies an overdue timeout. Windows lock/unlock, sleep/resume, sign-out and explicit Lock apply regardless of mode. **Lock when reopened** can require authentication whenever the panel is reopened, except in Always on top mode.

The full password is required at every process start. An optional 4–12 digit PIN unlocks timer/manual/session locks while that process remains running. Five incorrect PINs discard the quick session and require the full password; only the last three remaining attempts are shown. Verification/error feedback clears and disables input to prevent overlapping attempts. Change or disable the PIN in Security & locking after full-password verification.

Close hides the panel. **Settings → Quit → Confirm quit** exits. Enter saves/continues; Shift+Enter adds a line. Escape cancels a menu confirmation, returns from an editor or hides the main panel. Focused buttons, open pickers and shortcut recording retain their native key behavior.

## Organize and delete

The **+** menu offers New entry and New folder. Choose rows or the two-column grid with the view button; root and each folder remember their own view. Search uses labels, not secret values. Folder chips fit their names up to a maximum width, show full names in tooltips and remain in one row. Wheel scrolling and drag-edge scrolling reach overflow.

Grid tiles show their entry type on the left. If clicking the tile already runs it, there is no duplicate Run button. If clicking copies a value and the entry also has an action, the separate Run button remains. Copy/launch feedback briefly replaces the left icon without moving the text. The Shortcut symbol is a curved arrow leaving a rounded box; native entry icons use shared optical sizing and stroke weight.

The approved entry icons move slightly on hover: the secret key turns, login profile lifts, Shortcut arrow advances, SSH/RDP prompts shift, Type key depresses and Run actions respond. Movements reverse from their current position and settle without an idle loop. Windows animation preferences are honored. Folder, search, settings, close and window-mode symbols stay static.

Rows and grid cards now share a 56px height and consistent space between the icon, text and Run action. The Run symbol is smaller while its click target remains comfortable. Changing views briefly fades the new arrangement into place without resizing the panel; Windows reduced motion skips the fade.

Drag entries to reorder; an insertion line marks the destination. Drop onto a folder to move them or onto the parent breadcrumb to move up. A centered check fades over the folder's text, then the text returns without resizing the chip. Alt+Up/Down reorders a focused entry.

Drag a folder to a sibling's edge to reorder, or its center to nest the whole subtree. The preview follows the pointer. Dropping onto the parent breadcrumb moves it up. Right-click → Move folder and Alt+Left/Right are alternatives. Self/descendant moves are rejected. Edit folder changes its name, parent and optional built-in icon.

Under **Edit folder → Customize**, choose from 20 static symbols. Alongside Folder, Work, Home, Star and Private are Servers, Network, Cloud, Code, Database, Terminal, Documents, Downloads, Media, Mail, Finance, Tools, Devices, Web and Archive. Hover for the name. These icons do not animate.

Entry right-click menus keep **Edit, Duplicate, Move to folder, Delete** in order, with available Run/Launch actions above them. Duplicate opens an unsaved independent draft; Back creates nothing. Copies receive new IDs/timestamps on Save and fresh action approvals.

**Delete** changes that same menu item to **Confirm delete**. Confirm again, or dismiss/Escape to cancel. Secret/login entries go to encrypted Trash by default; ordinary entries delete permanently. Retention and delete preferences are configurable. **Only already-trashed items expire after seven days** by default; active entries do not.

**Delete folder** opens its editor at the deletion section. The first press shows affected counts and an irreversible warning; a second deliberate press confirms after a brief accidental-double-click guard. This permanently removes the entire subtree, including active and trashed entries. Independent backups/exports are unchanged; deletion is not guaranteed physical erasure from the storage device.

## Modules and connections

Open **Settings → Plugins & modules**. Installed modules show a check and their version; click one for its settings. **Add a connection** offers SSH · PuTTY and Remote Desktop if not installed. These included packages install offline. Use **Install from ZIP…** for another package, or **Online catalog → Check GitHub catalog** for the signed catalog. There are no automatic catalog checks. **Default entry types** controls the built-in choices.

ZIP modules are restricted definitions, not executable plugins. They cannot read secret values or run arbitrary background code. Uninstalling/disabling leaves entries encrypted and unavailable until a compatible module is restored. See [the module guide](../modules/README.md) for samples, signatures and supported capabilities.

### SSH with PuTTY

Install SSH · PuTTY, then create **+ → New entry → Connection → SSH · PuTTY**. Enter name, host, username and password. **More options** contains port, expected server fingerprint, IPv4/IPv6 preference, compression, keepalive and font/size. Module settings detect PuTTY or let you Browse. If absent, Download PuTTY opens the official download site; PuTTY is not bundled.

Click launches PuTTY by default. The first-use review can remember the connection on this Windows environment; changes to target/options, package or verified executable ask again. Host code sends the password only through a protected, random, single-use named pipe after checking that the reader is the exact launched PuTTY process. The launch path does not put it in arguments, environment, a file, logs or clipboard. Explicit Copy/Run and copy choices can copy it through Sable's sensitive clipboard path. The unlocked document and PuTTY still hold credentials in memory.

PuTTY checks the server's host key. An expected fingerprint can be configured. Locking cancels pending delivery; established SSH sessions remain open. SSH private keys, saved PuTTY sessions, proxies/tunnels and remote commands are not implemented.

### Remote Desktop

Install Remote Desktop, then create **+ → New entry → Connection → Remote Desktop**. Enter host, Windows username and password. **More options** includes domain, port and Start in full screen, enabled by default. Initial sizing, native resize/full-screen controls and normal bidirectional clipboard sharing belong to Windows mstsc and server policy.

Click launches by default. **When I click the entry** can select Copy password, Run or Run and copy password; Launch / Launch & copy are also explicit menu actions. Copy uses the stored password. Launch & copy copies after local startup, not confirmed remote authentication. Both kept-open window modes leave Sable open.

For each launch, Sable creates a temporary `.rdp` file with a DPAPI-encrypted password, opens system `mstsc.exe` and removes the file after startup, with retry/next-start cleanup. No permanent RDP file is kept in the vault, no Credential Manager entry is written, and no proxy is used. **Software running as your Windows user can capture and decrypt that file.** Cleanup cannot revoke a captured copy or guarantee secure deletion.

Windows may show publisher, certificate or credential prompts. Sable leaves certificate-warning choices to Windows and does not bypass NLA/CredSSP or organizational policy. Native Smart sizing scales the desktop; it is not dynamic remote-resolution negotiation on every resize. Established sessions can survive Sable locking/exiting and are not covered by its capture protection. See [RDP lifecycle and limitations](RDP-DESIGN.md).

## Settings, clipboard and activity

Everyday settings cover Security & locking, Clipboard & copying, App preferences and Plugins & modules. Advanced contains Backups & restore, Import & export, Trash & retention and Activity & audit. About and Quit sit at the main settings footer. Save stays fixed at the bottom. Back with unsaved changes first asks you to save; press Back again to discard. Further editing resets that reminder. Locking always discards drafts.

After three active saved entries, **Continue setup** offers backup preferences, Trash retention and the global shortcut. Completed items are dimmed. Backups can remain off; finishing the checklist is not proof that a backup exists.

Secret copies clear after 30 seconds by default if Sable still owns that clipboard content. Delay and clearing can be changed. History/cloud-exclusion formats and supported capture exclusion are requested. Other software can read the clipboard; capture protection cannot block every recorder, privileged process or camera. Windows 10 support differs by build; [Security](../SECURITY.md#screen-capture) records the fallback and unverified capture paths.

Network suggestions are built in; no saved shortcut is required. Short clipboard text such as “the ip is 192.168.8.1” offers Ping; an inline `192.168.8.1:53` or “192.168.8.1 and port 53” offers Test port (Test-NetConnection). Ports must be 1–65535 without leading zeros. Separate ports are paired only with one unambiguous IP; unrelated numbers are ignored. Up to three targets are shown. DNS lookup requires a dotted domain/URL such as `alodat.me`; copying `book` creates no suggestion.

Only clipboard items of 512 characters or fewer are inspected. Larger items are skipped entirely; there is no history, polling worker or regex engine in this scanner. Analysis happens only while the unlocked **All items** list is visible and not minimized, and skips Sable-owned/protected copies. Folders remain quiet, including when you copy new text there. Returning to All items examines the current clipboard without retaining a history.

A click launches the fixed diagnostic with the normalized target and requests the foreground once; nothing runs automatically. The **first X click** offers **Only this copy** or **Don't show again**, inside that same card. Sable remembers showing this choice; later X clicks dismiss the current suggestion immediately. Only this copy hides it until the clipboard changes. Don't show again disables network suggestions immediately. Toggle **Suggest network checks from clipboard** in Clipboard & copying at any time. Existing saved diagnostic entries still work; arbitrary scripts never receive clipboard substitutions.

Entry creation/edit dates are encrypted. **Advanced → Activity & audit → Save HTML report** exports a static readable report with labels, types, timestamps and Trash state, never payload values. Saving over a previous file refreshes it. Store that report privately. The separate device-protected journal retains at most 256 fixed-code events/128 KB, without a reporting server or background cache. It is operational history, not a tamper-proof forensic log; old unknown creation dates are not guessed.

## Backups, exports and recovery

| Operation | Required credentials | Effect |
| --- | --- | --- |
| Normal unlock | App password in the registered Windows environment | Opens the vault; session PIN is optional afterward |
| Recover a moved vault / forgotten password | Recovery key and complete data folder | Rebinds access and sets a new app password |
| Restore `.sablebackup` | Backup password **and** that backup's recovery key | Creates/restores a vault at an empty destination |
| Import `.sableexport` | Export password | Adds independent entries to the open vault |

**Backups start off.** Enable them in Advanced → Backups & restore, choose a destination and backup password. Once enabled, changed unlocked data is backed up when due; initial cadence is 24 hours with seven archives retained. Restoring to a new environment leaves automatic backups disabled until configured. Restoring while a vault exists creates a separate copy, not a replacement.

Advanced → Import & export uses a separate export password. It includes folders, entries including Trash, timestamps, unknown types and attachments. Imports assign new IDs and preserve destination credentials/settings. Importing twice creates duplicates. Passwords/PIN enrollment, backup configuration, local action approvals and audit history are not transferred. Unknown module entries remain unavailable until installed separately.

Random salts and Argon2id make password guessing more expensive; offline archives cannot enforce attempt limits. Use long unique passphrases. Losing an export password cannot be fixed with the vault recovery key. A replacement recovery key applies to the current vault, not older backups/copies. If both normal access and the appropriate recovery key are lost, a reset cannot decrypt the old vault; preserve the files and start a separate vault.

## Updating or moving

Quit the old app, extract the new version separately and copy the **complete** data folder beside `Sable.exe`, including attachments. Fresh installs use `Sable-data`; existing `SableLatch-data` is detected and takes precedence if both names exist. Keep the original as a rollback copy. On the same Windows environment, open normally. On another environment, use the recovery key or restore a backup with both credentials.

A custom data location uses `Sable.exe --data-dir "C:\path\to\vault"`. Do not run two versions against one vault or alternate old versions after saving newer fields. Existing Text entries become Secrets after authenticated loading, preserving values, formatting, IDs and dates; changes persist on the next save. Legacy namespaces and crypto identifiers intentionally remain compatible.

## Preview limits and development

This is an unaudited preview for Windows 10/11 x64. A full Windows/DPI/capture compatibility matrix remains outstanding. Native WPF has no browser engine or resident service, but memory use is not zero: Argon2 uses 64 MiB during derivation, unlocked metadata is one document, and large transfers need temporary memory. Attachment limit is 64 MiB each; metadata and archive limits are 256 MiB. Long entry lists are virtualized.

Executable third-party plugins, screenshot capture UI, SSH-key authentication, a rich-text composing editor, browser autofill, installer mode, auto-updates, application code signing and additional languages are not shipped. Theme presets are data-only. Window position and shortcut metadata are unencrypted `window.json` / `host.json`; they contain no vault values.

Build with a .NET 10 SDK on Windows:

```powershell
.\build.ps1 -Portable
```

The build script runs core and Windows module checks, then publishes to `artifacts/Sable-portable`. Debug-only fixtures require a fresh disposable data directory and must never use a real vault. See [Verification](../VERIFICATION.md), [Architecture](../ARCHITECTURE.md), [Security](../SECURITY.md) and the [documentation index](README.md).
