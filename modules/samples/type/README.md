# Type clipboard

An optional declarative module for Sable's host-owned typing capability. Install from Plugins & modules, then use New entry → Extras → Type clipboard, or Type clipboard in the tray menu. Tray typing works while the vault is locked.

Choose a target with the crosshair; Sable types the current clipboard after a 500 ms default delay, with 25 ms between characters. Esc cancels. Changing destination windows, locking Windows or exiting also stops the operation. Nothing reads vault fields, stores clipboard history or appends Enter. Internal newlines/tabs are preserved as keys; trailing newlines/tabs are removed, while spaces remain. Multiline/tabbed text asks for confirmation by default. A first-use explanation covers sensitive clipboard text.

Module defaults apply to the tray and entries that inherit them. Each entry can override timing, multiline confirmation and input mode. Unicode is suitable for normal fields; keyboard compatibility uses the destination keyboard layout for remote/VM consoles. Unsupported characters fail before typing in compatibility mode. Remote keyboard layouts must match. Windows integrity rules can block input into elevated programs; there is no automatic elevation or UAC bypass.

Global non-secret preferences live locally in type-host.json to support locked tray use. Entry names, overrides, colors and descriptions use vault encryption. Hooks exist only during the chosen operation; installed ZIPs contain no executable code. This package requires the Type-capable host; earlier releases reject the handler. For a community derivative, use a new namespace and omit the official signature.
