# Sable

**Keep it close. Keep it private.**

A compact tray companion for Windows. Keep secrets, logins, useful commands, files and server connections together. Copy a value or run a shortcut, with local clipboard suggestions and a choice of rows or a two-column grid.

[**Downloads for Windows x64**](https://github.com/odxxt/Sable/releases) · [User guide](docs/USER-GUIDE.md) · [Roadmap](#whats-next)

<p align="center">
  <img src="docs/images/vault.png" width="310" alt="Sable compact tray vault with folders and click-to-copy entries">
  &nbsp;
  <img src="docs/images/grid.png" width="310" alt="Sable two-column grid with compact shortcuts and secret entries">
</p>

*Screenshots use fictional demonstration data.*

## Keep useful things within reach

- **Secrets and Shortcuts.** Save a private value or Login to copy, or a standalone Shortcut to run a command, open a file or connect to a server. Logins still copy the username first and password on the next click.
- **Small, calm and close at hand.** A rounded dark panel, subtle feedback, a configurable keyboard shortcut and a pin to keep it open. Unpinned, it stays out of the taskbar and Alt+Tab by default.
- **Organize your own way.** Nested folders with drag-to-move/reorder, search, direct row dragging and folder drops, entry duplication, compact entry colors and multiline text or secrets. Reveal a secret only when you choose.
- **Useful advanced actions.** Open an application, find Windows Settings or Control Panel pages, use a Windows+R-style command, or run CMD/PowerShell. Choose Run or Run & copy, test before saving, with terminals staying open by default.
- **Keep useful formatting.** Paste formatted content into an empty Secret to retain common HTML formatting and tables when copying to supporting apps. The editor stays masked, plain text remains available as a fallback, and secret clipboard protection applies to both formats.
- **Lock when it matters.** A full password at startup, optional 4–12 digit quick PIN during that process session, configurable timed locking and Windows session locking. Five incorrect PINs require the full password. Pinning pauses the timer, but never overrides Windows locking.
- **Thoughtful clipboard handling.** Secret copies clear after 30 seconds by default if still owned by Sable. The delay is configurable; Windows clipboard-history exclusion and supported screen-capture exclusion are requested.
- **Your data stays local.** An encrypted vault, encrypted import/export and optional encrypted backups. Backups start **off**. No account, cloud service, telemetry, browser engine or resident background service. Each open RDP session uses a separate local process.
- **Add your own entry types.** Install declarative ZIP modules, use protected PuTTY SSH and Remote Desktop entries, or start with the quick-note sample. See the [module guide](modules/README.md). No background module tasks or executable plugin code.
- **Recover and review.** A master recovery key, configurable encrypted Trash for secrets, entry timestamps and a small protected activity history that can be exported to an HTML report.

## New in v0.9.0 preview

- **Sable:** the shorter app name matches [odxxt/Sable](https://github.com/odxxt/Sable).
- **Secrets and Shortcuts:** Secrets copy values or login credentials. Standalone Shortcuts run applications, files/folders, Windows settings, Run commands, PowerShell or CMD. RDP and SSH entries now launch on click, with credentials still encrypted inside their entries.
- **Clipboard suggestions:** opt a Ping, Test port or DNS lookup shortcut into matching. Sable suggests your configured shortcut for an IP, host:port, or hostname/website. Click to run it with that target; the saved destination stays unchanged. Matching stays local, only while the unlocked panel is visible, and keeps no history. Nothing runs automatically.
- **Rows or grid:** switch with the small view button beside +. Grid shows two entries across, supports editing, copying/running and dragging, and virtualizes long lists.
- **One row of folders:** four compact slots, smooth sideways browsing for more, and the existing drag-to-reorder or nest behavior. Clipboard suggestions now separate the action label from the readable destination.

See [shortcut setup and migration](docs/SABLE-0.9.md). Clipboard suggestions currently support the three diagnostic action types, not arbitrary script-template substitution.

### Included from v0.8.8

- **Create your first real secret:** finish vault setup, then follow gentle highlights through the actual + menu and Secret editor. A small Skip lets you continue without the guide. No pretend Wi-Fi entry or separate first-secret form.
- **Consistent entry menus:** Edit, Duplicate, Move to folder and Delete stay in the same order, with any Run/Launch actions above them. SSH and RDP use the same pattern.
- **Windows 10 capture compatibility:** use an opaque rounded panel to avoid transparent-window conflicts, and reapply protection when opening or pinning. Older Windows 10 can blank content rather than omit the window; actual capture behavior still depends on Windows and the capture tool. See [security limits](SECURITY.md#screen-capture).

### Included from v0.8.7

- **Compact setup:** Back keeps drafts; tooltips explain controls, and brief transitions respect Windows animation preferences.
- **Secret and Login:** Text is no longer a separate choice. Existing Text entries become Secrets while retaining values, formatting, identity and timestamps. Their values are masked and excluded from search; secret clipboard and Trash preferences now apply.
- **Ready to type:** opening the locked panel or pressing Lock focuses the PIN/password immediately. The shortcut also focuses an already-visible lock screen.

### Included from v0.8.6

- **Folder dragging repaired:** held folder buttons now start dragging correctly. Move to another folder's edge to reorder, or its center to nest. Existing folder contents move with it.

### Included from v0.8.5

- **Native Remote Desktop:** launch the Windows RDP app with saved credentials, use full screen or a normal window, and copy/paste between computers. Existing entries adopt the new defaults. Windows/server policy still applies.
- **Folders that move with you:** drag edges to reorder, drop in the center to nest, or use right-click → Move folder. Drop confirmations fade in place without shifting the layout.
- **Duplicate and adjust:** open an unsaved copy of any editable entry, including logins and installed module entries. Cancel leaves the vault unchanged.
- **Quieter deletion:** a centered Delete button and cancel icon appear inside the row. Escape cancels. Existing Trash preferences still decide what gets retained.

See the [changelog](CHANGELOG.md), [user guide](docs/USER-GUIDE.md) and [verification record](VERIFICATION.md) for details.

## Download and start

**Currently available: standalone/portable only. An installer edition is planned.**

Target: **Windows 10/11, x64**. The ZIP includes the .NET Desktop Runtime; you do not need to install .NET separately to run it. ARM64 and 32-bit packages are not currently provided. See [runtime OS support](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md) and [verification limits](VERIFICATION.md) for platform details.

1. Download **Sable-v<version>-win-x64.zip** from the [release page](https://github.com/odxxt/Sable/releases). GitHub's automatic **Source code** archives are for developers, not the runnable app.
2. Extract the **entire ZIP** to a writable folder. Keep all included files together.
3. Open **Sable.exe**. Create a vault or restore an existing encrypted backup.
4. Save your recovery key somewhere safe outside the app. It is shown during setup and is not available to reveal again later.
5. Open Sable from the tray or press **Ctrl+Alt+Space**. Use **+ → New entry**. Click a Secret to copy or a Shortcut to run.

You can change the shortcut and startup behavior in Settings. **Enter** saves or continues; **Shift+Enter** adds a line. The close button hides the panel. Settings → **Quit → Confirm quit** exits the app.

Checksums are included with each release as `SHA256SUMS-v<version>.txt`. To check a download in PowerShell:

```powershell
Get-FileHash .\Sable-v0.9.0-win-x64.zip -Algorithm SHA256
```

### Updating

Exit the old app, extract the new release separately, and copy your **entire data folder** beside the new executable. Keep an existing `SableLatch-data` name; Sable detects it automatically. Fresh installs use `Sable-data`. Include attachments and keep the old folder as a rollback copy. Open the new version on the same Windows account/computer. Do not run two versions against the same vault or alternate older builds on newer data.

## Encryption, explained

Sable uses established cryptographic primitives. These links describe the underlying algorithms; they are not a certification or audit of this application.

| Component | How Sable uses it | Reference |
| --- | --- | --- |
| **AES-256-GCM** | Authenticated encryption for vault data, attachments and encrypted envelopes, with fresh 96-bit nonces, 128-bit authentication tags and purpose-bound associated data. | [NIST SP 800-38D](https://csrc.nist.gov/pubs/sp/800/38/d/final) |
| **Argon2id** | Password-based key derivation: 64 MiB memory, 3 passes, 1 lane, a random 128-bit salt and a 256-bit output. | [RFC 9106](https://www.rfc-editor.org/rfc/rfc9106) |
| **RSA-3072 / OAEP-SHA256** | Wraps each backup's fresh AES key. The private key is itself encrypted under the backup password. | [RFC 8017](https://www.rfc-editor.org/rfc/rfc8017) |
| **ECDSA P-256 / SHA-256** | Verifies official module manifests and the GitHub catalog against the embedded publisher key. | [NIST FIPS 186-5](https://csrc.nist.gov/pubs/fips/186-5/final) |
| **HMAC-SHA256** | Combines a process-only random secret with the PIN-derived key for quick-unlock sessions. The PIN is not a replacement for the vault password. | [RFC 2104](https://www.rfc-editor.org/rfc/rfc2104) |
| **SHA-256** | Recovery-key typo checks, configuration fingerprints and clipboard ownership checks; not the password KDF. | [NIST FIPS 180-4](https://csrc.nist.gov/pubs/fips/180-4/upd1/final) |
| **Windows DPAPI** | Protects normal access using the Windows user context and app-specific machine identity. This is Windows protection, not a standalone encryption algorithm or TPM guarantee. | [Microsoft CryptProtectData](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata) |

Recovery keys contain **256 cryptographically random bits**. Random salts prevent reusable rainbow tables; Argon2id makes guessing more costly. Offline files cannot enforce an attempt limit, so strong, unique passwords still matter.

### Which credential opens which file?

| Operation | What you need |
| --- | --- |
| Normal vault access | App password on the registered Windows environment; optionally a PIN after the first password unlock in a running session. |
| Recover a moved vault or forgotten app password | Master recovery key and the complete vault data folder. |
| Restore a `.sablebackup` | That backup's password **and** its original recovery key. |
| Import a `.sableexport` | Its export password. The vault recovery key does not recover a forgotten export password. |

This is a **preview**, not an independently audited password manager. Capture exclusion depends on the recording method; it cannot block every recorder or camera. Advanced actions run with your Windows account's permissions and are not sandboxed. See the [security design and limits](SECURITY.md) before storing important data.

## What's next

The direction is a small app with room to grow. Checked items are available now; unchecked items are planned without a promised release date:

- [x] **Declarative ZIP modules:** installation, enable/disable controls, beginner samples, API 1 field/action contracts and the first PuTTY module.
- [x] **Local Remote Desktop:** click to launch Windows Remote Desktop, with an optional Launch & copy preference. A temporary Windows-encrypted connection file is used; see the [security limits](docs/RDP-DESIGN.md).
- [x] **Module trust and permissions:** signed official packages, community warnings and scoped host-operation consent.
- [ ] **More module capabilities:** encrypted SSH-key authentication, additional RDP options, and a separately reviewed sandbox for executable extensions.
- [ ] **More entry types:** image/screenshot and attachment providers that use the existing encrypted storage and clipboard contracts.
- [ ] **A Windows installer:** alongside the portable version, with a straightforward upgrade path.
- [ ] **Additional languages:** selectable translations with right-to-left and left-to-right layouts.
- [ ] **Trusted release delivery:** signed builds and an update experience that preserves local vaults.
- [ ] **Further hardening and performance work:** broader Windows/DPI testing, recovery testing across machines, independent security review and improvements for larger collections.

The current app already separates content, storage, clipboard, editors, layouts and toolbar actions. External **declarative ZIP modules are available**. Arbitrary executable plugins and external layout/toolbar contributions remain disabled. The [extension guide](EXTENSIONS.md) and [screenshot-provider example](examples/ScreenshotProvider.cs) explain the foundation.

## Build it yourself

Install a **.NET 10 SDK on Windows**, then run:

```powershell
git clone https://github.com/odxxt/Sable.git
cd Sable
.\build.ps1 -Portable
```

The script runs the core checks and publishes the standalone app to `artifacts/Sable-portable`.

For development:

```powershell
dotnet run --project tests/SableLatch.Tests
dotnet run --project src/SableLatch.Desktop
```

The preview is covered by core/storage/package checks, WPF interaction checks and native credential-pipe tests, including a real PuTTY 0.85 pipe-reader check. Full registry-backed launch testing needs a normal Windows session. See [VERIFICATION.md](VERIFICATION.md) for what was tested and what remains unverified.

## Explore and contribute

Found a bug or have an idea? [Open an issue](https://github.com/odxxt/Sable/issues). Include the app version, Windows version and steps to reproduce. Use fictional entries in screenshots and examples; never attach your vault, passwords or recovery key.

- [User guide](docs/USER-GUIDE.md) — everyday controls, settings and recovery.
- [RDP design](docs/RDP-DESIGN.md) — native Windows launch, credential-file lifecycle and limitations.
- [Publishing and catalog](docs/PUBLISHING.md) — source pushes, app releases and signed module updates.
- [Architecture](ARCHITECTURE.md) — how the parts fit together.
- [Security](SECURITY.md) — encryption, threat boundaries and verification limits.
- [Extensions](EXTENSIONS.md) — the modular foundation and future add-ons.
- [Changelog](CHANGELOG.md) — what's changed.

## License

Sable is licensed under the **[MIT License](LICENSE)**. Third-party components retain their own licenses; see [THIRD-PARTY.md](THIRD-PARTY.md).
