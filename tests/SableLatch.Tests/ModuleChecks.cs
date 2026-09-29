using SableLatch.Core;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

internal static class ModuleChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject, string root)
    {
        ModuleManifest Sample() => new() { Id = "example.note", Name = "Note", Author = "Test", Fields = [new() { Key = "body", Label = "Body" }, new() { Key = "password", Label = "Password", Kind = "secret" }], Actions = [new() { Field = "body", Label = "Copy note" }] };
        byte[] Zip(params (string Name, byte[] Bytes)[] files)
        {
            using var bytes = new MemoryStream();
            using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, true)) foreach (var file in files) { using var stream = archive.CreateEntry(file.Name).Open(); stream.Write(file.Bytes); }
            return bytes.ToArray();
        }
        var manifest = Sample(); var json = Codec.Encode(manifest);
        var package = ModulePackages.Read(Zip(("manifest.json", json)));
        check(!package.Official && package.Manifest.ContentType == "module.example.note", "Unsigned declarative module has scoped type and community status");
        foreach (var path in new[] { "../escape", "/absolute", "C:/file", "manifest.json:stream", "plugin.dll", "plugin.exe", "ui.xaml", "script.ps1", "folder/manifest.json", "CON" })
            reject(() => ModulePackages.Read(Zip(("manifest.json", json), (path, [1]))), "Module rejects unsafe/executable archive member " + path);
        reject(() => ModulePackages.Read(Zip(("manifest.json", json), ("manifest.json", json))), "Duplicate ZIP entries rejected");
        reject(() => ModulePackages.Read(Zip(("manifest.json", new byte[70000]))), "Expanded ZIP size bounded");
        reject(() => ModulePackages.Read(new byte[ModulePackages.MaxBytes + 1]), "Compressed package size bounded");
        reject(() => ModulePackages.Read(Zip(("manifest.json", Encoding.UTF8.GetBytes("{\"id\":\"a\",\"id\":\"b\"}")))), "Duplicate JSON properties rejected");
        var secretCopy = Sample(); secretCopy.Actions[0].Field = "password";
        reject(() => secretCopy.Validate(), "Module actions cannot expose a secret field");
        var duplicateLabel = Sample(); duplicateLabel.Actions.Add(new() { Id = "another", Label = "Copy note", Field = "body" });
        reject(() => duplicateLabel.Validate(), "Ambiguous duplicate action captions rejected");
        var script = Sample(); script.Actions[0].Kind = "powershell";
        reject(() => script.Validate(), "Modules cannot request arbitrary scripts");
        var unknown = Sample(); unknown.ApiVersion = 99;
        reject(() => unknown.Validate(), "Unsupported module API rejected");
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        bool Verify(byte[] data, byte[] signature) => signer.VerifyData(data, signature, HashAlgorithmName.SHA256);
        var official = Sample(); official.Id = "sablelatch.test"; var officialJson = Codec.Encode(official);
        reject(() => ModulePackages.Read(Zip(("manifest.json", officialJson))), "Unsigned modules cannot impersonate official namespace");
        var signed = Zip(("manifest.json", officialJson), ("module.sig", signer.SignData(officialJson, HashAlgorithmName.SHA256)));
        check(ModulePackages.Read(signed, Verify).Official, "Official signature verified using pinned publisher key");
        reject(() => ModulePackages.Read(signed, (_, _) => false), "Untrusted official signature rejected");
        official.Description = "Tampered";
        reject(() => ModulePackages.Read(Zip(("manifest.json", Codec.Encode(official)), ("module.sig", signer.SignData(officialJson, HashAlgorithmName.SHA256))), Verify), "Tampered official manifest rejected");
        var store = new ModuleStore(Path.Combine(root, "modules-test")); store.Install(package);
        check(store.Load(out var failures).Count == 1 && failures == 0, "Module installed atomically and reloaded");
        var changed = Sample(); changed.Version = "1.1.0"; changed.Fields[1].Kind = "text";
        reject(() => store.Install(ModulePackages.Read(Zip(("manifest.json", Codec.Encode(changed))))), "Update cannot downgrade secret field to ordinary text");
        var publisher = Sample(); publisher.Author = "Someone else";
        reject(() => store.Install(ModulePackages.Read(Zip(("manifest.json", Codec.Encode(publisher))))), "Update requires stable publisher identity");
        store.Remove(package); check(store.Load(out _).Count == 0, "Uninstall removes only module package");
        var provider = new DeclarativeProvider(package);
        var entry = new VaultItem { ContentType = manifest.ContentType, Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { body = "ordinary", password = "secret-canary" }) };
        check(Encoding.UTF8.GetString(provider.Copy(entry, "Copy note").Data) == "ordinary", "Host copy reads only declared ordinary field");
        reject(() => provider.Copy(entry, "password"), "Module action cannot request undeclared secret copying");
        var utf8Secret = SecretBuffers.Utf8(entry, "password");
        check(Encoding.UTF8.GetString(utf8Secret) == "secret-canary", "Secret handoff reads UTF-8 without an immutable plaintext string copy"); CryptographicOperations.ZeroMemory(utf8Secret);
        reject(() => SecretBuffers.Utf8(entry, "password", 2), "Secret handoff buffer is bounded");
        entry.ModuleSchema = manifest.SchemaFingerprint();
        check(Codec.Clone(entry).ModuleSchema == entry.ModuleSchema, "Module schema binding survives vault serialization");
        var profile = new PuttyProfile("example.test", "operator"); var arguments = profile.Arguments("SableLatch-" + new string('A', 64));
        check(arguments.Contains("-pwfile") && !arguments.Contains("-pw") && arguments.Contains("-noagent") && arguments.Contains("-restrict-acl"), "PuTTY uses pipe password and restricted launch arguments");
        reject(() => new PuttyProfile("-proxycmd", "user").Validate(), "PuTTY host cannot inject an option");
        reject(() => new PuttyProfile("example.test", "user\ncommand").Validate(), "PuTTY username rejects control characters");
        reject(() => new PuttyProfile("example.test", "user", 65536).Validate(), "PuTTY port bounded");
        reject(() => profile.Arguments("predictable-pipe"), "Only random host-created pipe names accepted");
        check(profile.Fingerprint("module", "binary", Guid.Empty) != (profile with { Host = "other.test" }).Fingerprint("module", "binary", Guid.Empty), "Connection approval binds destination");
        check(profile.Fingerprint("module", "binary", Guid.Empty) != profile.Fingerprint("module", "new binary", Guid.Empty), "Connection approval binds PuTTY binary");
        var rdp = new RdpProfile("server.example", "DOMAIN\\operator"); rdp.Validate();
        check(rdp.Port == 3389, "RDP profile defaults to standard port");
        var nativeSettings = RdpConnectionFile.Create(rdp, false);
        var resourceOverrides = new[] { "redirectdrives:", "drivestoredirect:", "redirectprinters:", "redirectcomports:", "redirectsmartcards:", "devicestoredirect:", "usbdevicestoredirect:", "audiocapturemode:", "audiomode:", "autoreconnection enabled:", "bitmapcachepersistenable:", "gatewayusagemethod:", "gatewayprofileusagemethod:" };
        check(resourceOverrides.All(key => !nativeSettings.Contains(key)), "RDP does not impose extra resource, audio, reconnection, cache or gateway restrictions");
        var legacyClipboard = new VaultItem { Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { host = "fixture.test", username = "user", clipboard = "false" }) };
        check(RdpConnectionFile.Create(RdpProfile.From(legacyClipboard), false).Contains("redirectclipboard:i:1\r\n"), "Existing RDP entries no longer disable clipboard through a legacy field");
        check(nativeSettings.Contains("username:s:DOMAIN\\operator\r\n") && nativeSettings.Contains("full address:s:server.example:3389\r\n"), "Native Remote Desktop settings carry the selected destination and username");
        check(!nativeSettings.Contains("password", StringComparison.OrdinalIgnoreCase) && nativeSettings.Contains("authentication level:i:2\r\n") && nativeSettings.Contains("enablecredsspsupport:i:1\r\n") && !nativeSettings.Contains("authentication level:i:0") && nativeSettings.Contains("redirectclipboard:i:1\r\n"), "Native Remote Desktop offers certificate review while retaining CredSSP, protected-password separation and enabled clipboard");
        check(RdpConnectionFile.Create(rdp with { Host = "::1", Port = 3390 }, true).Contains("full address:s:[::1]:3390\r\n"), "Native Remote Desktop formats IPv6 hosts with a separate port");
        check(RdpConnectionFile.Create(rdp, true).Contains("prompt for credentials:i:1\r\n"), "Native Remote Desktop supports explicit interactive credential prompting");
        foreach (var host in new[] { "https://server", "server:3389", "server\nother", "", "-argument" }) reject(() => (rdp with { Host = host }).Validate(), "RDP rejects malformed destination: " + (host.Length == 0 ? "<empty>" : host).Replace('\n', ' '));
        reject(() => (rdp with { Port = 0 }).Validate(), "RDP port zero rejected");
        check(rdp.FullScreen && nativeSettings.Contains("screen mode id:i:2\r\n") && !nativeSettings.Contains("desktopwidth") && !nativeSettings.Contains("desktopheight") && !nativeSettings.Contains("smart sizing") && !nativeSettings.Contains("winposstr") && !nativeSettings.Contains("dynamic resolution"), "RDP uses native full-screen defaults without fixed dimensions, forced scaling or unsupported dynamic resolution");
        check(RdpConnectionFile.Create(rdp with { FullScreen = false }, false).Contains("screen mode id:i:1\r\n"), "RDP can start in a native window");
        var legacyRdp = new VaultItem { Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { host = "fixture.test", username = "user", width = "1280", height = "800" }) };
        check(RdpProfile.From(legacyRdp).FullScreen && !RdpConnectionFile.Create(RdpProfile.From(legacyRdp), false).Contains("1280"), "Existing RDP entries automatically stop enforcing their old desktop dimensions");
        var windowedRdp = new VaultItem { Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { host = "fixture.test", username = "user", fullScreen = "false" }) };
        check(!RdpProfile.From(Codec.Clone(windowedRdp)).FullScreen, "RDP windowed preference survives document serialization");
        reject(() => (rdp with { Domain = "OTHER" }).Validate(), "Conflicting RDP domain forms rejected");
        reject(() => (rdp with { Username = "user\npassword" }).Validate(), "RDP username control characters rejected");
        check(rdp.Fingerprint("module", "helper", Guid.Empty) != (rdp with { FullScreen = false }).Fingerprint("module", "helper", Guid.Empty), "RDP approval binds display preference");
        check(rdp.Fingerprint("module", "helper", Guid.Empty) != (rdp with { Host = "other.example" }).Fingerprint("module", "helper", Guid.Empty), "RDP approval binds destination");
        check(rdp.Fingerprint("module", "helper", Guid.Empty) != rdp.Fingerprint("module", "new-helper", Guid.Empty), "RDP approval binds helper version");
        var rdpManifest = new ModuleManifest { Id = "example.rdp", Name = "Remote Desktop", Author = "Test", Handler = "rdp", Icon = "desktop" }; rdpManifest.Validate();
        check(new DeclarativeProvider(new(rdpManifest, [], "", false)).Descriptor.Sensitive, "RDP module is sensitive by default");
        var rdpProvider = new DeclarativeProvider(new(rdpManifest, [], "", false));
        var rdpEntry = new VaultItem { ContentType = rdpManifest.ContentType, Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { password = "fictional-rdp-password", host = "fixture.test", username = "user", copyOnLaunch = "true" }) };
        var rdpCopy = rdpProvider.Copy(rdpEntry, "Password");
        check(rdpProvider.CopyActions.SequenceEqual(new[] { "Password" }) && Encoding.UTF8.GetString(rdpCopy.Data) == "fictional-rdp-password" && rdpCopy.Sensitive && !rdpCopy.KeepPanelOpen, "Host-owned RDP copy returns only the password using sensitive clipboard policy");
        CryptographicOperations.ZeroMemory(rdpCopy.Data);
        check(RdpProfile.From(rdpEntry).CopyOnLaunch && Codec.Clone(rdpEntry).Field("copyOnLaunch") == "true", "RDP launch-and-copy preference survives encrypted document serialization");
        reject(() => rdpProvider.Copy(rdpEntry, "Other"), "RDP cannot copy an undeclared field");
        rdpManifest.Fields.Add(new() { Key = "password", Label = "Override", Kind = "text" });
        reject(rdpManifest.Validate, "RDP manifest cannot override host-owned secret fields");
    }
}
