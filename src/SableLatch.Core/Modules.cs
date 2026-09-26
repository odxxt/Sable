using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SableLatch.Core;

// API 1 is deliberately data-only. Never resolve CLR types, XAML, scripts or assembly paths from a package.
public sealed class ModuleManifest
{
    public int ApiVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "note";
    public string Handler { get; set; } = "fields";
    public List<ModuleField> Fields { get; set; } = [];
    public List<ModuleAction> Actions { get; set; } = [];
    public string ContentType => "module." + Id;
    public void Validate()
    {
        if (ApiVersion != 1 || !Regex.IsMatch(Id, @"^[a-z][a-z0-9-]{1,30}(\.[a-z][a-z0-9-]{0,30}){1,4}$") || Id.Length > 100)
            throw new InvalidDataException("Unsupported module API or invalid module ID.");
        if (!System.Version.TryParse(Version, out var v) || v.Major < 1 || Version.Length > 30) throw new InvalidDataException("Use a module version such as 1.0.0.");
        if (Name.Length is < 1 or > 48 || Author.Length is < 1 or > 80 || Description.Length > 500 || new[] { Name, Author, Description }.Any(s => s.Any(c => char.IsControl(c) && c != '\n')))
            throw new InvalidDataException("Invalid module name, author or description.");
        if (Icon is not ("note" or "terminal" or "mail" or "lock" or "desktop") || Handler is not ("fields" or "putty" or "rdp")) throw new InvalidDataException("Unsupported module icon or action handler.");
        if (Fields.Count > 16 || Actions.Count > 6 || Fields.Select(f => f.Key).Distinct().Count() != Fields.Count || Actions.Select(a => a.Id).Distinct().Count() != Actions.Count || Actions.Select(a => a.Label).Distinct().Count() != Actions.Count)
            throw new InvalidDataException("Too many or duplicate module fields/actions.");
        if (Handler is ("putty" or "rdp") && (Fields.Count != 0 || Actions.Count != 0)) throw new InvalidDataException("Connection modules use a protected host-owned editor and connection action.");
        if (Handler == "fields" && Fields.Count == 0) throw new InvalidDataException("Define at least one field.");
        foreach (var field in Fields)
            if (!Regex.IsMatch(field.Key, "^[a-z][a-z0-9_]{0,31}$") || field.Label.Length is < 1 or > 60 || field.Label.Any(char.IsControl) || field.Kind is not ("text" or "multiline" or "secret"))
                throw new InvalidDataException("Invalid field definition.");
        foreach (var action in Actions)
            if (!Regex.IsMatch(action.Id, "^[a-z][a-z0-9_-]{0,31}$") || action.Label.Length is < 1 or > 40 || action.Label.Any(char.IsControl) || action.Kind != "copy" || !Fields.Any(f => f.Key == action.Field && f.Kind != "secret"))
                throw new InvalidDataException("API 1 supports host-managed copying of non-secret fields only.");
    }
    public string SchemaFingerprint() => Convert.ToHexString(SHA256.HashData(Codec.Encode(new { Handler, Fields = Fields.Select(f => new { f.Key, f.Kind }).OrderBy(f => f.Key) })));
}
public sealed class ModuleField
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Kind { get; set; } = "text";
    public bool Required { get; set; } = true;
}
public sealed class ModuleAction
{
    public string Id { get; set; } = "copy";
    public string Label { get; set; } = "Copy";
    public string Kind { get; set; } = "copy";
    public string Field { get; set; } = "text";
}
public sealed record ModulePackage(ModuleManifest Manifest, byte[] Archive, string Fingerprint, bool Official);

public static class ModulePackages
{
    public const int MaxBytes = 256 * 1024;
    private static readonly JsonSerializerOptions StrictJson = new(Codec.Options) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public static ModulePackage Read(byte[] archive, Func<byte[], byte[], bool>? verifyOfficial = null)
    {
        if (archive.Length is 0 or > MaxBytes) throw new InvalidDataException("Module ZIPs must be under 256 KB.");
        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
        if (zip.Entries.Count is < 1 or > 4) throw new InvalidDataException("A module must contain manifest.json and optional README.md, LICENSE and module.sig.");
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            // A flat allowlist also excludes paths, devices, ADS, symlinks and executable payloads.
            if (entry.FullName is not ("manifest.json" or "README.md" or "LICENSE" or "module.sig") || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || !files.TryAdd(entry.FullName, []))
                throw new InvalidDataException("Unexpected, duplicate or unsafe file in module ZIP.");
            total += entry.Length;
            if (total > MaxBytes || entry.Length > 64 * 1024) throw new InvalidDataException("Module contents exceed the size limit.");
            using var stream = entry.Open(); using var bytes = new MemoryStream();
            var buffer = new byte[4096]; int count;
            while ((count = stream.Read(buffer)) > 0) { if (bytes.Length + count > 64 * 1024) throw new InvalidDataException("Expanded module file is too large."); bytes.Write(buffer, 0, count); }
            files[entry.FullName] = bytes.ToArray();
        }
        if (!files.TryGetValue("manifest.json", out var manifestBytes)) throw new InvalidDataException("manifest.json is missing.");
        using (var document = JsonDocument.Parse(manifestBytes)) RejectDuplicateProperties(document.RootElement);
        var manifest = JsonSerializer.Deserialize<ModuleManifest>(manifestBytes, StrictJson) ?? throw new InvalidDataException("Invalid manifest.");
        manifest.Validate();
        var official = files.TryGetValue("module.sig", out var signature) && verifyOfficial?.Invoke(manifestBytes, signature) == true;
        if (files.ContainsKey("module.sig") && !official) throw new InvalidDataException("This module's official signature cannot be verified.");
        if (manifest.Id.StartsWith("sablelatch.", StringComparison.Ordinal) && !official) throw new InvalidDataException("The sablelatch namespace is reserved for verified official modules.");
        return new(manifest, archive.ToArray(), Convert.ToHexString(SHA256.HashData(manifestBytes)), official);
    }
    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in element.EnumerateObject()) { if (!keys.Add(p.Name)) throw new InvalidDataException("Duplicate JSON property."); RejectDuplicateProperties(p.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var value in element.EnumerateArray()) RejectDuplicateProperties(value);
    }
}

public sealed class ModuleStore(string directory, Func<byte[], byte[], bool>? verifyOfficial = null)
{
    public IReadOnlyList<ModulePackage> Load(out int rejected)
    {
        var loaded = new List<ModulePackage>(); rejected = 0;
        if (!Directory.Exists(directory)) return loaded;
        foreach (var path in Directory.EnumerateFiles(directory, "*.zip").Take(101))
        {
            try
            {
                if (loaded.Count >= 100 || new FileInfo(path).Length > ModulePackages.MaxBytes) throw new InvalidDataException();
                var package = ModulePackages.Read(File.ReadAllBytes(path), verifyOfficial);
                if (Path.GetFileName(path) != package.Manifest.Id + ".zip" || loaded.Any(p => p.Manifest.Id == package.Manifest.Id)) throw new InvalidDataException();
                loaded.Add(package);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NullReferenceException or CryptographicException) { rejected++; }
        }
        return loaded;
    }
    public void Install(ModulePackage package)
    {
        package = ModulePackages.Read(package.Archive, verifyOfficial);
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, package.Manifest.Id + ".zip");
        if (File.Exists(target))
        {
            var previous = ModulePackages.Read(File.ReadAllBytes(target), verifyOfficial);
            if (previous.Official != package.Official || previous.Manifest.Author != package.Manifest.Author) throw new InvalidDataException("Publisher identity changed. Remove the old module explicitly before replacing it.");
            if (System.Version.Parse(package.Manifest.Version) < System.Version.Parse(previous.Manifest.Version)) throw new InvalidDataException("Module downgrades are not supported.");
            if (previous.Manifest.SchemaFingerprint() != package.Manifest.SchemaFingerprint()) throw new InvalidDataException("This update changes stored field security or schema. API 1 requires a new module ID for schema changes.");
        }
        else if (Directory.EnumerateFiles(directory, "*.zip").Count() >= 100) throw new InvalidDataException("At most 100 modules may be installed.");
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, package.Archive); File.Move(temp, target, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Remove(ModulePackage package)
    {
        package.Manifest.Validate();
        File.Delete(Path.Combine(directory, package.Manifest.Id + ".zip"));
    }
}

// This is trusted host code interpreting data, not a provider supplied by a module.
public sealed class DeclarativeProvider(ModulePackage package) : IContentProvider
{
    public ModulePackage Package => package;
    public ContentDescriptor Descriptor => new(package.Manifest.ContentType, 1, package.Manifest.Name,
        package.Manifest.Icon switch { "terminal" => "\uE756", "mail" => "\uE715", "lock" => "\uE72E", "desktop" => "\uE7F4", _ => "\uE70B" },
        package.Manifest.Handler is ("putty" or "rdp") || package.Manifest.Fields.Any(f => f.Kind == "secret"));
    public IReadOnlyList<EditorField> EditorFields => package.Manifest.Fields.Select(f => new EditorField(f.Key, f.Label, f.Kind switch { "secret" => EditorFieldKind.Secret, "multiline" => EditorFieldKind.Multiline, _ => EditorFieldKind.Text }, f.Required)).ToArray();
    public IReadOnlyList<string> CopyActions => package.Manifest.Handler == "rdp" ? ["Password"] : package.Manifest.Actions.Select(a => a.Label).ToArray();
    public CopyValue Copy(VaultItem item, string action)
    {
        // Explicit host-owned RDP capability, never a module-defined secret-field action.
        if (package.Manifest.Handler == "rdp" && action == "Password")
            return new("text/plain", SecretBuffers.Utf8(item, "password"), true, false);
        var definition = package.Manifest.Actions.Single(a => a.Label == action);
        if (!package.Manifest.Fields.Any(f => f.Key == definition.Field && f.Kind != "secret")) throw new InvalidOperationException("Secret fields cannot be read by module actions.");
        return new("text/plain", Encoding.UTF8.GetBytes(item.Field(definition.Field)), item.IsSensitive, false);
    }
}
