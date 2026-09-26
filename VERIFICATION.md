# Verification — SableLatch 0.8.3 preview

- Debug and self-contained Windows x64 Release builds passed with no warnings or errors. The portable output includes the .NET and Windows Desktop runtime; no separate runtime installation is required.
- **222 core checks passed.** Display regressions verify full-screen startup, optional windowed startup, absence of fixed dimensions/forced scaling, legacy-entry compatibility and serialized display preference. Existing credential and certificate-policy checks pass.
- **295 WPF interaction checks and 7 view checks passed** using fictional data and an isolated preview clipboard. The new checks verify that the editor has no dimension fields, starts with full screen selected, and preserves a windowed preference after saving/reopening. Launch execution is simulated. An initial fixture run used the wrong checkbox content selector; the fixture was corrected and the complete run passed.
- User feedback confirms 0.8.2 connected successfully to their server. The new live-session fullscreen/window transitions have not been exercised against a remote server here. Native mstsc's dynamic-resolution limitation remains; removing the app's fixed dimensions does not add a new client capability.
- Password handoff, file cleanup, certificate warning/choice and module signatures are unchanged by this release. Windows module lifecycle tests were not rerun for this display-only change; their prior results and environment limitations are below.
- No real vault, clipboard, saved credentials or remote server was used for these checks.

# Previous verification — SableLatch 0.8.2 preview

- All 219 core checks passed. The RDP regression check verifies authentication level 2 (warning/choice), CredSSP enabled, no authentication-level-0 silent acceptance, and unchanged clipboard defaults.
- Self-contained Windows x64 Release publish passed. This is a targeted setting/consent-text update; the prior UI and cleanup results below are from 0.8.1, not a new full run.
- The user supplied screenshots confirming that mstsc reads the launch file and reaches server certificate verification. Version 0.8.2 changes strict certificate refusal to warning mode; successful login and the resulting live Windows dialog have not been tested here. Windows/organizational policy may still prevent proceeding. The unsigned-file publisher warning is unchanged.

# Previous verification — SableLatch 0.8.1 preview

- Debug and self-contained Windows x64 Release builds passed. A portable tray smoke check loaded coreclr, hostfxr and hostpolicy from the app folder with DOTNET_ROOT deliberately pointing to an absent runtime. Only an empty session-lock file was created in the disposable data directory.
- **219 core checks passed**, including RDP profile validation, safe public-file formatting, host-owned sensitive password copying and launch preference serialization.
- **294 WPF interaction checks passed** with fictional vault data and an isolated preview clipboard. RDP checks exercise row copy without launch, separate Launch / Launch & copy review, cancellation, saved action mode, row icon/tooltip state and module uninstall/reinstall. Launch execution is simulated for these UI checks.
- **31 Windows module checks passed; 2 environment checks blocked.** RDP file-lifecycle checks use a deterministic test protector and simulated process startup; they verify ACLs, sharing, secret buffer clearing, cleanup on success/failure/cancellation/normal exit, and scoped crash-leftover cleanup. They do not establish actual remote login.
- **Blocked:** real CurrentUser DPAPI protection is unavailable under this restricted execution account. The actual protection attempt failed closed, with no launch file. A normal Windows-user test is required. The pre-existing registry-backed PuTTY launch check is also blocked; PuTTY's actual pipe reader still passes.
- **Not tested:** successful mstsc password-file authentication, real startup/file-read timing, certificate rejection against a live target, organizational policy, Windows 10, gateways, expired-password flows, or an RDP server. No live server/account was supplied. Windows may still show sign-in prompts. Do not confuse a started process with a successful login.
- No existing user vault, real clipboard, saved Windows credentials or live remote credentials were used. The replacement uses temporary DPAPI-encrypted files and has a different same-user exposure boundary from 0.8.0. See [RDP design](docs/RDP-DESIGN.md).

# Historical verification — SableLatch 0.8.0 preview

## Previous release

- **212 core/storage/module checks passed.** New RDP coverage validates destination/account/port/size constraints, consent binding, sensitive classification and protection of host-owned fields.
- **288 WPF interaction checks passed, plus 7 view checks.** Added offline RDP install/catalog state, masked entry editing, fixed Save footer, validation, click-to-connect review, cancelled review without launch, uninstall preservation and reinstall. Screenshots use fictional data.
- **32 Windows module/pipe checks passed; one PuTTY registry check remains environment-blocked.** Tests exercise complete framed transfer, exact child PID, wrong-client rejection with zero bytes delivered, cancellation clearing, malformed frame limits, signed RDP and PuTTY packages, and the two-module catalog.
- Tests instantiated the **actual Windows Microsoft RDP ActiveX control**, checked server authentication/CredSSP and redirection settings, read back disabled credential saving/prompts and dynamic devices, passed a fictional Unicode password through its write-only API, and attached native event callbacks.
- A real SableLatch helper was launched with a random pipe and parent PID. It received a fictional connection, requested RDP to **loopback port 1 only**, acknowledged the handoff, displayed its session/error window, and exited cleanly when that window was closed. This verifies startup and local handoff, **not successful remote authentication**.
- **Not tested:** successful RDP login, live server certificate rejection, wrong/expired-password policy behavior, disconnect events after successful login, remote clipboard transfer, full-screen rendering of a live session, and Windows 10 compatibility on a separate machine. No disposable RDP server or credentials were provided. Native properties were read back, but property checks do not substitute for those end-to-end cases.
- The existing PuTTY registry coordinator test is denied in this environment. Its failure path clears the transfer buffer; the real signed PuTTY 0.85 password-pipe reader test still passes.
- Debug and self-contained Windows x64 Release builds pass. Portable archive/runtime checks are recorded in the accompanying release verification file.
- The previous live GitHub PuTTY catalog was verified on 24 September 2026. The updated signed catalog includes RDP locally; publication requires pushing this source version. Bundled installation works offline.
- No existing user vault, clipboard, saved action or remote credentials were used. The vault still holds decrypted JSON while unlocked. This preview has not received an independent security audit.

The package includes core, UI and module test result files. Earlier storage, capture-exclusion, memory and platform limitations below still apply.

## Previous release verification record

### SableLatch 0.6.6

## Results

- Debug and self-contained Windows x64 Release builds passed with zero warnings/errors.
- **151 core checks passed in 0.6.5; core code is unchanged in 0.6.6.** Coverage includes real Argon2id/AES-GCM/RSA operations, incorrect credentials, vault/backup/export tampering, recovery, device-protector separation, unknown provider schemas, encrypted attachments, additive import safety, retention, PIN limits and bounded protected audit records. New checks confirm folder icon transfer and preservation of the destination setup-checklist state during import.
- **233 WPF interaction checks passed**, plus the existing view checks. These run the actual setup, copying, deletion, ordering, search, PIN settings and rapid five-attempt exhaustion handlers using fictional data.
- New UI checks verify welcome Create/Restore actions with import in Settings, muted green key-copy text, plain All items label, text contrast effect, default-position reset, inline folder customization and persistence, and a settings Save button outside the scrolling form.
- Unsaved-change checks verify first Back stays and shakes Save, the hint does not move the save footer, further edits reset confirmation, second Back discards without changing the stored setting, and reverting to original values avoids a false warning.
- Scroll checks verify top/middle/bottom edge states, a vertical thumb positioned at the start/end of the real track, and actual content movement from a thumb drag. Unloaded overlays are removed so they do not retain previous pages.
- Setup-guide checks verify an explicit backup preference, saved Trash retention, shortcut completion and removal of the completed progress card. The guide records reviewed preferences; it does not claim a backup has already been written.
- Fifty-five fixture screens rendered. Welcome, folders, key-copy feedback, list, settings, unsaved prompt, top/middle/bottom scroll states, setup guide, editors and locked screens were visually reviewed.
- HTML audit checks still verify escaped labels and exclusion of secret values. Browser rendering remains unverified; the earlier local-file browser restriction was not bypassed.
- Debug-only ephemeral device and clipboard adapters were used for visual tests. Release excludes these adapters. No existing user vault was opened or modified.

## 0.5.1 keyboard and opacity checks

The UI harness sends routed Enter events through the real window handler for setup Next/Finish, PIN changes, valid and failed PIN unlocks, multiline entry Save and settings Save. Validation and the five-attempt budget still hold. It also checks that navigating back to the list clears the previous form action. All bundled themes and the default surface use alpha 242 (approximately 95%). This update changes no core vault cryptography; the existing 116 core checks cover that unchanged code. Native held-key and modifier behavior still benefits from normal keyboard testing.

## Resource scope

The previous 0.5.0 warm, unopened Release tray sample measured **93.9 MiB working set / 49.2 MiB private memory** on this host. This is a whole-app WPF sample, not a zero-memory claim or a guarantee for every machine. The idle fixture created only a zero-byte session lease. No audit file, vault or HTML report appeared while unopened. The owned smoke process was stopped afterward.

The welcome animation is a short one-shot WPF animation, respects Windows animation preferences and is removed on page unload. Scroll cues respond to scroll/load/unload events, with no polling timer. Setup progress is a few step IDs in encrypted preferences. There is no added service, network listener or permanent transfer/report cache. Large transfers and Argon2 intentionally require temporary memory; Argon2 uses 64 MiB during derivation. The audit journal remains capped at 256 records / 128 KB.

## Verification boundaries

Real DPAPI is unavailable under this restricted build account. Normal Windows-account testing is still needed for actual device binding and second-computer recovery, native session/sleep notifications, clipboard ownership/history flags, physical pointer input and double-click behavior, shell Alt+Tab display, global shortcut collisions, startup registration, multiple monitors/DPI and Windows 10/11 compatibility. Native style bits and the position-reset operation are tested in the fixture. These tests are not an independent security audit.

Offline encrypted files cannot enforce PIN-style attempt limits. Strong unique passwords remain necessary. Export passwords cannot be recovered using the vault recovery key. PIN tickets are process-only convenience, not Windows Hello or hardware-backed authentication. .NET strings and OS copies cannot be guaranteed erased. Security locking always clears drafts instead of waiting for a save/discard response.

## Packaging and upgrade

This release uses a fresh portable folder and ZIP. Exit the old app, extract the new ZIP separately, then copy the entire SableLatch-data folder beside the new executable, or continue using the existing custom --data-dir. Keep the original folder as a rollback copy. The vault format remains v1; do not alternate older builds on the same data because they may discard newer metadata fields.

Installer mode, executable ZIP plugins, additional languages, code signing, automatic updates and a public plugin ABI remain deferred.
## 0.6.0 results

126 core checks pass, including encrypted script storage, advanced login content preservation, color/action export and import, encrypted Trash retention, input bounds and legacy defaults. 137 WPF interaction checks pass, including advanced editor draft switching, fixed Save, separate copy/action buttons, explicit review cancellation, settings hierarchy, all taskbar modes and folder footer placement. (A multiline invalid-command test occupies two lines in the text report; count PASS records.)

Benign, fixed test scripts actually ran through the prepared PowerShell, CMD and application launch paths, with hidden process windows and captured output. They verify multiline PowerShell and quoted command metacharacters. No user-configured commands or programs ran. Windows Settings/Control Panel destinations were checked as launch plans without opening those system interfaces. Native taskbar style and WPF registration states pass; visual shell taskbar behavior still merits normal Windows 10/11 user-account testing.

The updated folder, advanced row, action review, settings hubs, app preferences and scroll glow screens were rendered and reviewed. The fixture uses fictional data only. There are no added permanent services or processes; actions start external processes only when requested. Debug-only fixture process tests are excluded from Release.

## 0.6.1 verification

129 core checks pass, including encrypted local confirmation preference, forced verification on import and the direct-launch default. 153 WPF interaction checks pass. New coverage includes Advanced placement, missing-path animation rather than label animation, application selection/Browse, clearing fields on action-kind changes, test-before-save and retest invalidation, direct/verified invocation, fixed window height/position, plugin navigation and layout visibility. Fixed size is checked with DPI-rounding tolerance; the window retained identical native height and position across folder/settings/editor transitions.

Real on-demand application discovery found 77 existing executable paths on the fixture host without launching them. Editor test/direct-launch routing uses a Debug-only recording launcher. Separate benign fixed commands still execute through the real PowerShell, CMD and application handlers, checking multiline and metacharacter quoting. Windows system settings launches remain checked as plans only. No user vault or user-configured command was opened/executed.

Reviewed rendered settings root, fixed folder list, action editor footer, and scroll fades. All retained recovery/setup/lock/PIN, copy, reorder, save and scroll-lifecycle checks also passed at the smaller panel size. The in-process fixture reports PASS records; a multiline invalid-command case occupies an extra physical line in the report.
## 0.6.2 verification

170 WPF interaction checks and 7 view checks pass. New cases cover dimmed Save without execution or renaming, Test enabling Save, launch failure leaving Save dimmed, configuration-change invalidation, third-failure inline explanations, focused app search with icons, hidden underlying scroll cues, cancelling the picker, selecting a friendly Control Panel destination, fixed Run labels, Windows shell command parsing, quoted paths and environment-variable expansion. All retained lock/PIN, copy, editor, navigation and scroll checks pass. The unchanged core suite retains its previous 129 passing results; it was not rerun for this desktop-only update.

Debug and self-contained Release publishing succeeded. The Release tray process started responsively against a new disposable folder; no user vault was opened. Fixture application discovery still returned 77 existing executable paths. Picker, editor and scroll screenshots were visually inspected. Run and system-settings destinations are checked as launch plans, not an exhaustive interactive test of Windows Run or every Control Panel item. Test marks a successful launch request, not successful script completion.

The shipped ZIP excludes vault data and Debug-only preview adapters. Application icons are cached only while their picker is open; scroll cues remain event-driven with no extra timer or service.
## 0.6.3 verification

142 core checks passed, adding persistent same-device approval, foreign-device rejection, imported-ID isolation, command/argument/folder invalidation, independent repeat verification, tampering and size rejection, protected storage and recovery after a damaged approval file. Existing encryption, backups, transfers and PIN tests pass; real Windows DPAPI is still outside this fixture's available permissions.

191 WPF interaction checks and 7 view checks passed. New cases exercise the header switch, immediate application/settings picker opening, six initial suggestions, combined search with source labels/icons, uncommon settings/keyword search, all 208 unique launch-plan destinations, open/closed dropdown wheel handling, first-run review, cancellation, failed launch, repeat direct launch, changed-command re-review and the existing-entry test overlay. Queued clicks on cancelled run/test review controls cannot execute anything. Fixture launches use the recording adapter, except the existing fixed benign process tests.

46 fictional-data screens were rendered. The header switch, combined search and first-run review were visually inspected. A fresh self-contained Release tray process started responsively and was stopped after inspection. No user vault was opened or modified. Windows version/hardware-specific destination availability is not established by launch-plan checks; the catalog cannot enumerate every third-party panel or device-specific settings page. Search itself has no resident process or background scan.
## 0.6.4 verification

146 core checks and 210 WPF interaction checks passed, plus 7 view checks. New coverage verifies encrypted Run and copy persistence/import, safe defaults for older entries, renewed approval when copy mode changes, the eye's reveal/hide data transfer and hidden-control clearing, ordinary color persistence, Advanced dropdown-first behavior, theme accent, fixed copy-badge dimensions, copying actual secrets/passwords through sensitive clipboard handling, execution-only testing, failed/cancelled launch behavior, changing back to Run, and Quit footer placement/busy handling. Existing PIN, recovery, transfers, copy, window and navigation checks remain passing.

49 fictional-data screens were rendered. Secret eye placement, Advanced accent, the small action copy badge and Quit beside About were visually inspected. Debug and self-contained Windows x64 Release builds passed. Packaging compares the complete archive inventory and every entry SHA-256 with its source. The extracted ZIP is tested with development runtime discovery disabled and its loaded hostfxr/hostpolicy/coreclr paths checked against the extracted application directory. User vaults are never used for these checks. Native clipboard behavior still needs normal Windows-user testing; the fixture observes the sensitive copy flag through its test adapter.
## 0.6.5 verification

151 core checks, 230 WPF interaction checks and 7 retained view checks pass. New checks cover backup opt-in defaults, masked multiline insertion/paste, mixed newline preservation, absent secret automation patterns, no masked copy/undo, reveal/hide clearing, Text Run & copy from its context menu, keep-terminal metadata round trips and approval invalidation, matching footer geometry within one physical pixel of layout rounding, dark tooltips, and native display-affinity readback for main panel/context/dropdown/tooltip HWNDs. No capture-protection failures were reported for the fixture surfaces.

53 fictional-data screens rendered. The final action selector, keep-terminal setting, aligned footer, compact colors, transparent badge over a colored row, masked multiline editor, context menu separator and dark tooltip were visually reviewed. No user vault or configured user action was opened. Core encryption algorithms and vault format are unchanged.

CMD /k was tested with a fixed benign command: it produced output, stayed alive, and exited when instructed by the fixture. PowerShell -NoExit is verified as a launch plan, using Microsoft's documented switch. A redirected-stdin fixture does not reproduce its interactive console behavior, so the suite does not claim end-to-end PowerShell console retention. Ordinary PowerShell/CMD script execution and quoting still pass the real fixed-command checks. Scripts that explicitly exit can close a terminal despite keep-open.

Native display-affinity readback verifies Windows accepted the requested exclusion. It is not an actual Teams, Zoom or every-screenshot-tool test; those are unverified. Earlier Windows versions use a blank-content fallback and were not exercised on this host. Capture exclusion cannot protect cameras, all recorder APIs, privileged software, external apps or native file dialogs.

Debug and self-contained Release builds passed with zero warnings/errors. Release excludes the debug preview adapters and process fixtures. The portable ZIP inventory and every file hash are checked against the publish folder, then a freshly extracted app is launched with development runtime discovery disabled. See the adjacent release verification record for the extracted runtime check. The entire original user vault remains untouched.
## 0.6.6 verification

233 WPF interaction checks and 7 retained view checks pass. Quit is exercised through the actual Settings button: the first click keeps the vault open and changes its label; a busy operation blocks confirmation; leaving Settings resets it; the confirmed second click closes the window and releases the unlocked vault. The existing footer geometry check now verifies a 1:2 width ratio within pixel rounding, with unchanged height and vertical alignment. Existing secret reveal/hide checks still pass.

55 fictional-data screens rendered. Revealed eye/slash, Confirm quit and the 1:2 action footer were visually reviewed. No user vault was opened or modified. Core code is unchanged, so its previous 151 passing checks were retained rather than rerun. Debug and self-contained Release builds passed with zero warnings/errors. Every ZIP entry is hash-verified against its source; the extracted portable build is checked with external runtime discovery disabled in the adjacent RELEASE-VERIFICATION-v0.6.6.txt record.