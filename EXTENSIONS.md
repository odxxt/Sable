# Extensions

Sable 0.9.1 supports **declarative API 1 ZIP modules**. Start with the [module author guide, samples and PuTTY module](modules/README.md). External packages use a restricted JSON schema, host-owned fields and approved host operations. They cannot load executable code, WPF factories or the interfaces below. Arbitrary code sandboxing and third-party layout/toolbar contributions are deferred.

The sections below describe the internal **trusted built-in composition** interfaces. They are not the public ZIP API and must never be exposed to an unsigned executable extension.

## Registration surfaces

| Contract | Responsibility |
|---|---|
| `IContentProvider` / `ContentRegistry` | Type ID/version, name/icon, optional field definitions, and copy actions |
| `VaultItem.Payload` | Provider-owned JSON schema, preserved without interpretation by the vault |
| `IAttachmentStore` | Authenticated encrypted binary content, addressed by UUID |
| `IEntryEditorFactory` | Create a content-specific editing surface |
| `IEntryPresenter` | Supply a content-specific item template, such as a thumbnail |
| `IClipboardWriter` | Convert MIME-typed bytes into a Windows clipboard representation |
| `IEntryLayout` | Supply the collection host, such as rows or a grid |
| `ToolbarContribution` | Add an action to the compact header's extension menu |
| `IExtensionContext` | Draft broker contract for selected-item access, attachments, toolbar registration, and session cancellation |

The built-in editors register through `FieldEditorFactory`, using provider field definitions instead of separate vault schema branches. The rows layout registers through `IEntryLayout`. Text and PNG clipboard writers are supplied. Custom item templates and layouts can be registered without editing the encryption code.

## Screenshot example

`examples/ScreenshotProvider.cs` is compiled by the test suite. A host saves PNG bytes with its attachment service and creates an item whose type is `example.screenshot` and whose attachment list contains the returned ID. The provider retrieves that attachment and returns a `CopyValue` with MIME type `image/png`. The desktop PNG adapter produces the image clipboard representation.

This demonstrates new binary content flowing through existing encrypted storage, folder membership, backup, Trash, and clipboard contracts. A complete capture extension would additionally register its capture action, preview template, and editor, and request screen-capture permission from the future broker. It is deliberately not installed or advertised as a capture feature in this preview.

## Notepad example

A bundled extension could contribute a toolbar action that opens an `IEntryEditorFactory` for a multiline note type. Notes use their own JSON payload version and the existing encrypted save service. A grid extension can supply another `IEntryLayout`; the host can enumerate registered layouts. The shipped row/grid switch is in the list header and remembers each folder independently; external layout registration is not available.

## Missing or disabled providers

Disabling a module only disables its current editing/copying behavior. Data, schema versions, folder membership, and attachments stay intact. Unknown versions/types render a placeholder. Never discard or reinterpret unknown payloads as text.

## Executable plugin work still required

The WPF factories in this preview are trusted in-process composition surfaces. They are **not safe to hand directly to arbitrary unsigned code**. A production external host should expose a restricted declarative UI protocol or sandboxed broker, keep app keys private, and mediate selected-item access and clipboard operations. Use explicit permissions for screen capture, network, files, and secrets; deny ambient access. Keep secret fields host-owned; new sensitive operations require reviewed host capabilities rather than raw secret access for package code.

The API 1 data-only package path now validates a flat archive allowlist, versions, schemas, signatures and bounded sizes. It does not use an executable entry point. Unsigned declarative packages may be installed with a community warning; DLLs, scripts and EXEs are rejected regardless of signature. Future executable packages still require an independently enforced execution boundary, resource limits and a separate permission model.

### Transfer references

Store attachment references in `VaultItem.Attachments`, not as embedded IDs or file paths inside a provider payload. Import remaps those IDs to newly encrypted destination attachments. Unknown JSON payloads and content versions remain opaque and are preserved. A provider should access blobs through `IAttachmentStore`; imports never install or execute extensions.
## Advanced-entry action handlers (0.6.0)

`VaultItem.Advanced` is independent of the content type. `EntryActions.Register(IEntryActionHandler)` registers a trusted handler under a distinct Kind; Prepare produces a ProcessStartInfo without needing vault keys, a VaultStore, or provider payloads. The fixed review/launch coordinator handles explicit invocation. Current actions include application, file/folder, windows, control-panel (legacy-compatible), run, powershell and cmd. Saved diagnostic handlers remain compatible but are not offered as new actions. The Windows picker combines Settings and Control Panel without changing stored content schemas. Local first-run approvals are handled by the launch coordinator independently of handler registration. Adding a trusted handler does not change vault encryption, backup/import or normal copy behavior. Registration belongs at application composition. Declarative ZIP loading is available, but those packages cannot register executable handlers; an executable plugin broker remains deferred. A future UI contribution is still needed to expose a new handler's own options in the advanced editor.


## Starter placement and script values

The host groups installed, enabled `putty` and `rdp` manifests under **Connection** in the starter chooser. `fields` packages remain their own described starters. This presentation choice adds no permission; unsupported handlers fail validation and a new protocol needs trusted host implementation. Disabling a provider removes its creation choice while retaining encrypted entries.

The combined Secret/Login editor continues to use the existing provider IDs and payloads. Optional username, descriptions, color, per-folder layout and click behavior do not require a cryptographic-format change. PowerShell's explicit field binding is a host capability using a protected process-verified pipe, not a module API. See [the entry model](docs/ENTRY-MODEL.md) and [architecture](ARCHITECTURE.md).
