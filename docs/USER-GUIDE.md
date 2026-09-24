# SableLatch 0.7.2 — portable preview

A compact, local clipboard vault for Windows. A native WPF window and tray icon; no browser engine, service, telemetry, or network listener.

## Run the portable app

Extract the **entire** portable ZIP into a writable folder, then open `SableLatch.exe`. Keep the accompanying files together. The self-contained package includes its Windows runtime; no separate .NET installation is needed.

The welcome screen has a brief lock animation, a primary Create your vault action, and a smaller Restore from backup option. Encrypted import is available in Settings after creating or opening a vault. New-vault setup has Back and Next controls. Create a password with at least 12 characters, reveal/copy the initially hidden recovery key when ready, confirm you saved it separately, then set an optional PIN. Startup with Windows is offered during setup and can be disabled later.

After the first full-password unlock, set a 4–12 digit quick PIN. The full password is required whenever the process starts. Timer, manual, Windows-lock, and sleep locks use the PIN while that process remains running. Five incorrect PIN attempts discard that quick session and require the full password. The centered locked screen uses typing only; clicking its background focuses the input. Input is disabled and cleared through verification and error feedback; concurrent requests cannot extend the five-attempt budget. Set, change, or disable the PIN in Settings → Security & locking. Changing it requires the full password.

### Updating an existing portable copy

Exit the old version from its tray menu. Extract the new ZIP to a separate writable folder. Copy your complete `SableLatch-data` folder into the new `SableLatch` folder, beside the new executable; keep the original folder as your rollback copy. Launch the new executable on the same Windows account/computer. If you use a custom data location, launch with `--data-dir` pointing to that existing folder instead. Never run both versions against the same vault. The encrypted vault format remains v1; no reset or recovery is needed for this same-computer upgrade. Older builds do not understand PIN settings and can discard them when saving, so do not alternate versions on the same data.

The vault lives in `SableLatch-data` beside the app. A custom location can be opened with `SableLatch.exe --data-dir "C:\path\to\vault"`. Keep the whole data folder, including `attachments`, when transferring it. On a different Windows installation, choose recovery and supply the master recovery key.

## Included

- Text, secret, and login entries; labels; multiline text and masked multiline secrets; nested folders; search across labels and ordinary text.
- Compact, rounded 342 × 411 popup with borderless rows, subtle edit icons, optional header search, and visual invalid-field feedback.
- A fixed compact window with the list filling the available space, smooth whole-row hover, dark context menus without an icon gutter, and active header fills that clear when switched off. Back follows the settings hierarchy and stays in a fixed position. The entry editor has a fixed Save button, a bottom list fade signals more rows, and the scrollbar is a slim rounded indicator.
- Centered locked screen with a box-free typed PIN. Four to twelve digits are supported. Only the last three remaining attempts are shown; five rejected attempts require the full password.
- Lightweight audit metadata: creation/modification timestamps on entries, 256 protected security-event records, and a static HTML report from Settings → Advanced → Activity & audit. No reporting server, background timer, or copied-value logging.
- Click to copy with animated confirmation. Entry types use buttons; switching types preserves unfinished drafts. Drag the entry row to show an insertion line and reorder. Drop it onto a folder chip to move it there, or onto the parent breadcrumb to move up. Alt+Up/Down reorders a focused entry.
- Click a login row once to copy its username and again to copy its password. The corresponding caption blinks. Username copy temporarily keeps the panel on top without activating another application. Password copy closes it unless pinned or close-after-copy is disabled.
- A configurable 60-minute unlock session by default. Pin suspends the timeout; unpin applies any expired timeout immediately. Windows lock, unlock, sleep/resume, sign-out, explicit Lock, and Exit lock the vault regardless of pin.
- Secret clipboard copies clear after 30 seconds by default if still owned by the app. Windows clipboard history/cloud exclusion flags are set. Other clipboard software can still read copied values.
- Hover over the edit icon for five seconds to reveal its trash action, or right-click → Delete. Either path first shows a red **DELETE** button; clicking that button confirms. Ordinary entries then delete permanently by default; secrets and logins enter encrypted Trash. Only trashed items expire after seven days, with configurable retention or no automatic expiry.
- Settings for sessions, clipboard, deletion, backups, startup, shortcut, themes, and built-in content modules.
- Password changes and recovery-key replacement after password verification. First-run backup restore uses the normal vault folder. Restoring while a vault already exists creates a separate copy and preserves it.
- Encrypted backups using both a backup password and the appropriate recovery key. Automatic backups are off by default, including in the setup checklist. Enable them explicitly in Settings; after enabling they run when unlocked, changed, and due. The initial cadence is 24 hours with seven archives retained.
- Three data-only monochrome theme presets; English UI. Typed item, attachment, editor, presenter, clipboard, layout, and toolbar interfaces are in place for extension development.

## Controls

Default shortcut: **Ctrl+Alt+Space**. Change it with the press-to-record control or restore its default in Appearance & startup. Drag the header to move the panel. Double-click the SableLatch name to return it to its default bottom-right position; the title remains ordinary text. Unpinned windows stay out of Alt+Tab; pinning makes them appear. Close hides it in the tray; Lock clears the active document and requires the PIN (or full password if no quick session exists). Escape cancels an armed deletion, returns from an editor, or hides the main panel. Enter activates the current Save, Next or Unlock action, including PIN setup, password confirmation, recovery and settings. Shift+Enter inserts a newline in multiline fields. Focused buttons, open dropdowns and shortcut recording keep their own keyboard behavior. Held-key repeats and submissions during busy operations are ignored. About is inside Settings.

## Encrypted import and export

Settings → Advanced → Import & export saves a `.sableexport` protected by its own export password. It includes entries (including Trash), folders, creation/edit dates, unknown content types and encrypted attachments. App passwords, PIN enrollment, backup credentials, host settings and audit history are excluded. Imports add independent copies without replacing existing entries or changing the destination password/PIN. Importing the same file twice creates duplicates.

On a new computer, create a vault with a fresh password/recovery key/PIN, then choose Settings → Advanced → Import & export and enter the export password. For `.sablebackup` archives, choose Restore from backup and supply both its backup password and original recovery key; then set a new local app password. Restored automatic backups start disabled so a previous computer's destination is not reused accidentally.

Exports use AES-256-GCM with a fresh nonce and random 128-bit salt, plus Argon2id (64 MiB, three passes). Unique salts defeat reusable rainbow tables; the memory-hard derivation slows password guessing. An offline file cannot enforce a retry limit, and a weak password can still be guessed. Choose a long, unique export passphrase. Archives are currently limited to 256 MB and individual attachments to 64 MB; large transfers need temporary memory while running, but no background process or cache remains.
## Settle in

The top of Settings offers **Finish setting up / Continue setup** until you review three essentials: automatic backups, encrypted Trash retention, and your global shortcut. Progress is saved inside the encrypted vault. The guide has no appearance or advanced tuning steps. Backups can be explicitly left off; a completed checklist records your preferences, not proof that a backup exists. A restored vault starts this checklist again for its new environment.

Settings saves stay at the bottom while fields scroll. Back with unsaved changes first shakes Save and shows “Save changes? Back again to discard.” Back again leaves without saving. Further editing resets the confirmation; returning fields to their original values does not prompt. Locking always takes priority and discards editor drafts.

Folders use real vector folder icons by default. In New/Edit folder, Customize expands a small icon picker on the same page. The choice is encrypted with the folder and travels with encrypted exports. “All items” is a plain label at the root, while folder breadcrumbs still navigate back.

The default and bundled theme backgrounds are approximately 95% opaque (previously 80%), while text stays fully opaque. No live background blur is enabled. Text has a subtle black contrast shadow. Scrollable pages show upper/lower fades only when content exists beyond that edge. The thin scrollbar thumb is proportional to its viewport and can be dragged normally. The welcome animation is brief, honors the Windows animation preference, and stops when its page closes. Recovery-key copy shows a muted green confirmation.
## Audit reports

New entries record their creation and last edit time in the encrypted vault. Old entries have no known creation timestamp; the report says “Not recorded” rather than guessing. Editing them records a modification time. Reports list labels, entry type, timestamps, and trash state, but never payloads, usernames, passwords, PINs, or recovery keys. An unlabelled entry is shown as “Unlabelled entry” rather than exposing its first line.

The separate `audit.sablelog` contains at most 256 fixed-code security events and is limited to 128 KB. It uses the same Windows-user/device protector, is read only during writes or explicit export, and has no resident history cache or timer. It is local to that Windows environment and is not part of recovery backups; a copied unreadable history file is preserved. This is operational history, not a tamper-proof forensic ledger.

Choose **Settings → Advanced → Activity & audit → Save HTML report**. Save over the previous report to refresh it, then open it in your browser. The exported HTML is a readable file containing labels and dates, so store it where you intend. It has no scripts, external assets, or network requests. Nothing is exported automatically.

## Recovery rules

Normal access requires the app password and the registered Windows environment. The 256-bit master recovery key can recover the vault by itself and set a new app password. A lost recovery key can be replaced while you can still authenticate on the registered computer. If both normal access and the recovery key are lost, no reset can decrypt the old vault; keep its encrypted files and start a new vault in a separate folder.

A replacement recovery key applies to the current vault. Older backups and older vault copies still require their original key. Backups additionally require their backup password; the recovery key alone cannot open the outer backup archive.

## Preview status and limits

This is a first working preview, **not an independently audited password manager**. The core test suite checks encryption failures, recovery, backups, attachment integrity, mutation safety, and session policies. Windows DPAPI is unavailable under the restricted build account, so real device binding must still be tested from a normal Windows session. The test-only device adapter and sample-data renderer are excluded from Release builds.

The target is x64 Windows 10/11. Only the available Windows host has been used for compilation and rendering; a Windows 10/11 compatibility matrix remains necessary before a public release. Microsoft's currently supported .NET Windows editions are listed in its [runtime support policy](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md). Compatibility and vendor support are different claims.

The app uses one process; memory use is not zero. Argon2 intentionally uses 64 MiB while deriving a password key. Large collections use a virtualized list, but metadata is currently decrypted as one document and rewritten atomically on changes. Attachment maximum: 64 MiB each. Vault metadata and complete backup archive maximum: 256 MiB. A paged/chunked storage implementation can replace this behind the storage contracts later.

Declarative ZIP installation, scoped operation prompts, signed official modules and beginner samples are shipped in 0.7.0; see the [module guide](../modules/README.md). Arbitrary executable plugins, screenshot capture UI, SSH keys, a rich-text composing editor, RDP connections, installer mode, automatic app updates, application code signing and additional translations are **not shipped**. The screenshot provider in `examples` demonstrates the storage/clipboard contract; it is not a capture plugin enabled in this app. English fallback/localization hooks exist, but full translation coverage still needs finishing before adding another language.

Window coordinates and the global shortcut are stored unencrypted in `window.json` and `host.json`; they contain no vault values. Startup registration contains only the executable and vault-folder paths. The rest of the preferences, labels, folder names, and entry payloads are encrypted. Clipboard history exclusion and capture exclusion are best-effort platform features, not defenses against malware or an administrator.

## Build and test

Install a .NET 10 SDK on Windows, then run:

```powershell
.\build.ps1 -Portable
```

Or build and run the projects directly:

```powershell
dotnet run --project tests/SableLatch.Tests
dotnet run --project src/SableLatch.Desktop
dotnet publish src/SableLatch.Desktop -c Release -r win-x64 --self-contained true -o artifacts/SableLatch-portable
```

Debug builds alone support `--preview --data-dir <empty-disposable-folder>` for rendering fixture screens and running view checks. Never use a real vault for that switch. Release builds contain no preview device adapter.

See [ARCHITECTURE.md](../ARCHITECTURE.md), [SECURITY.md](../SECURITY.md), and [EXTENSIONS.md](../EXTENSIONS.md). SableLatch is available at https://github.com/odxxt/SableLatch under the MIT license.

## Advanced entries (0.6.6)

Choose **+ → New entry**, then turn on the **Advanced** switch beside the page title. Keep the same Text, Secret or Login type. Entry colors are available with Advanced off. Turning Advanced on reveals a closed action-type dropdown, with its fields hidden until a type is selected. Choosing a type configures the separate play button beside Edit:

- **Application:** choose an existing .exe, with optional arguments.
- **Windows & Control Panel:** search both collections together, with distinct icons and source labels. The compact catalog contains 208 destinations and keywords, with only six common choices shown before searching. You can also enter an `ms-settings:` address or canonical name.
- **Run:** enter a Windows+R-style command, such as `notepad`, `control printers`, or `%APPDATA%`. Quote executable paths containing spaces when adding arguments. Commands use Windows shell resolution, without an implicit CMD wrapper.
- **PowerShell:** enter a script (Shift+Enter adds lines), or invoke an existing .ps1 file. Up to 12,000 characters; no profile or execution-policy override.
- **CMD:** enter one command line, chain commands, or call a .cmd file. Up to 7,000 characters; AutoRun is disabled.

Existing ordinary entries can gain these options from their right-click **Advanced options…** command. **Test action** sits to the left of **Save entry** while Advanced is enabled, using one third of the footer width while Save uses two thirds. Both keep the same height and text alignment. Save stays dimmed until the enabled action is tested; clicking dimmed Save only shakes Test action. Neither button changes its name. A failed launch leaves Save dimmed. Changing the action destination, script, arguments or working folder requires testing that changed configuration. A successful test means Windows accepted the launch; inspect its actual result yourself before saving. Copying the row never runs anything and keeps the existing login username/password sequence. Actions are never run on opening, importing, restoring or unlocking. The action target, script, color and arguments are part of the encrypted entry and travel with backups/exports. A transferred application path may need updating for the new PC.

Actions execute as your Windows account, outside a sandbox. Review imported actions before running them. Scripts and command-line arguments are readable by the launched process and may be visible to local process-inspection tools; do not embed credentials in commands. SableLatch never substitutes vault values or keys into actions. App locking does not terminate a program or script you already started. Existing audit storage records only that an action started, never its script or arguments.

## Simpler settings and tray behavior

Settings keeps Security & locking, Clipboard & copying, App preferences and Plugins & modules together. About stays at the bottom with a circled information icon; Quit sits to its right. Click Quit, then Confirm quit to close the app; leaving Settings or hiding the panel cancels the confirmation. **Advanced** holds Backups & restore, Import & export, Trash & retention and Activity & audit. Entry layouts appear only under Plugins & modules when another layout is registered; App preferences has no layout selector. Saves stay below scrolling fields.

In **App preferences → Taskbar & Alt+Tab**, choose Only when pinned (default), Always or Never. The preference is encrypted and is applied after unlocking; it stays cached through locks during that app session. Before the first unlock, the default tray-only unpinned behavior applies.

The window stays at 342 × 411 logical pixels across folders, settings and forms, matching the previous compact folder view. Windows DPI scaling may round this to physical pixels. The list footer stays at the bottom. Scroll cues have a broad soft falloff vertically and fade out along both sides; top/bottom cues disappear at their respective ends.

Actions require a first-run review on each computer unless you created and tested them locally. The successful review is remembered for that exact entry and command on this Windows account/computer. Changed commands, arguments or starting folders ask again. Imported entries receive new IDs and must be reviewed; restored or copied approvals cannot carry trust to another PC. **Verify each time** remains independent: enable it to review every run. Imports preserve that choice. Existing entries with it already enabled keep it enabled. Copying an entry never runs its action.

**Choose application…** lists desktop applications discovered on demand from registered Windows App Paths and Start-menu shortcuts. The highlighted picker focuses its fixed search field at the top, with small application icons beside results below. Filter the list by name; **Browse…** selects an .exe that is not listed. This is not a complete inventory of every Microsoft Store or portable application. The picker does not scan the entire disk or keep a resident app catalog. Changing action type clears the previous target, arguments and working folder. Missing action inputs are focused, scrolled into view and animated individually.
**Starting folder · optional** controls where relative paths resolve. For example, a script that reads `report.txt` can start in `C:\Reports` to find that file there. Most actions can leave it blank, which starts in your Windows user folder. Existing configured folders are preserved. Expand this option only when needed.

Missing or invalid fields shake red. After three failed attempts on that field, a short explanation appears beneath it; editing hides the explanation. PIN unlocking retains its separate five-attempt limit and countdown. Scroll glows are centered beyond the top/bottom edge so only their inner halves show; covered pages do not shine through a picker.
Selecting Application or Windows & Control Panel opens the relevant focused picker immediately. Cancel returns to the editor. The mouse wheel scrolls forms or dropdown lists without changing the selected value. The Advanced switch stays beside New/Edit entry, separate from Text / Secret / Login.

Settings search uses a small built-in catalog, not an indexing service. Search matches names, categories, destination addresses and keywords such as microphone privacy, wifi or credential. Windows build, hardware, policy and installed software determine which destinations actually exist; some older Control Panel destinations redirect to Settings. Third-party panels and pages requiring a specific device/app ID are not exhaustively catalogued. References: https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings and https://learn.microsoft.com/en-us/windows/win32/shell/controlpanel-canonical-names.

First-run approvals live in a separate device-protected actions.sableconsent file, capped at 2,048 exact entry/action digests (under 300 KB). No command text is stored there, and vault backups/exports omit it. A missing, corrupt, foreign or evicted approval asks again. Local test execution of an existing unapproved entry also requests review; a locally authored new entry's explicit successful test counts as its approval. Programs and scripts still run under your Windows permissions, without a sandbox.
Secret and Login value editors have an eye inside the right edge of the value box. When revealed, the same eye gains a diagonal slash. Reveal is temporary; hiding clears the plaintext editor and switching/locking clears both controls. Advanced is now the sole action enable switch, with its active track using the theme accent. Entry colors work independently. Turning Advanced off disables a saved action while keeping its configuration for later.

For Text, Secret and Login actions, use the segmented **Run / Run & copy** selector. Run launches only; Run & copy copies the entry value after a successful launch (username first for Login entries; the panel stays open until the next row click copies the password), never the row label. The same action is available by right-clicking the entry. The action icon receives a small copy badge without changing size or position. Secret copies use the normal clipboard-history exclusion, clearing timer and close-after-copy preferences. Cancelled reviews and failed launches do not copy. Test action tests execution only and does not copy the edited value. Changing copy mode requires testing and renewed local approval. Commands receive no automatic secret interpolation.
## Multiline secrets, terminals and capture protection (0.6.6)

Secret and Login password fields accept multiline paste and Shift+Enter while masked. The eye reveals the same complete value; hiding clears the visible plaintext field. Masked text contains only bullets and line breaks, exposes no automation value/text pattern, and has no copy/cut/undo buffer. Enter still saves the current form. Entry colors use smaller swatches, tooltips use the dark theme, and neighboring footer buttons share their height, padding and text alignment.

New PowerShell and CMD actions enable **Keep terminal open when finished** by default, so you can read output after a command such as ping finishes. Turn it off to close the terminal when the command completes. Existing saved actions retain their choice. It uses PowerShell -NoExit or CMD /k, and requires testing the changed action before saving. A script that explicitly exits can still close its terminal. Selecting a new CMD or PowerShell action defaults it to on; other action types hide and clear it. Existing launch approvals are preserved with this option off; enabling it requires new consent. No extra process runs until you test or invoke an action.

Windows capture exclusion is applied to the panel and its dropdowns, menus and tooltips. On Windows 10 version 2004 and later, supported capture paths omit these windows; older Windows versions use the blank-content fallback. Security settings report detected protection failures. Teams/Zoom behavior depends on the capture path they use and was not directly tested. Native dialogs, launched programs, alternate capture APIs, elevated software and cameras are outside this protection. It is not possible to promise invisibility to every recorder. See SECURITY.md.
## Install the PuTTY module (0.7.0)

Open **Settings → Plugins & modules → Catalog → PuTTY SSH → Install**. This included package works offline. Open the installed module's settings to review the detected PuTTY application or locate it using Browse. If PuTTY is absent, use Download PuTTY to visit the official download page.

Use **+ → New entry → More → PuTTY SSH**. Save a name, host, username and password. Advanced contains port, expected server fingerprint, network preference, compression, keepalive and terminal font/size. Clicking the row opens details; Run launches PuTTY after the first connection review. Don't ask again remembers that connection on this Windows environment. Changed hosts, binaries or module manifests ask again.

Use Install ZIP for community modules. Removal preserves encrypted entries; reinstall the compatible module to use them again. See the module guide for samples and the supported first-version limits.

## Compact navigation and scrolling (0.7.1)

The **+** beside the folder heading opens a small **New entry / New folder** menu. There is no separate bottom Add entry button, so entries use the freed space while the window stays the same size. The entry type selector uses matching segments and a clear active fill. **More** opens module types, with the chosen module named below the selector. Changing type preserves the name and each type's unfinished field values until you leave the editor or lock.

Installed modules show **✓** in Plugins and the catalog; opening the same package shows **Already installed** and a link to its settings. The online catalog checks the installed package against the signed catalog's archive hash. A different package still goes through verification and update review.

Mouse-wheel scrolling eases over 160 milliseconds, including dropdowns, without changing their selection. Nested multiline fields scroll first; at their edge, scrolling continues on the page. It honors Windows' wheel line count and disabled animation preference, cancels on leaving the page, and does no animation work while idle. Scrollbar dragging and keyboard scrolling remain direct.
## Formatted Text entries (0.7.2)

Copy formatted text from an app that supplies HTML on the clipboard. Paste into an empty **Text** value box, or select the entire value before pasting. SableLatch stores a sanitized HTML representation alongside the plain text, inside the encrypted vault. Copying the entry offers both formats; Word, email editors and other supporting destinations can retain bold, italic, underline, lists, links, common inline styles and tables. Plain-text destinations receive plain text.

The compact editor still displays plain text and shows **Formatting preserved**. This is preservation, not a rich-text composition editor. Typing or partially replacing the value clears its previous formatting to prevent outdated content being copied. Re-paste the formatted source to restore it. Sources offering only RTF or plain text do not preserve rich formatting in this version.

Scripts, event attributes, forms, embedded objects and external images are removed. External resources are never fetched. CSS classes, arbitrary styles and images are not preserved, so complex email layouts are not pixel-perfect. HTML is capped at 100,000 characters, 5,000 DOM nodes and 64 levels. Secret/Login values remain plain text. The destination app ultimately decides which clipboard representation and styling to accept.