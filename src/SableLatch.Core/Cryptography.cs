using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SableLatch.Core;

public sealed record SealedBox(byte[] Nonce, byte[] Ciphertext, byte[] Tag);
public interface IDeviceProtector
{
    byte[] Protect(byte[] bytes);
    byte[] Unprotect(byte[] bytes);
}

public static class Crypto
{
    public static byte[] Random(int count = 32) => RandomNumberGenerator.GetBytes(count);
    public static byte[] Derive(string password, byte[] salt)
    {
        if (salt.Length != 16) throw new InvalidDataException("Invalid salt.");
        var input = Encoding.UTF8.GetBytes(password);
        try
        {
            using var kdf = new Argon2id(input) { Salt = salt, MemorySize = 65536, Iterations = 3, DegreeOfParallelism = 1 };
            return kdf.GetBytes(32);
        }
        finally { CryptographicOperations.ZeroMemory(input); }
    }
    public static SealedBox Seal(byte[] key, ReadOnlySpan<byte> plaintext, string context)
    {
        var nonce = Random(12); var cipher = new byte[plaintext.Length]; var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, cipher, tag, Encoding.UTF8.GetBytes(context));
        return new(nonce, cipher, tag);
    }
    public static byte[] Open(byte[] key, SealedBox box, string context)
    {
        if (box.Nonce.Length != 12 || box.Tag.Length != 16 || box.Ciphertext.Length > 256 * 1024 * 1024) throw new InvalidDataException("Invalid encrypted record.");
        var clear = new byte[box.Ciphertext.Length];
        try { using var aes = new AesGcm(key, 16); aes.Decrypt(box.Nonce, box.Ciphertext, box.Tag, clear, Encoding.UTF8.GetBytes(context)); return clear; }
        catch { CryptographicOperations.ZeroMemory(clear); throw; }
    }
    public static string NewRecoveryKey()
    {
        var raw = Random();
        try
        {
            var body = Convert.ToHexString(raw);
            var checksum = Convert.ToHexString(SHA256.HashData(raw))[..8];
            return "SL1-" + string.Join('-', Enumerable.Range(0, 8).Select(i => body.Substring(i * 8, 8))) + "-" + checksum;
        }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }
    public static byte[] ParseRecoveryKey(string formatted)
    {
        var text = string.Concat(formatted.Where(c => !char.IsWhiteSpace(c) && c != '-')).ToUpperInvariant();
        if (text.Length != 75 || !text.StartsWith("SL1", StringComparison.Ordinal)) throw new CryptographicException("Recovery key format is invalid.");
        byte[] raw;
        try { raw = Convert.FromHexString(text.Substring(3, 64)); } catch (FormatException) { throw new CryptographicException("Recovery key format is invalid."); }
        if (Convert.ToHexString(SHA256.HashData(raw))[..8] != text[^8..]) { CryptographicOperations.ZeroMemory(raw); throw new CryptographicException("Recovery key checksum does not match."); }
        return raw;
    }
    public static void RequirePassword(string password)
    {
        if (password.Length is < 12 or > 1024) throw new ArgumentException("Use a password or passphrase with 12–1,024 characters.");
    }
}
