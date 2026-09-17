namespace SableLatch.Core;

[Flags]
public enum ExtensionPermission { None = 0, SelectedItem = 1, ClipboardWrite = 2, ScreenCapture = 4, Network = 8 }
public record ContentDescriptor(string Id, int Version, string Name, string Symbol, bool Sensitive);
public enum EditorFieldKind { Text, Multiline, Secret }
public record EditorField(string Key, string Label, EditorFieldKind Kind, bool Required = true);
public record CopyValue(string MediaType, byte[] Data, bool Sensitive, bool KeepPanelOpen);
public record ToolbarContribution(string Id, string Label, string Symbol, Func<CancellationToken, Task> Execute);

// Public contracts are intentionally independent of WPF and of the on-disk cryptography.
// External modules will be mediated by a permission broker; never hand them VaultStore.
public interface IContentProvider
{
    ContentDescriptor Descriptor { get; }
    IReadOnlyList<string> CopyActions { get; }
    IReadOnlyList<EditorField> EditorFields => [];
    CopyValue Copy(VaultItem item, string action);
}
public interface IAttachmentStore
{
    Guid SaveAttachment(ReadOnlySpan<byte> bytes);
    byte[] ReadAttachment(Guid id);
}
public interface IExtensionContext
{
    int ApiVersion { get; }
    CancellationToken SessionCancellation { get; }
    Task<VaultItem?> RequestSelectedItemAsync();
    Task SaveSelectedItemAsync(VaultItem item);
    Task<Guid> SaveAttachmentAsync(ReadOnlyMemory<byte> bytes);
    void RegisterToolbar(ToolbarContribution action);
}

public sealed class ContentRegistry
{
    private readonly Dictionary<string, IContentProvider> providers = [];
    public IEnumerable<IContentProvider> All => providers.Values;
    public void Register(IContentProvider provider)
    {
        if (!providers.TryAdd(provider.Descriptor.Id, provider)) throw new InvalidOperationException("Content type already registered.");
    }
    public IContentProvider? Find(string id) => providers.GetValueOrDefault(id);
    public static ContentRegistry BuiltIns()
    {
        var r = new ContentRegistry();
        r.Register(new TextProvider(false)); r.Register(new TextProvider(true)); r.Register(new LoginProvider());
        return r;
    }
}
public sealed class TextProvider(bool secret) : IContentProvider
{
    public ContentDescriptor Descriptor => new(secret ? "sable.secret" : "sable.text", 1, secret ? "Secret" : "Text", secret ? "\uE72E" : "\uE8A5", secret);
    public IReadOnlyList<string> CopyActions => ["Copy"];
    public IReadOnlyList<EditorField> EditorFields => [new("text", secret ? "Secret value" : "Text", secret ? EditorFieldKind.Secret : EditorFieldKind.Multiline)];
    public CopyValue Copy(VaultItem item, string action) => new("text/plain", System.Text.Encoding.UTF8.GetBytes(item.Field("text")), secret || item.IsSensitive, false);
}
public sealed class LoginProvider : IContentProvider
{
    public ContentDescriptor Descriptor => new("sable.login", 1, "Login", "\uE77B", true);
    public IReadOnlyList<string> CopyActions => ["Username", "Password"];
    public IReadOnlyList<EditorField> EditorFields => [new("username", "Username or email", EditorFieldKind.Text), new("password", "Password", EditorFieldKind.Secret)];
    public CopyValue Copy(VaultItem item, string action) => action switch
    {
        "Username" => new("text/plain", System.Text.Encoding.UTF8.GetBytes(item.Field("username")), false, true),
        "Password" => new("text/plain", System.Text.Encoding.UTF8.GetBytes(item.Field("password")), true, false),
        _ => throw new InvalidOperationException("Unknown copy action.")
    };
}

public sealed class SessionPolicy(TimeProvider? clock = null)
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private long opened;
    public bool IsUnlocked { get; private set; }
    public bool IsPinned { get; set; }
    public void Unlock() { opened = time.GetTimestamp(); IsUnlocked = true; }
    public void Lock() { IsUnlocked = false; }
    public bool Expired(int minutes) => IsUnlocked && !IsPinned && time.GetElapsedTime(opened).TotalMinutes >= minutes;
    public void SystemLocked() => Lock();
}
