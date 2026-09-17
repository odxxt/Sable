using System.Security.Cryptography;

namespace SableLatch.Core;

// This configuration is stored only inside the encrypted vault document.
public sealed class PinConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public byte[] Salt { get; set; } = [];
    public byte[] Verifier { get; set; } = [];
    public static void RequirePin(string pin)
    {
        if (pin.Length is < 4 or > 12 || pin.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Use 4–12 digits for your PIN.");
    }
    public static PinConfiguration Create(string pin)
    {
        RequirePin(pin);
        var salt = Crypto.Random(16);
        return new() { Salt = salt, Verifier = Crypto.Derive(pin, salt) };
    }
    public void Validate()
    {
        if (Id == Guid.Empty || Salt.Length != 16 || Verifier.Length != 32)
            throw new InvalidDataException("Invalid PIN configuration.");
    }
}

// An encrypted, process-only key ticket. Never serialize this object or write it to disk.
// It is not a hardware-backed credential and does not protect a compromised process.
public sealed class QuickUnlockSession : IDisposable
{
    private readonly byte[] processSecret = Crypto.Random();
    private readonly byte[] salt;
    private readonly byte[] passwordSalt;
    private readonly SealedBox gate;
    private int failures;
    private readonly object sync = new();
    private bool disposed;
    public Guid VaultId { get; }
    public Guid ConfigurationId { get; }
    public bool Available { get { lock (sync) return !disposed; } }
    public int AttemptsRemaining { get { lock (sync) return disposed ? 0 : Math.Max(0, 5 - failures); } }
    private string Context => $"SableLatch/quick-unlock/{VaultId:D}/{ConfigurationId:D}";
    internal QuickUnlockSession(Guid vaultId, PinConfiguration configuration, byte[] dataKey, byte[] passwordSalt)
    {
        configuration.Validate(); VaultId = vaultId; ConfigurationId = configuration.Id;
        salt = configuration.Salt.ToArray();
        this.passwordSalt = passwordSalt.ToArray();
        var key = HMACSHA256.HashData(processSecret, configuration.Verifier);
        try { gate = Crypto.Seal(key, dataKey, Context); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    internal byte[] Open(string pin, Guid vaultId, byte[] currentPasswordSalt)
    {
        lock (sync) return OpenSerial(pin, vaultId, currentPasswordSalt);
    }
    private byte[] OpenSerial(string pin, Guid vaultId, byte[] currentPasswordSalt)
    {
        if (disposed || vaultId != VaultId) throw new CryptographicException("Use your full password.");
        if (!CryptographicOperations.FixedTimeEquals(passwordSalt, currentPasswordSalt)) { Dispose(); throw new CryptographicException("Password changed. Use your full password."); }
        byte[]? derived = null, key = null;
        try
        {
            PinConfiguration.RequirePin(pin);
            derived = Crypto.Derive(pin, salt);
            key = HMACSHA256.HashData(processSecret, derived);
            var dataKey = Crypto.Open(key, gate, Context); failures = 0; return dataKey;
        }
        catch
        {
            if (++failures >= 5) Dispose();
            throw;
        }
        finally
        {
            if (derived is not null) CryptographicOperations.ZeroMemory(derived);
            if (key is not null) CryptographicOperations.ZeroMemory(key);
        }
    }
    public void Dispose()
    {
        lock (sync)
        {
        disposed = true; CryptographicOperations.ZeroMemory(processSecret);
        CryptographicOperations.ZeroMemory(gate.Ciphertext);
        }
    }
}
