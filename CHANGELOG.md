# 0.7.2 — Direct dragging and formatted text

- Align the top + menu to the right edge of its button, opening inward into the panel.
- Remove arrange mode and its toolbar button. Drag entry rows directly to reorder or drop into folder chips; drop on the parent breadcrumb to move up. Show brief folder confirmation and ease row movement. Alt+Up/Down moves a focused entry without a mouse.
- Preserve safe HTML clipboard formatting in built-in Text entries, alongside plain text. Common inline formatting, lists and tables survive copying to a supporting destination. Strip active content, external images and unsupported styles; bound size and complexity. Editing the plain text removes stale formatting.
- Document the agreed RDP helper design (not implemented in this release) and publishing/catalog procedure. Verify the live signed PuTTY catalog and ZIP hash.

# 0.7.1 — Compact navigation and smoother editing

- Smooth wheel scrolling across pages and nested editors, respecting Windows animation preferences and wheel settings; no idle timer.
- Replace the bottom Add entry button with a compact entry/folder menu on the top + button. Keep the window fixed and use the freed list space.
- Unify the type selector and keep the entry name when changing between text, secret, login and module types.
- Show installed module checkmarks and Already installed in package review and the catalog.
- Start Login Run & copy with the username, keep the panel open, then copy the password on the next row click before hiding an unpinned panel.
- Default newly selected CMD/PowerShell actions to keep their terminal open, with the option to close after completion. Preserve existing saved choices.
- Use a crisp terminal icon for PuTTY SSH entries.

# 0.7.0 — ZIP modules and protected PuTTY connections

- Install data-only module ZIPs, review community/official status, enable/disable or uninstall while preserving encrypted entries.
- Add More types, host-rendered custom fields and actions, module settings, signed official packages and an on-demand signed GitHub catalog. The included PuTTY module installs offline.
- Add PuTTY SSH entries with encrypted passwords, details and explicit Run, local first-run consent, executable signature/hash verification, and single-use local named-pipe delivery tied to the launched process.
- Advanced SSH options include port, expected host fingerprint, IP family, compression, keepalive and terminal font/size. Password-only initially; private keys follow later.
- Reject executable ZIP payloads, unsafe archive names, unknown APIs and changes that downgrade secret field schemas. No background module tasks or direct module network access.
- Provide a ready-to-install quick-note sample, beginner/AI author guide, packaging tool and official-signing helper.
- Clear mutable password transfer buffers on success, cancellation and failure; record module lifecycle events without credentials or connection details.

# 0.6.6 — quit confirmation and editor refinements

- Settings Quit now requires a second Confirm quit click; hiding the panel or leaving Settings cancels it. The button keeps a fixed width.
- Revealed values keep the same eye icon with a diagonal slash, without changing the reveal/hide behavior.
- Advanced editor footer uses a 1:2 Test action / Save entry split with the same height and alignment. Save remains full-width with Advanced off.
# 0.6.5 — consistent controls and capture protection

- Kept backup setup unchecked by default; existing saved preferences are preserved.
- Advanced reveals a closed dropdown and hides settings until an action type is selected.
- Added masked multiline secret editing, paste, eye reveal, and protected automation behavior.
- Extended Run & copy to Text, with a segmented selector and matching right-click actions.
- Aligned form/footer buttons, shrank color swatches, themed tooltips and removed the copy badge background.
- Added Keep terminal open for CMD and PowerShell, with consent and test invalidation.
- Protected native main-panel, menu, dropdown and tooltip windows from supported capture APIs, with failure status in Security settings.
- Portable archives include the runtime and verify every packaged file against its source hash.
# 0.6.4 — simpler secret and action controls

- Replaced Reveal while editing with an eye inside the value box.
- Made colors independent of Advanced; removed the redundant Show an action button checkbox.
- Advanced opens the action-type dropdown before application selection and uses the active theme accent.
- Added Run / Run & copy modes for Secret and Login actions, with a compact copy badge, sensitive clipboard handling and updated first-run approval identity.
- Added Quit to the main Settings footer beside About.
- Packaging verifies every ZIP entry against its source hash; runtime verification runs the extracted portable ZIP.
# 0.6.3 — unified settings search and local action approvals

- Combined Windows Settings and Control Panel into one 208-destination catalog, with keyword search, distinct icons/source labels and six default suggestions.
- Opened the destination picker immediately when selecting Application or Windows & Control Panel; prevented mouse-wheel dropdown selection changes.
- Moved Advanced to a compact on/off switch beside New/Edit entry.
- Added device-protected, bounded local first-run approvals keyed to exact entry and action content. Imported IDs, changed commands and new computers require review, including existing-entry tests.
- Separated one-time local review from Verify each time; imports preserve the repeat-review preference. Cancelled and failed launches never grant consent.
- Kept approval digests out of backups and exports; no resident catalog service or plaintext command history.
# 0.6.2 — action editor refinement

- Kept Test action and Save entry separate, with a dimmed, guarded Save until testing succeeds; failed tests cannot enable saving.
- Fixed row action tooltips to Run and removed the tooltip editor.
- Added a highlighted application picker with focused fixed search, executable icons and a separate Browse fallback; hidden editor scroll cues no longer leak through it.
- Added friendly Windows Settings and Control Panel pickers, plus Windows+R-style Run actions.
- Tucked starting folder into an optional control; blank starts in the Windows user folder.
- Expanded scroll fades and centered them at the viewport edges, with soft side falloff.
- Added per-field explanations after three failures, preserving PIN attempt protection and targeting the visible secret editor when revealed.
# 0.6.1 — fixed panel and action workflow

- Fixed every page at the compact folder size and softened scroll cues vertically and at both sides.
- Moved Advanced into the entry-type tabs, Plugins into main Settings, and About into its footer. Layout choices now belong only to installed plugin layouts.
- Added an on-demand desktop application picker with search and Browse fallback.
- Added precise missing-field feedback and cleared stale targets/arguments on action-type changes.
- Added Test action before saving new/changed actions, with Run action beside Save.
- Made per-launch verification optional. Imported actions enable it; local actions default to direct launch.
- Passed 129 core and 153 WPF interaction checks.
# 0.6.0 — advanced entries and simpler settings

- Added an Advanced entry button, muted row colors, and independent application/Windows settings/PowerShell/CMD actions alongside the existing content types.
- Encrypted action metadata follows normal save, Trash, backup and export flows. Copying never invokes the action; every launch has an explicit review.
- Fixed WPF taskbar registration to follow pinning, with Always and Never options in App preferences.
- Split Settings into everyday controls and an Advanced hub for less frequent vault tools.
- Attached list overflow overlays to a local layer, added contrasting edge glows, and removed unused folder-window space below Add entry.
- Added encryption round-trip, action preparation/launch, taskbar state, navigation and layout checks.
# 0.5.1 — readability and Enter support

- Increased default and bundled theme background opacity from 80% to approximately 95%; no live blur rendering added.
- Enter invokes Save/Next/Unlock in setup, PIN configuration, recovery, entry editing, folders, settings and transfer forms through one shared page-action route.
- Preserved Shift+Enter multiline input, focused-button behavior, dropdown selection and shortcut capture; ignored key repeats and busy submissions.
- Verified keyboard submission through the actual setup/PIN/entry/settings handlers, including all five failed PIN attempts.
# 0.5.0 — welcome, scroll and settings polish

- Animated minimal welcome with Create and Restore; encrypted import stays in Settings.
- Subtle text shadows and muted green recovery-key copy feedback.
- Real folder icons with an inline Customize picker; the root All items label is no longer a button.
- Scroll fades above/below all overflowing pages, correct proportional thin-thumb dragging, and unloaded-overlay cleanup.
- Resumable encrypted setup checklist for backup preference, Trash days and shortcut only.
- Double-click the title to restore default placement without a button or hand cursor.
- Fixed settings save footers; one Back prompts for unsaved changes, a second discards. Changed drafts reset this confirmation.
# 0.4.0 — setup, transfers and interaction fixes

- First-run welcome with new vault, backup restore and encrypted import paths; Back/Next setup and an initially masked recovery key with explicit saved confirmation.
- Password-only authenticated encrypted exports for entries/folders/attachments; independent random salt and Argon2id, safe additive imports and preserved destination credentials.
- Serialized five-attempt PIN gate, input clearing/disable through feedback, and background click-to-focus on the locked page.
- Fixed editor Save and settings Back positions, close list/footer spacing, bottom scroll fade, thin scrollbar, vector notepad icons and rounded checkboxes.
- Native Alt+Tab visibility follows pinning; moving to a folder opens the destination choices immediately.
# 0.3.0

- Distinct header active states; smooth whole-row hover; custom dark context menus.
- Smaller 342 × 446 popup with a maximum 4.5-row viewport.
- Centered, box-free typed PIN input; keypad removed; minimum reduced to four digits.
- Remaining-attempt hints only for the last three tries; five failures force the full password.
- Back navigation follows nested settings pages.
- Encrypted creation/edit metadata, a capped protected event journal, and on-demand static HTML audit export.

# 0.2.0

- Rounded, smaller, borderless popup; search on demand; About moved into Settings.
- Two-click login rows with username/password feedback and temporary topmost behavior between copies.
- Faded edit markers, five-second hover-to-trash transition, and red DELETE confirmation.
- Rearrangement insertion line and correct before/after positioning.
- Segmented entry types with per-type draft preservation.
- Animated red/shake validation and quieter forms.
- Full password after process start; configurable quick PIN for subsequent locks with an optional number pad.
- Press-to-record shortcut control and restore-default action.
- Additional security and interaction checks; compatible encrypted vault format v1.

External executable plugins and installer packaging remain deferred. This portable preview has not received an independent security audit.
