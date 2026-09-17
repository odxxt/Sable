using System.Security.Cryptography;

namespace SableLatch.Core;

public sealed class VaultEnvelope
{
    public string Format { get; set; } = "SableLatch/1";
    public Guid VaultId { get; set; }
    public byte[] PasswordSalt { get; set; } = [];
    public byte[] DeviceGate { get; set; } = [];
    public SealedBox RecoveryGate { get; set; } = null!;
    public SealedBox Data { get; set; } = null!;
}

public sealed class VaultStore(string directory, IDeviceProtector device) : IAttachmentStore, IDisposable
{
    private readonly string directory = Path.GetFullPath(directory);
    private readonly IDeviceProtector device = device;
    private byte[]? dataKey;
    private byte[]? fileHash;
    private VaultEnvelope? envelope;
    public VaultDocument? Document { get; private set; }
    public string DirectoryPath => directory;
    public string FilePath => Path.Combine(directory, "vault.sable");
    public Guid VaultId => envelope?.VaultId ?? Guid.Empty;
    public bool Exists => File.Exists(FilePath);
    public bool IsUnlocked => dataKey is not null && Document is not null;
    public event EventHandler? Changed;
    public event EventHandler? Locked;
    private string Context(string purpose) => $"SableLatch/1/{VaultId:D}/{purpose}";

    public string Create(string password, string? recovery = null)
    {
        if (Exists || IsUnlocked) throw new InvalidOperationException("A vault already exists here.");
        Crypto.RequirePassword(password);
        recovery ??= Crypto.NewRecoveryKey();
        var recoveryKey = Crypto.ParseRecoveryKey(recovery);
        dataKey = Crypto.Random();
        envelope = new() { VaultId = Guid.NewGuid(), PasswordSalt = Crypto.Random(16) };
        try
        {
            envelope.RecoveryGate = Crypto.Seal(recoveryKey, dataKey, Context("recovery"));
            SetPasswordGate(envelope, password);
            var doc = new VaultDocument();
            Commit(doc);
            return recovery;
        }
        catch { Lock(); throw; }
        finally { CryptographicOperations.ZeroMemory(recoveryKey); }
    }
    public void Unlock(string password)
    {
        Lock();
        var candidate = ReadEnvelope();
        var gateBytes = device.Unprotect(candidate.DeviceGate);
        var key = Crypto.Derive(password, candidate.PasswordSalt);
        byte[]? dk = null;
        try
        {
            dk = Crypto.Open(key, Codec.Decode<SealedBox>(gateBytes), $"SableLatch/1/{candidate.VaultId:D}/password");
            Accept(candidate, dk); dk = null;
        }
        finally { CryptographicOperations.ZeroMemory(gateBytes); CryptographicOperations.ZeroMemory(key); if (dk is not null) CryptographicOperations.ZeroMemory(dk); }
    }
    public void Recover(string recovery, string newPassword)
    {
        Crypto.RequirePassword(newPassword);
        Lock();
        var candidate = ReadEnvelope(); var rk = Crypto.ParseRecoveryKey(recovery); byte[]? dk = null;
        try
        {
            dk = Crypto.Open(rk, candidate.RecoveryGate, $"SableLatch/1/{candidate.VaultId:D}/recovery");
            Accept(candidate, dk); dk = null;
            ChangePassword(newPassword);
        }
        catch { Lock(); throw; }
        finally { CryptographicOperations.ZeroMemory(rk); if (dk is not null) CryptographicOperations.ZeroMemory(dk); }
    }
    public QuickUnlockSession? CreateQuickUnlockSession()
    {
        RequireUnlocked();
        return Document!.QuickUnlock is { } config ? new(VaultId, config, dataKey!, envelope!.PasswordSalt) : null;
    }
    public void UnlockWithPin(string pin, QuickUnlockSession ticket)
    {
        Lock(); var candidate = ReadEnvelope(); byte[]? key = null;
        try
        {
            key = ticket.Open(pin, candidate.VaultId, candidate.PasswordSalt);
            Accept(candidate, key); key = null;
            if (Document!.QuickUnlock?.Id != ticket.ConfigurationId)
                throw new CryptographicException("PIN configuration changed. Use your full password.");
        }
        catch { Lock(); throw; }
        finally { if (key is not null) CryptographicOperations.ZeroMemory(key); }
    }
    private VaultEnvelope ReadEnvelope()
    {
        if (!Exists || new FileInfo(FilePath).Length > 256L * 1024 * 1024) throw new InvalidDataException("Vault is missing or too large.");
        var bytes = File.ReadAllBytes(FilePath);
        var e = Codec.Decode<VaultEnvelope>(bytes);
        if (e.Format != "SableLatch/1" || e.VaultId == Guid.Empty || e.PasswordSalt.Length != 16) throw new InvalidDataException("Unsupported vault format.");
        fileHash = SHA256.HashData(bytes);
        return e;
    }
    private void Accept(VaultEnvelope e, byte[] key)
    {
        var bytes = Crypto.Open(key, e.Data, $"SableLatch/1/{e.VaultId:D}/data");
        try
        {
            var doc = Codec.Decode<VaultDocument>(bytes); doc.Validate();
            envelope = e; dataKey = key; Document = doc;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private void SetPasswordGate(VaultEnvelope target, string password)
    {
        target.PasswordSalt = Crypto.Random(16);
        var key = Crypto.Derive(password, target.PasswordSalt);
        byte[]? gate = null;
        try { gate = Codec.Encode(Crypto.Seal(key, dataKey!, Context("password"))); target.DeviceGate = device.Protect(gate); }
        finally { CryptographicOperations.ZeroMemory(key); if (gate is not null) CryptographicOperations.ZeroMemory(gate); }
    }
    public void VerifyPassword(string password)
    {
        RequireUnlocked();
        var key = Crypto.Derive(password, envelope!.PasswordSalt); byte[]? bytes = null; byte[]? actual = null;
        try
        {
            bytes = device.Unprotect(envelope.DeviceGate);
            actual = Crypto.Open(key, Codec.Decode<SealedBox>(bytes), Context("password"));
            if (!CryptographicOperations.FixedTimeEquals(actual, dataKey!)) throw new CryptographicException("Incorrect password.");
        }
        finally { CryptographicOperations.ZeroMemory(key); if (bytes is not null) CryptographicOperations.ZeroMemory(bytes); if (actual is not null) CryptographicOperations.ZeroMemory(actual); }
    }
    public void ChangePassword(string password)
    {
        RequireUnlocked(); Crypto.RequirePassword(password);
        var candidate = Codec.Clone(envelope!);
        SetPasswordGate(candidate, password);
        Commit(Codec.Clone(Document!), candidate);
    }
    public string RotateRecoveryKey()
    {
        RequireUnlocked();
        var text = Crypto.NewRecoveryKey(); var key = Crypto.ParseRecoveryKey(text);
        try
        {
            var candidate = Codec.Clone(envelope!);
            candidate.RecoveryGate = Crypto.Seal(key, dataKey!, Context("recovery"));
            Commit(Codec.Clone(Document!), candidate);
            return text;
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    public void Mutate(Action<VaultDocument> mutation)
    {
        RequireUnlocked(); var next = Codec.Clone(Document!); mutation(next); Commit(next);
        Changed?.Invoke(this, EventArgs.Empty);
    }
    private void Commit(VaultDocument next, VaultEnvelope? candidate = null)
    {
        next.Validate(); next.Revision++;
        var bytes = Codec.Encode(next);
        try
        {
            candidate ??= Codec.Clone(envelope!);
            candidate.Data = Crypto.Seal(dataKey!, bytes, Context("data"));
            var encrypted = Codec.Encode(candidate);
            if (encrypted.Length > 256 * 1024 * 1024) throw new InvalidDataException("This vault exceeds the current 256 MB metadata limit.");
            Directory.CreateDirectory(directory);
            if (fileHash is not null && (!Exists || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(File.ReadAllBytes(FilePath)), fileHash))) throw new IOException("The vault changed outside this app. Lock and reopen before saving.");
            if (fileHash is null && Exists) throw new IOException("Refusing to overwrite an existing vault.");
            AtomicWrite(FilePath, encrypted);
            fileHash = SHA256.HashData(encrypted); envelope = candidate; Document = next;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    internal static void AtomicWrite(string path, byte[] encrypted)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { stream.Write(encrypted); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public Guid SaveAttachment(ReadOnlySpan<byte> bytes)
    {
        RequireUnlocked();
        if (bytes.Length > 64 * 1024 * 1024) throw new ArgumentException("Attachments are limited to 64 MB in this version.");
        var id = Guid.NewGuid(); Directory.CreateDirectory(Path.Combine(directory, "attachments"));
        AtomicWrite(AttachmentPath(id), Codec.Encode(Crypto.Seal(dataKey!, bytes, Context("attachment/" + id.ToString("D")))));
        return id;
    }
    public byte[] ReadAttachment(Guid id)
    {
        RequireUnlocked();
        var file = AttachmentPath(id);
        if (new FileInfo(file).Length > 100L * 1024 * 1024) throw new InvalidDataException("Attachment too large.");
        return Crypto.Open(dataKey!, Codec.Decode<SealedBox>(File.ReadAllBytes(file)), Context("attachment/" + id.ToString("D")));
    }
    private string AttachmentPath(Guid id) => Path.Combine(directory, "attachments", id.ToString("N") + ".sableblob");
    public void CleanOrphanAttachments()
    {
        RequireUnlocked(); var used = Document!.Items.SelectMany(x => x.Attachments).ToHashSet();
        var folder = Path.Combine(directory, "attachments"); if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder, "*.sableblob"))
            if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var id) && !used.Contains(id)) File.Delete(file);
    }
    internal RecoveryPackage RecoveryPackage()
    {
        RequireUnlocked();
        return new() { VaultId = VaultId, RecoveryGate = Codec.Clone(envelope!.RecoveryGate), Data = Codec.Clone(envelope.Data), Attachments = Document!.Items.SelectMany(x => x.Attachments).Distinct().ToDictionary(x => x, x => File.ReadAllBytes(AttachmentPath(x))) };
    }
    public static void RestorePackage(RecoveryPackage package, string recovery, string newPassword, string directory, IDeviceProtector device)
    {
        Crypto.RequirePassword(newPassword);
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()) throw new IOException("Restore into an empty folder to preserve any existing vault.");
        using var store = new VaultStore(directory, device);
        store.Restore(package, recovery, newPassword);
    }
    public void Restore(RecoveryPackage package, string recovery, string newPassword)
    {
        if (Exists || IsUnlocked) throw new IOException("A vault already exists here. Existing vaults are preserved.");
        Crypto.RequirePassword(newPassword);
        var rk = Crypto.ParseRecoveryKey(recovery); byte[]? dk = null;
        var store = this;
        try
        {
            dk = Crypto.Open(rk, package.RecoveryGate, $"SableLatch/1/{package.VaultId:D}/recovery");
            var candidate = new VaultEnvelope { VaultId = package.VaultId, RecoveryGate = package.RecoveryGate, Data = package.Data };
            store.Accept(candidate, dk); dk = null;
            // Authenticate every attachment before creating any destination files.
            foreach (var id in store.Document!.Items.SelectMany(x => x.Attachments).Distinct())
            {
                if (File.Exists(store.AttachmentPath(id))) throw new IOException("An attachment already exists at the destination.");
                if (!package.Attachments.TryGetValue(id, out var blob)) throw new InvalidDataException("Backup is missing an attachment.");
                var plain = Crypto.Open(store.dataKey!, Codec.Decode<SealedBox>(blob), store.Context("attachment/" + id.ToString("D")));
                CryptographicOperations.ZeroMemory(plain);
            }
            store.SetPasswordGate(candidate, newPassword);
            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(Path.Combine(directory, "attachments"));
            foreach (var id in store.Document.Items.SelectMany(x => x.Attachments).Distinct()) AtomicWrite(store.AttachmentPath(id), package.Attachments[id]);
            store.Commit(store.Document, candidate);
        }
        catch { Lock(); throw; }
        finally { CryptographicOperations.ZeroMemory(rk); if (dk is not null) CryptographicOperations.ZeroMemory(dk); }
    }
    private void RequireUnlocked() { if (!IsUnlocked) throw new InvalidOperationException("Unlock the vault first."); }
    public void Lock()
    {
        if (Document?.QuickUnlock is { } pin) CryptographicOperations.ZeroMemory(pin.Verifier);
        if (dataKey is not null) CryptographicOperations.ZeroMemory(dataKey);
        dataKey = null; Document = null; envelope = null; fileHash = null;
        Locked?.Invoke(this, EventArgs.Empty);
    }
    public void Dispose() => Lock();
}
