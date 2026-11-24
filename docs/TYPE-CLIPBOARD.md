# Type clipboard — Sable 0.9.4 preview

Install the optional **Type clipboard** module in **Settings → Plugins & modules → GitHub catalog** or install its signed ZIP. Create entries via **New entry → Extras → Type clipboard** or use the tray-menu action, even while locked.

## Use

1. Copy plain text in any app, including a value explicitly copied from Sable.
2. Click a Type entry or the tray action. First use explains destination exposure. Multiline/tabbed text asks for confirmation by default, without showing its contents.
3. Sable hides. Click a destination using the crosshair.
4. After the configured delay, text is typed. Esc cancels during selection, delay or typing. Moving to another foreground window cancels; already typed text is not undone.

Internal line breaks and tabs are preserved as keys. Consecutive CR/LF representing a Windows newline become one Enter. Trailing CR, LF and Tab characters are removed, including multiple trailing blank lines; normal spaces are preserved. No Enter is appended. Unsupported control codes and invalid Unicode are rejected. Clipboard snapshots are bounded to 100,000 UTF-16 characters and are never saved or logged.

## Preferences

Module settings supply **500 ms start delay**, **25 ms between characters**, **Automatic · apps & remote control**, and **Confirm multiline text** by default. Type entries use module defaults unless you disable **Use module defaults** and save overrides. Tray typing always uses global defaults. Global non-secret preferences are local to the data folder in `type-host.json`; entry names/settings remain encrypted.

**Automatic** sends virtual-key and scan-code events for characters represented by the destination Windows keyboard layout, as remote-control clients and browser consoles commonly expect. Other characters use Unicode packets; a remote client may not forward those. **Unicode text** remains available for normal fields. **Keyboard compatibility** uses keyboard events only and rejects unmappable characters before typing. Characters that cannot be represented in that layout are rejected before typing; select Unicode or change the layout. Remote and local keyboard layouts must agree. VM/remote console behavior and elevated windows require testing in your environment; this is not universal compatibility or an elevation bypass.

Old default-Unicode module preferences and overrides migrate to Automatic once, retaining delay/speed. Explicit Unicode choices saved by the new editor remain unchanged. First-use/multiline/error prompts appear at the bottom right of the pointer's monitor.

Only one Type operation runs at once. No background hook, service, text history or network request is added. Disabling/uninstalling the module removes its creation/tray capability and preserves encrypted entries for reinstallation. Type packages cannot access vault fields or supply executable code.

The interaction was studied from [ClickPaste](https://github.com/Collective-Software/ClickPaste); Sable uses its own native host implementation and adds explicit focus checks. Windows input rules are documented in [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput). See [Security](../SECURITY.md#type-clipboard-093) and [Verification](../VERIFICATION.md).
