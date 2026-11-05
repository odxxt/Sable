# Sable 0.9 — secrets and everyday shortcuts

Sable keeps its compact tray window, lock/PIN behavior and encrypted storage. Shortcuts are a separate entry type, not a secret with a dummy value. The application remains local and portable.

## Create a shortcut

Use **+ → New entry → Shortcut → Action**. Give it a label and choose Application, File or folder, Windows & Control Panel, Run, PowerShell, CMD, Ping, Test port or DNS lookup. Test it once, then save. Clicking its row or tile runs it. Commands run with your normal Windows permissions. Terminals can stay open; choose that option in the editor.

Choose **Connection** or **More** for the installed SSH/RDP modules. Their dedicated editors keep host, username and password together. Clicking these entries now launches PuTTY or Windows Remote Desktop, including for existing connection entries. Credentials remain encrypted in the vault, and existing first-use approvals still apply. RDP's explicit Launch & copy option remains available in its right-click menu. There is no new credential store, proxy or authentication transport.

**Secret → Value** copies a saved value. **Secret → Login** retains username-first, password-second copying. Existing secret entries with optional actions keep working. All stored shortcuts and their settings remain encrypted too.

## Suggest a configured diagnostic

Create a Ping, Test port or DNS lookup shortcut with a saved destination and enable **Suggest for matching clipboard** in that shortcut. For example:

| Clipboard | Opted-in shortcut | Result after clicking the suggestion |
| --- | --- | --- |
| `192.168.1.20` | Ping | Ping that address |
| `192.168.1.20:443` or `server.example:443` | Test port | Test-NetConnection for that host and port |
| `server.example` or `https://example.com/page` | DNS lookup | Look up the hostname; URL path/query is discarded |
| `[2001:db8::1]:443` | Test port | Test the IPv6 host and port |

Up to three matching shortcuts appear in compact rows above the list, in saved order, including shortcuts in other folders. Click one to run it with that target. Its saved destination is unchanged. If no configured shortcut matches, no suggestion appears.

Matching is deterministic and local: no AI service, network lookup or command runs merely because the clipboard changes. Only small text is read, while the unlocked list is visible. There is no history or persisted clipboard target. Sable skips its own copies and clipboard formats marked as protected/history-excluded. Other apps may omit these marks, so this is not a universal secret detector. Disable the feature in **Settings → Copying → Suggest shortcuts from clipboard**. Suggestions only support the three diagnostic kinds in this version; arbitrary user scripts never receive clipboard substitutions.

## Rows and two-column grid

Use the view icon beside **+**. The choice is saved in encrypted preferences. Grid shows two tiles per row; click to copy or run, right-click to edit or use the other shared commands. Drag a tile to its neighbor's edge to reorder or onto a folder to move it. Only visible tiles and a small surrounding buffer are realized, so large lists do not create a control for every entry.

Folders occupy one 36-pixel row with four visible slots. Drag sideways to browse extra folders or use the mouse wheel over the strip. Browsing settles on folder boundaries and keeps the panel the same size. To rearrange or nest folders, hold for a moment (350 ms) before dragging, or start the drag vertically; drop at a sibling's edge to reorder or in its center to nest. Holding a dragged entry/folder near the strip's left or right edge scrolls to more destinations. Right-click → Move folder and Alt+Left/Right remain available.

The compact grid uses equal 68-pixel slots. The view control, folder icons and suggested-action control use restrained line icons; suggestions show a small action name above a clearer address. Keyboard focus receives the same visible surface feedback as hover. Motion respects the Windows animation preference and runs only in response to interaction.

## Updating from SableLatch

Quit the old app. Extract the complete Sable ZIP to a separate folder and copy the **entire `SableLatch-data` folder** beside `Sable.exe`. Keep its name: Sable detects it automatically. Retain the original folder as a rollback copy. Fresh installations create `Sable-data`; a custom `--data-dir` continues to work. When both directory names exist, the legacy folder takes precedence; use `--data-dir` to select explicitly. Do not run both versions against the same data or alternate old builds after saving new entry types.

The old code namespaces, cryptographic purpose strings, module IDs, startup registry value and scratch cleanup path are compatibility identifiers. They deliberately retain their previous names so the rename cannot invalidate vault keys, approvals or signed modules. Public branding, executable and catalog origin are Sable. Existing signed catalog URLs are verified first, then their exact old official repository prefix is mapped to the renamed official repository; module signature/hash checks remain enforced.
