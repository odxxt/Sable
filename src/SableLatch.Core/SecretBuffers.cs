using System.Security.Cryptography;
using System.Text.Json;

namespace SableLatch.Core;

public static class SecretBuffers
{
    // Avoid an extra immutable GetString copy during transfer. The unlocked document still
    // owns its JSON payload; this does not claim to erase that payload or another process's memory.
    public static byte[] Utf8(VaultItem item, string field, int maximum = 4096)
    {
        if (!item.Payload.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String) throw new InvalidDataException("A password is required.");
        using var encoded = new MemoryStream();
        byte[]? result = null;
        try
        {
            using (var writer = new Utf8JsonWriter(encoded)) value.WriteTo(writer);
            if (encoded.Length > maximum * 6L + 2) throw new InvalidDataException("Password is too long.");
            var reader = new Utf8JsonReader(encoded.GetBuffer().AsSpan(0, (int)encoded.Length)); reader.Read();
            result = new byte[maximum]; int count;
            try { count = reader.CopyString(result); }
            catch (ArgumentException) { throw new InvalidDataException("Password is too long."); }
            return result.AsSpan(0, count).ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded.GetBuffer());
            if (result is not null) CryptographicOperations.ZeroMemory(result);
        }
    }
}
