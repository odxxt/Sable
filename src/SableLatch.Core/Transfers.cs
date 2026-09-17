using System.Security.Cryptography;

namespace SableLatch.Core;

// Deliberately separate from recovery backups: exports need only their export password.
public sealed class TransferArchive
{
    public string Format { get; set; } = "SableLatchExport/1";
    public byte[] Salt { get; set; } = [];
    public SealedBox Data { get; set; } = null!;
}

public sealed class TransferContents : IDisposable
{
    public List<VaultItem> Items { get; set; } = [];
    public List<VaultFolder> Folders { get; set; } = [];
    public Dictionary<Guid, byte[]> Attachments { get; set; } = [];
    public void Validate()
    {
        new VaultDocument { Items = Items, Folders = Folders }.Validate();
        var referenced = Items.SelectMany(i => i.Attachments).ToHashSet();
        if (!referenced.SetEquals(Attachments.Keys) || Attachments.Any(p => p.Key == Guid.Empty || p.Value.Length > 64 * 1024 * 1024))
            throw new InvalidDataException("Invalid export attachments.");
    }
    public void Dispose()
    {
        foreach (var bytes in Attachments.Values) CryptographicOperations.ZeroMemory(bytes);
        Attachments.Clear(); Items.Clear(); Folders.Clear();
    }
}

public static class Transfers
{
    public const int MaximumBytes = 256 * 1024 * 1024;
    private const string Context = "SableLatchExport/1/argon2id-m65536-t3-p1/aes256gcm";
    public static byte[] Create(VaultStore vault, string password)
    {
        Crypto.RequirePassword(password);
        if (!vault.IsUnlocked) throw new InvalidOperationException("Unlock the vault first.");
        using var contents = new TransferContents { Items = Codec.Clone(vault.Document!.Items), Folders = Codec.Clone(vault.Document.Folders) };
        long size = 0;
        foreach (var id in contents.Items.SelectMany(i => i.Attachments).Distinct())
        {
            var bytes = vault.ReadAttachment(id); contents.Attachments.Add(id, bytes); size += bytes.Length;
            if (size > MaximumBytes / 2) throw new InvalidDataException("This export exceeds the supported size.");
        }
        contents.Validate();
        var salt = Crypto.Random(16); var key = Crypto.Derive(password, salt); var plain = Codec.Encode(contents);
        try
        {
            if (plain.Length > MaximumBytes / 4 * 3 - 4096) throw new InvalidDataException("This export exceeds 256 MB.");
            var archive = Codec.Encode(new TransferArchive { Salt = salt, Data = Crypto.Seal(key, plain, Context) });
            if (archive.Length > MaximumBytes) throw new InvalidDataException("This export exceeds 256 MB.");
            return archive;
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }
    public static TransferContents Open(byte[] archive, string password)
    {
        if (archive.Length > MaximumBytes) throw new InvalidDataException("Choose an export smaller than 256 MB.");
        var envelope = Codec.Decode<TransferArchive>(archive);
        if (envelope.Format != "SableLatchExport/1" || envelope.Salt.Length != 16) throw new InvalidDataException("Unsupported export format.");
        // Fixed KDF cost: the file cannot request attacker-chosen memory or iteration counts.
        var key = Crypto.Derive(password, envelope.Salt); byte[]? plain = null;
        TransferContents? contents = null;
        try
        {
            plain = Crypto.Open(key, envelope.Data, Context); contents = Codec.Decode<TransferContents>(plain);
            contents.Validate(); return contents;
        }
        catch { contents?.Dispose(); throw; }
        finally { CryptographicOperations.ZeroMemory(key); if (plain is not null) CryptographicOperations.ZeroMemory(plain); }
    }
    public static void Import(VaultStore vault, TransferContents contents)
    {
        contents.Validate();
        if (!vault.IsUnlocked) throw new InvalidOperationException("Unlock the vault first.");
        var folders = Codec.Clone(contents.Folders); var items = Codec.Clone(contents.Items);
        var folderIds = folders.ToDictionary(f => f.Id, _ => Guid.NewGuid());
        var attachmentIds = new Dictionary<Guid, Guid>();
        if (vault.Document!.Items.Count + items.Count > 100_000 || vault.Document.Folders.Count + folders.Count > 10_000)
            throw new InvalidDataException("The combined vault exceeds supported limits.");
        foreach (var pair in contents.Attachments) attachmentIds.Add(pair.Key, vault.SaveAttachment(pair.Value));
        foreach (var f in folders) { f.Id = folderIds[f.Id]; if (f.ParentId is Guid parent) f.ParentId = folderIds[parent]; }
        foreach (var item in items)
        {
            item.Id = Guid.NewGuid(); item.Order += vault.Document.Items.Count;
            // New IDs have no local execution consent. Preserve the independent Verify each time preference.
            if (item.FolderId is Guid parent) item.FolderId = folderIds[parent];
            item.Attachments = item.Attachments.Select(id => attachmentIds[id]).ToList();
        }
        vault.Mutate(doc => { doc.Folders.AddRange(folders); doc.Items.AddRange(items); });
    }
    public static void Save(string path, byte[] archive) => VaultStore.AtomicWrite(path, archive);
}
