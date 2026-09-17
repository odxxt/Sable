using System.Security.Cryptography;

namespace SableLatch.Core;

public sealed class BackupCapability
{
    public byte[] PublicKey { get; set; } = [];
    public byte[] PasswordSalt { get; set; } = [];
    public SealedBox PrivateKey { get; set; } = null!;
}
public sealed class RecoveryPackage
{
    public Guid VaultId { get; set; }
    public SealedBox RecoveryGate { get; set; } = null!;
    public SealedBox Data { get; set; } = null!;
    public Dictionary<Guid, byte[]> Attachments { get; set; } = [];
}
public sealed class BackupArchive
{
    public string Format { get; set; } = "SableLatchBackup/1";
    public BackupCapability Capability { get; set; } = null!;
    public byte[] WrappedKey { get; set; } = [];
    public SealedBox Archive { get; set; } = null!;
}
public static class Backups
{
    public static BackupCapability Configure(string password)
    {
        Crypto.RequirePassword(password);
        using var rsa = RSA.Create(3072);
        var salt = Crypto.Random(16); var key = Crypto.Derive(password, salt); var privateKey = rsa.ExportPkcs8PrivateKey();
        try { return new() { PasswordSalt = salt, PublicKey = rsa.ExportSubjectPublicKeyInfo(), PrivateKey = Crypto.Seal(key, privateKey, "SableLatchBackup/1/private") }; }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(privateKey); }
    }
    public static byte[] Create(VaultStore store)
    {
        var capability = store.Document?.Backup ?? throw new InvalidOperationException("Set a backup password first.");
        using var rsa = RSA.Create(); rsa.ImportSubjectPublicKeyInfo(capability.PublicKey, out _);
        var key = Crypto.Random(); var inner = Codec.Encode(store.RecoveryPackage());
        try
        {
            var result = Codec.Encode(new BackupArchive { Capability = capability, WrappedKey = rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA256), Archive = Crypto.Seal(key, inner, "SableLatchBackup/1/archive") });
            if (result.Length > 256 * 1024 * 1024) throw new InvalidDataException("This vault exceeds the current 256 MB backup archive limit.");
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(inner); }
    }
    public static RecoveryPackage Open(byte[] archive, string password)
    {
        if (archive.Length > 256 * 1024 * 1024) throw new InvalidDataException("Backup exceeds this version's 256 MB limit.");
        var b = Codec.Decode<BackupArchive>(archive);
        if (b.Format != "SableLatchBackup/1") throw new InvalidDataException("Unsupported backup format.");
        var key = Crypto.Derive(password, b.Capability.PasswordSalt); byte[]? pk = null; byte[]? archiveKey = null; byte[]? clear = null;
        try
        {
            pk = Crypto.Open(key, b.Capability.PrivateKey, "SableLatchBackup/1/private");
            using var rsa = RSA.Create(); rsa.ImportPkcs8PrivateKey(pk, out _);
            archiveKey = rsa.Decrypt(b.WrappedKey, RSAEncryptionPadding.OaepSHA256);
            clear = Crypto.Open(archiveKey, b.Archive, "SableLatchBackup/1/archive");
            return Codec.Decode<RecoveryPackage>(clear);
        }
        finally { CryptographicOperations.ZeroMemory(key); if (pk is not null) CryptographicOperations.ZeroMemory(pk); if (archiveKey is not null) CryptographicOperations.ZeroMemory(archiveKey); if (clear is not null) CryptographicOperations.ZeroMemory(clear); }
    }
    public static string Save(VaultStore store, string directory, int keep)
    {
        if (keep is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(keep));
        Directory.CreateDirectory(directory);
        var prefix = "SableLatch-" + store.VaultId.ToString("N") + "-";
        var path = Path.Combine(directory, prefix + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".sablebackup");
        VaultStore.AtomicWrite(path, Create(store));
        // Retention only touches this vault's generated archives, after successful write.
        foreach (var old in Directory.EnumerateFiles(directory, prefix + "*.sablebackup").OrderByDescending(Path.GetFileName).Skip(keep)) File.Delete(old);
        return path;
    }
}
