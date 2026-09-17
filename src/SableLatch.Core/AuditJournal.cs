using System.Security.Cryptography;

namespace SableLatch.Core;

// Only fixed event codes are accepted: no credentials, labels, paths or exception messages.
public enum AuditEvent
{
    Started, PasswordUnlocked, PinUnlocked, PasswordRejected, PinRejected, PinExhausted,
    Locked, Changed, PasswordChanged, PinChanged, RecoveryChanged, BackupSaved,
    OperationFailed, ReportExported, Exited, ExportSaved, Imported, EntryActionStarted
}
public sealed record AuditRecord(DateTimeOffset At, AuditEvent Event);
public sealed class AuditJournal(string directory, IDeviceProtector protector)
{
    public const int Capacity = 256;
    public string FilePath { get; } = Path.Combine(Path.GetFullPath(directory), "audit.sablelog");
    public IReadOnlyList<AuditRecord> Read()
    {
        if (!File.Exists(FilePath)) return [];
        if (new FileInfo(FilePath).Length > 128 * 1024) throw new InvalidDataException("Audit history is too large.");
        var bytes = protector.Unprotect(File.ReadAllBytes(FilePath));
        try
        {
            var records = Codec.Decode<List<AuditRecord>>(bytes);
            if (records.Count > Capacity || records.Any(r => !Enum.IsDefined(r.Event))) throw new InvalidDataException("Invalid audit history.");
            return records;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public void Record(AuditEvent code, DateTimeOffset? at = null)
    {
        if (!Enum.IsDefined(code)) throw new ArgumentException("Unknown audit event.");
        var records = Read().TakeLast(Capacity - 1).Append(new AuditRecord(at ?? DateTimeOffset.UtcNow, code)).ToList();
        var bytes = Codec.Encode(records);
        try
        {
            var encrypted = protector.Protect(bytes);
            if (encrypted.Length > 128 * 1024) throw new InvalidDataException("Audit history is too large.");
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            VaultStore.AtomicWrite(FilePath, encrypted);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}

public static class EntryMetadata
{
    public static void MarkSaved(VaultItem item, VaultItem? previous, DateTimeOffset at)
    {
        // A legacy item's original creation time is unknown; never fabricate it.
        item.CreatedAt = previous is null ? at : previous.CreatedAt;
        item.UpdatedAt = at;
    }
}
