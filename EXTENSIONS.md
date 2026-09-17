# Extension development foundation

The preview ships trusted built-ins and extension contracts. It does **not** install ZIP modules or promise a stable production plugin ABI yet.

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

This demonstrates new binary content flowing through existing encrypted storage, folder membership, backup, Trash, and clipboard contracts. A complete capture extension would additionally register its capture action, preview template, and editor, and request screen-capture permission from the future broker. It is deliberately not installed or advertised as a capture feature in v0.1.

## Notepad example

A bundled extension could contribute a toolbar action that opens an `IEntryEditorFactory` for a multiline note type. Notes use their own JSON payload version and the existing encrypted save service. A grid extension can supply another `IEntryLayout`; the appearance settings enumerate registered layouts.

## Missing or disabled providers

Disabling a module only disables its current editing/copying behavior. Data, schema versions, folder membership, and attachments stay intact. Unknown versions/types render a placeholder. Never discard or reinterpret unknown payloads as text.

## External plugin work still required

The WPF factories in this preview are trusted in-process composition surfaces. They are **not safe to hand directly to arbitrary unsigned code**. A production external host should expose a restricted declarative UI protocol or sandboxed broker, keep app keys private, and mediate selected-item access and clipboard operations. Use explicit permissions for screen capture, network, files, and secrets; deny ambient access. Authorize individual secret access when needed.

Before ZIP distribution, define a manifest with ID/version/API range, content schemas, capabilities, resources, and entry point. Validate archive paths, symlinks, size limits, signatures when available, and update compatibility before enabling anything. Unsigned packages may be accepted with a warning only after the execution boundary is implemented. Add migration/rollback tests and resource limits. Official GitHub hosting and signing keys are project-owner decisions still to be made.

### Transfer references

Store attachment references in `VaultItem.Attachments`, not as embedded IDs or file paths inside a provider payload. Import remaps those IDs to newly encrypted destination attachments. Unknown JSON payloads and content versions remain opaque and are preserved. A provider should access blobs through `IAttachmentStore`; imports never install or execute extensions.
## Advanced-entry action handlers (0.6.0)

`VaultItem.Advanced` is independent of the content type. `EntryActions.Register(IEntryActionHandler)` registers a trusted handler under a distinct Kind; Prepare produces a ProcessStartInfo without needing vault keys, a VaultStore, or provider payloads. The fixed review/launch coordinator handles explicit invocation. Default handlers are application, windows, control-panel (legacy-compatible), run, powershell and cmd. The Windows picker combines Settings and Control Panel without changing stored content schemas. Local first-run approvals are handled by the launch coordinator independently of handler registration. Adding a trusted handler does not change vault encryption, backup/import or normal copy behavior. Registration currently belongs at application composition; external ZIP loading and a public safe plugin broker remain deferred. A future UI contribution is still needed to expose a new handler's own options in the advanced editor.
