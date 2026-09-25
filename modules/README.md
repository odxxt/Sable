# Modules for SableLatch

Modules add entry types and actions while keeping SableLatch small. Install a ZIP from **Settings → Plugins & modules → Install ZIP**, or open **Catalog** to install the included PuTTY SSH and Remote Desktop modules without a download. **Check GitHub catalog** fetches the signed catalog only when clicked.

## Start with a sample

1. Copy `samples/quick-note` to your own folder.
2. Edit `manifest.json` in a text editor. Give your module a unique ID, such as `yourname.quick-note`, and change its name and fields.
3. Put `manifest.json` at the root of a ZIP, not inside another folder. Or run:

   ```powershell
   .\Pack-Module.ps1 -Folder .\samples\quick-note -Destination .\my-note.zip
   ```

4. Install the ZIP in SableLatch and review its permission summary.
5. Choose **+ → New entry → More**. Select your module, enter a name and its values, then save.

You do not need Visual Studio, a compiler, a developer account or a signing certificate to make a community module. The sample ZIP in `packages/example.quick-note.zip` is ready to try. Community modules show **Not reviewed by SableLatch**.

## API 1: data-only packages

This release interprets a small JSON definition; it does not run a module's JavaScript, Python, PowerShell, DLL, EXE or XAML. This is a deliberately restricted first API, not a general-purpose code sandbox. Arbitrary executable modules, custom layouts, toolbar contributions and background tasks are not supported. New sensitive integrations require reviewed host capabilities.

The complete ZIP may contain only:

| File | Required | Purpose |
| --- | --- | --- |
| `manifest.json` | Yes | Identity, version, entry fields and actions |
| `README.md` | No | Author documentation; never executed |
| `LICENSE` | No | Your module license |
| `module.sig` | Official modules only | Raw ECDSA P-256/SHA-256 signature over the exact manifest bytes, IEEE P1363 format |

Limits: 256 KiB ZIP and expanded total, 64 KiB per member, 16 fields, 6 actions and 100 installed modules. Paths, duplicate names, symlinks and additional files are rejected. JSON property names are case-sensitive; unknown properties and duplicate properties are rejected.

### Manifest

```json
{
  "apiVersion": 1,
  "id": "yourname.quick-note",
  "version": "1.0.0",
  "name": "Quick note",
  "author": "Your name",
  "description": "Save and copy an ordinary note.",
  "icon": "note",
  "handler": "fields",
  "fields": [
    { "key": "body", "label": "Note", "kind": "multiline", "required": true }
  ],
  "actions": [
    { "id": "copy", "label": "Copy note", "kind": "copy", "field": "body" }
  ]
}
```

IDs have two or more dot-separated lowercase segments. The `sablelatch.*` namespace is reserved for verified official packages. Version is a numeric version such as `1.0.0`. Icons: `note`, `terminal`, `mail`, `lock`. Field kinds: `text`, `multiline`, `secret`.

`copy` is a host operation. It can target only a declared **non-secret** field. A module defining any secret field produces sensitive entries, so the host's secret clipboard clearing/history policy also applies to its permitted copy operations. The package never receives an entry object, field values or encryption keys. The first copy asks for approval; **Don't ask again** is scoped to that entry, action and module manifest. The Plugins page can reset remembered approvals.

`handler: "putty"` requests the reviewed host integration. It uses the fixed protected PuTTY form and cannot declare replacement fields, custom arguments or scripts. Community packages can request it, but remain community packages and require the same connection review and executable verification.

### Ask an AI to make a module

Give the AI this document and the sample, then use a request like:

> Create a SableLatch API 1 community module ZIP. Use the fields handler, a unique lowercase dotted ID, and only supported properties. Add these fields: [describe them]. Declare passwords as secret fields and never create copy actions for secret fields. Use host-managed copy actions for these ordinary fields: [describe them]. Include a short README explaining the module. Put manifest.json directly at the ZIP root. Do not include executable code, network requests, HTML, XAML or scripts.

SableLatch validates the result; an AI-generated package does not receive special trust.

## PuTTY SSH 1.0

Install the included official PuTTY module, then use **+ → New entry → More → PuTTY SSH**. Enter name, host, username and password. Advanced options include port, one expected server fingerprint, IPv4/IPv6 preference, SSH compression, keepalive (0-600 seconds) and terminal font/size.

Click the entry to view details. Its **Run** button launches PuTTY. The first connection reviews the destination, with an optional **Don't ask again for this connection**. Editing the destination/options, changing the module manifest, changing the PuTTY executable or moving to a new machine invalidates the remembered approval.

The app detects the standard PuTTY installation directories. In the module's settings, choose a different installed `putty.exe` or follow the official download link. PuTTY is not bundled. It must be a locally installed, Windows-trusted executable signed by **Simon Tatham**, with PuTTY identity and version 0.77 or newer. The handoff was exercised against PuTTY 0.85; other supported versions require compatibility testing.

Passwords stay in the encrypted vault. SableLatch creates a local-only, unpredictable, single-instance Windows pipe, launches PuTTY directly and checks that the pipe client is that exact launched process before writing the password. The command line contains the pipe name, never the password. Passwords are not sent through the clipboard, environment, files or logs. The host clears its mutable transfer buffers. .NET's existing unlocked document and PuTTY's memory are separate and cannot be promised immediate physical erasure.

A temporary **non-secret** PuTTY session is created under the current user's PuTTY registry Sessions key to avoid inherited logging, proxy commands or other saved-session behavior. It is deleted after the handoff. A process crash may leave an inert session entry behind; it contains no password. Server host-key checking stays enabled. Do not accept an unknown or changed host identity without verifying it independently.

Locking cancels pending password delivery; the pending child process is stopped if delivery fails. Established SSH sessions stay open after SableLatch locks. The 15-second delivery timeout does not limit how long the SSH session may remain open.

**Not in this release:** SSH private-key storage/authentication, existing PuTTY saved sessions, jump hosts/proxies, tunnels, remote commands, rich-text composition, image/file attachments or arbitrary executable module code. These need separate host capabilities and tests. HTML clipboard preservation is now part of the built-in Text type. The RDP host capability is available in 0.8.0; see [RDP implementation](../docs/RDP-DESIGN.md).

## Updates, removal and recovery

Install an updated ZIP with the same ID and author. API 1 rejects downgrades and changes to field names/types or the handler; publish a new ID for a changed storage schema. Stored entries carry a schema fingerprint so an incompatible replacement cannot reinterpret an old secret as ordinary text.

Disable or uninstall a module from its settings. Its encrypted entries remain and show **Module unavailable**. Reinstall a compatible package to use them again. Backups and exports preserve entries and schema bindings, but never install packages or transfer local permissions. Restore the data, reinstall the module, locate PuTTY on the new PC, and review the connection again.

Module settings use the vault's encrypted extension settings. Package manifests are public metadata stored in `SableLatch-data/modules`; keep credentials out of manifests and package documentation.

## Catalog and publisher trust

The shipped publisher public key is `publisher.pub`. Official manifests and `catalog.json` are verified against it. The catalog envelope holds base64 `payload` and `signature`; its signed payload is a JSON array of name, description, URL and ZIP SHA-256. Downloads are restricted to HTTPS under the official `raw.githubusercontent.com/odxxt/SableLatch/` repository path, with no redirects. Packages must also have a verified official manifest signature. No automatic background catalog checks or updates occur.

The signed PuTTY catalog is published and was verified online on 24 September 2026. See [Publishing](../docs/PUBLISHING.md) for updates. The bundled module and local ZIP installation also work offline.

Official signing material must remain outside the repository and release archives. Back up the publisher private key and keep its unlock password separately. Losing the key requires an explicit trust-key transition in a future app version. A valid signature verifies publisher identity and package integrity; it is not an independent security audit.

## Remote Desktop 1.0 (requires SableLatch 0.8.0)

`handler: "rdp"` and `icon: "desktop"` request the host-owned RDP editor and launcher. Fields and actions must be empty, just like the protected PuTTY capability: ZIP authors cannot add credential extraction, arbitrary arguments, shell commands or executable files. See `samples/rdp/manifest.json`. A community example must use its own namespace rather than `sablelatch.*` and omit the official signature.

Install Remote Desktop from the bundled catalog. Click its entry row to connect, or right-click for details. The first-use review is remembered only on this Windows environment; destination, account, clipboard option, helper or package changes require a new review. Disabling/uninstalling preserves encrypted entries. Server authentication is required. See the [user guide](../docs/USER-GUIDE.md#remote-desktop-080) for setup and limitations.

The source catalog now includes the signed RDP ZIP as well as PuTTY. The RDP catalog update becomes available online only after this source version is pushed to GitHub. Until then, the bundled catalog and local ZIP installation work offline.