# Sable 0.9.1 interface refinements

The welcome page leads with Sable, a short explanation, and a repeating native illustration. Its clocks are removed when hidden or unloaded. Reduced motion shows a settled illustration. No browser, video, background service or timer was added.

Clicking a revealed recovery key copies it through the protected clipboard path, releases textbox mouse capture, fades the key and displays a green confirmation in the same box. The key returns after the short confirmation. Save key remains available. These controls do not resize the page or block Next.

The settings checklist appears after three active saved entries. Completed tasks are dimmed. Backup directory pickers place Browse directly beside the editable path.

Plugins & modules shows Installed modules and available connection templates with an Add action. Install from ZIP and Online catalog are separate actions; included connection modules can be added offline. Standard Secret, Login and Shortcut templates are under Default entry types, with a short explanation. Existing module verification, installation review and encrypted preservation remain in effect.

The entry editor now uses described starter rows instead of a dropdown; see [Choose a starter](ENTRY-MODEL.md). Connection settings expand below credentials under More options, with shared inner padding and a subtle background that connects the controls visually. Script fields appear for PowerShell; a shortcut can also expose a value when its Copy behavior needs one. Plain secrets omit action-only behavior controls. About derives the displayed version from the executable assembly.

## Folder deletion

Right-click → Delete folder opens that folder's edit page at its deletion section. Press Delete folder to see the number of affected entries/subfolders and an irreversible-action notice. The second button becomes available after a brief delay, preventing an accidental double-click from confirming immediately.

Confirmation permanently removes the selected folder, every nested folder and all their entries, including trashed entries. Other folders and root entries are preserved. Unreferenced attachment cleanup uses the existing vault routine. This is removal from the current vault; independent encrypted backup/export copies are not altered, and storage-device physical erasure is not promised.

## Design review

| Before | After | Why |
| --- | --- | --- |
| Generic welcome tagline above the app identity | Large Sable wordmark, explanation, then a looping illustration | Establish the first impression in a clear reading order |
| Copy feedback occupied its own row; textbox could retain capture | Feedback overlays the key; click explicitly releases capture | Keep geometry stable and the rest of the page interactive |
| Folder path and its picker were separated vertically | One aligned field-and-Browse row | Make their relationship visible |
| Module controls mixed built-ins, catalog and install actions | Installed/Add rows with optional standard-type controls | Give each action a clear purpose |
| Connection options looked detached from their inputs | A single rounded disclosure section | Visually group the configuration |

Native WPF rendering and focused interaction checks cover this update. Physical pointer gestures, Windows 10 capture tools, elevated launch and live remote connections remain manual checks. The PowerShell credential transport is unchanged from the preceding preview.


## Starter spacing and three window modes

Starter rows are flat dark surfaces with short descriptions/tooltips and faint hover feedback. Their 14-pixel side padding, fixed icon slot and centered back target align the page. The previous left selection marker is removed. Change uses an explicit muted text brush. Connection choices use native monitor/terminal vectors. Advanced, More options and Color & description use real internal padding rather than negative margins. Username toggles briefly fade the affected fields without a resident animation timer.

The pin cycles **Tray → Always on top → Normal window** using the same fixed-size drawing at three orientations. Both kept-open modes appear in taskbar/Alt+Tab by default; Always/Never preferences override that. Only Always on top suspends timeout enforcement. Normal window retains timed locking and never becomes topmost for login copying. Mode changes preserve session age and immediately apply overdue timeouts. Windows-session locking applies to every mode.

The latest pass completed **305 core checks and 53 focused native UI checks**. [Verification](../VERIFICATION.md) distinguishes these results from earlier refinement runs. This changes UI/orchestration, not the vault format or signed module packages.
