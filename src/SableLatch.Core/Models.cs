using System.Text.Json;

namespace SableLatch.Core;

public static class Codec
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 32 };
    public static byte[] Encode<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);
    public static T Decode<T>(byte[] bytes) => JsonSerializer.Deserialize<T>(bytes, Options) ?? throw new InvalidDataException("Invalid data.");
    public static T Clone<T>(T value) => Decode<T>(Encode(value));
}

public sealed class VaultItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ContentType { get; set; } = "sable.secret";
    public int ContentVersion { get; set; } = 1;
    public string Label { get; set; } = "";
    public bool IsSensitive { get; set; } = true;
    public AdvancedEntry? Advanced { get; set; }
    public Guid? FolderId { get; set; }
    public int Order { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public JsonElement Payload { get; set; } = JsonSerializer.SerializeToElement(new { text = "" });
    public List<Guid> Attachments { get; set; } = [];
    public string? ModuleSchema { get; set; }
    public string Field(string key) => Payload.ValueKind == JsonValueKind.Object && Payload.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}

public sealed class VaultFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "New folder";
    public string Icon { get; set; } = "folder";
    public int Order { get; set; }
}

public sealed class Preferences
{
    public List<string> CompletedSetupSteps { get; set; } = [];
    public int SessionMinutes { get; set; } = 60;
    public bool LockEveryOpen { get; set; }
    public bool PinPromptDismissed { get; set; }
    public bool ClearSecretClipboard { get; set; } = true;
    public int ClipboardSeconds { get; set; } = 30;
    public bool CloseAfterCopy { get; set; } = true;
    public bool TrashSecrets { get; set; } = true;
    public bool TrashNormal { get; set; }
    public bool ExpireTrash { get; set; } = true;
    public int TrashDays { get; set; } = 7;
    public bool Startup { get; set; } = true;
    public string Shortcut { get; set; } = "Ctrl+Alt+Space";
    public string TaskbarMode { get; set; } = "pinned";
    public bool BackupEnabled { get; set; }
    public int BackupHours { get; set; } = 24;
    public int BackupKeep { get; set; } = 7;
    public string BackupDirectory { get; set; } = "";
    public DateTimeOffset? LastBackupAt { get; set; }
    public long LastBackupRevision { get; set; }
    public string Theme { get; set; } = "Graphite";
    public string Layout { get; set; } = "rows";
    public string Language { get; set; } = "en";
    public List<string> DisabledModules { get; set; } = [];
}

public sealed class VaultDocument
{
    public int Version { get; set; } = 1;
    public long Revision { get; set; }
    public List<VaultItem> Items { get; set; } = [];
    public List<VaultFolder> Folders { get; set; } = [];
    public Preferences Settings { get; set; } = new();
    public BackupCapability? Backup { get; set; }
    public PinConfiguration? QuickUnlock { get; set; }
    public Dictionary<string, JsonElement> ExtensionSettings { get; set; } = [];
    public void Validate()
    {
        QuickUnlock?.Validate();
        if (Version != 1 || Items.Count > 100_000 || Folders.Count > 10_000) throw new InvalidDataException("Unsupported vault size or version.");
        if (Items.Select(x => x.Id).Distinct().Count() != Items.Count || Folders.Select(x => x.Id).Distinct().Count() != Folders.Count) throw new InvalidDataException("Duplicate identifiers.");
        var folders = Folders.ToDictionary(x => x.Id);
        foreach (var f in Folders)
        {
            var visited = new HashSet<Guid> { f.Id };
            var next = f.ParentId;
            while (next is Guid id)
            {
                if (!folders.TryGetValue(id, out var parent) || !visited.Add(id)) throw new InvalidDataException("Invalid folder tree.");
                next = parent.ParentId;
            }
        }
        foreach (var i in Items)
        {
            i.Advanced?.Validate();
            if (i.Id == Guid.Empty || string.IsNullOrWhiteSpace(i.ContentType) || i.ContentVersion < 1 || i.FolderId is Guid f && !folders.ContainsKey(f)) throw new InvalidDataException("Invalid entry.");
        }
        var s = Settings;
        if (s.SessionMinutes is < 1 or > 1440 || s.ClipboardSeconds is < 1 or > 3600 || s.TrashDays is < 1 or > 365 || s.BackupHours is < 1 or > 720 || s.BackupKeep is < 1 or > 100) throw new InvalidDataException("A setting is outside its supported range.");
    }
}

public static class ItemOperations
{
    // Keep the old storage identifier readable, but expose one protected value type.
    // Never derive a label from the value: previously unlabelled text may be private.
    public static void UpgradeTextEntries(VaultDocument doc)
    {
        foreach (var item in doc.Items.Where(i => i.ContentType == "sable.text"))
        {
            item.ContentType = "sable.secret"; item.IsSensitive = true;
            if (string.IsNullOrWhiteSpace(item.Label)) item.Label = "Saved secret";
        }
        doc.Settings.DisabledModules.RemoveAll(id => id == "sable.text");
    }
    public static VaultItem DuplicateDraft(VaultDocument doc, Guid id)
    {
        var source = doc.Items.Single(i => i.Id == id && i.DeletedAt is null);
        var copy = Codec.Clone(source);
        copy.Id = Guid.NewGuid(); copy.CreatedAt = null; copy.UpdatedAt = null; copy.DeletedAt = null;
        copy.Order = doc.Items.Where(i => i.FolderId == source.FolderId && i.DeletedAt is null).Select(i => i.Order).DefaultIfEmpty(-1).Max() + 1;
        var name = string.IsNullOrWhiteSpace(source.Label) ? "Entry" : source.Label;
        for (var number = 1; ; number++)
        {
            var suffix = number == 1 ? " (copy)" : " (copy " + number + ")";
            copy.Label = name[..Math.Min(name.Length, 300 - suffix.Length)] + suffix;
            if (!doc.Items.Any(i => i.FolderId == copy.FolderId && i.DeletedAt is null && i.Label == copy.Label)) break;
        }
        // Attachments are immutable blobs; shared references remain protected by orphan cleanup.
        return copy;
    }
    public static void Delete(VaultDocument doc, Guid id, DateTimeOffset now)
    {
        var item = doc.Items.Single(x => x.Id == id);
        var trash = item.IsSensitive ? doc.Settings.TrashSecrets : doc.Settings.TrashNormal;
        if (trash && item.DeletedAt is null) item.DeletedAt = now;
        else doc.Items.Remove(item);
    }
    public static int ExpireTrash(VaultDocument doc, DateTimeOffset now) => !doc.Settings.ExpireTrash ? 0 : doc.Items.RemoveAll(x => x.DeletedAt is { } deleted && deleted <= now.AddDays(-doc.Settings.TrashDays));
    public static IEnumerable<VaultItem> Search(VaultDocument doc, string query) => doc.Items.Where(x => x.DeletedAt is null && (x.Label.Contains(query, StringComparison.OrdinalIgnoreCase) || x.ContentType == "sable.text" && !x.IsSensitive && x.Field("text").Contains(query, StringComparison.OrdinalIgnoreCase)));
}
