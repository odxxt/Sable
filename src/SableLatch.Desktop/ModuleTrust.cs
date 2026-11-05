using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace SableLatch.Desktop;

internal static class ModuleTrust
{
    public const string CatalogUrl = "https://raw.githubusercontent.com/odxxt/Sable/main/modules/catalog.json";
    public static byte[] Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SableLatch.Modules." + name) ?? throw new InvalidDataException("Module resource unavailable.");
        using var bytes = new MemoryStream(); stream.CopyTo(bytes); return bytes.ToArray();
    }
    public static bool Verify(byte[] bytes, byte[] signature)
    {
        try { using var key = ECDsa.Create(); key.ImportSubjectPublicKeyInfo(Resource("publisher.pub"), out _); return key.VerifyData(bytes, signature, HashAlgorithmName.SHA256); }
        catch (CryptographicException) { return false; }
    }
    public static async Task<byte[]> Download(string address, int limit, CancellationToken token)
    {
        address = address.Replace("https://raw.githubusercontent.com/odxxt/SableLatch/", "https://raw.githubusercontent.com/odxxt/Sable/", StringComparison.Ordinal);
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "raw.githubusercontent.com" || !uri.AbsolutePath.StartsWith("/odxxt/Sable/", StringComparison.Ordinal) || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new InvalidDataException("The catalog can download only from the official Sable GitHub repository.");
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Download exceeds its size limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(token); using var bytes = new MemoryStream();
        byte[] buffer = new byte[8192]; int count;
        while ((count = await stream.ReadAsync(buffer, token)) > 0) { if (bytes.Length + count > limit) throw new InvalidDataException("Download exceeds its size limit."); bytes.Write(buffer, 0, count); }
        return bytes.ToArray();
    }
    public static List<CatalogEntry> ReadCatalog(byte[] bytes)
    {
        var envelope = Codec.Decode<CatalogEnvelope>(bytes);
        var payload = Convert.FromBase64String(envelope.Payload);
        if (!Verify(payload, Convert.FromBase64String(envelope.Signature))) throw new InvalidDataException("Catalog signature could not be verified.");
        var catalog = Codec.Decode<List<CatalogEntry>>(payload);
        if (catalog.Count > 100 || catalog.Any(e => e.Name.Length > 80 || e.Description.Length > 500 || e.Sha256.Length != 64 || !e.Sha256.All(Uri.IsHexDigit))) throw new InvalidDataException("Invalid catalog.");
        return catalog;
    }
}
internal sealed record CatalogEnvelope(string Payload, string Signature);
internal sealed record CatalogEntry(string Name, string Description, string Url, string Sha256);
