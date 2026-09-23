# Verification — SableLatch 0.7.1 preview

## Current release

- Core/storage/module suite: **194 checks passed in 0.7.0; core code is unchanged in 0.7.1**. New coverage includes bounded ZIP loading, unsafe paths, duplicate JSON/ZIP entries, unsupported code/actions, signature tampering, secret field protection, schema-safe updates, device-local approvals and bounded UTF-8 secret transfer.
- WPF: **264 interaction checks passed**, plus seven existing view checks. Signed module installation, PuTTY entry editing and validation, first-run consent, no copy/launch on opening details, enable/disable, uninstall preservation, reinstall and More types are exercised using disposable data. The 0.7.1 run also checks entry/folder creation, selected types, shared labels, installed-module state, terminal defaults, username-first Run & copy, the SSH vector icon, and smooth/nested scrolling.
- Windows module/pipe suite: **10 checks passed, one environment-blocked check in 0.7.0; the broker and native test code are unchanged in 0.7.1**. Exact client identity, wrong-client rejection, cancellation, buffer clearing, actual publisher/catalog signatures and tampered executable rejection were exercised. The installed official PuTTY **0.85** successfully consumed the actual named pipe through `-pwfile`; no temporary password file was used.
- **Full connection coordinator remains unverified in this restricted session:** creating the temporary non-secret PuTTY registry session was denied by Windows. The failure path clears the transfer buffer. Run `dotnet run --project tests/SableLatch.ModuleTests` in a normal Windows development session to exercise that path using fictional credentials and loopback only. No successful login to a real SSH server has been claimed or tested.
- Debug and self-contained Windows x64 Release compile with zero warnings/errors. Portable archive inventory/hash checks and a fresh extracted-runtime startup check are recorded in the accompanying release verification file.
- No existing user vault was opened or modified. No user passwords, SSH hosts or saved actions were used during verification. The online catalog needs these files published to GitHub; bundled module installation is available offline.

Module verification outputs are included in `module-test-results.txt`; core and UI results are in `core-test-results.txt` and `view-test-results.txt`. The security and Windows-platform limitations below still apply. This preview has not received an independent security audit.

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