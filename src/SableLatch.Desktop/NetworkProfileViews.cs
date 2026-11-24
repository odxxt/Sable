using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text.Json;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private static readonly string[] NetworkModes = ["Keep current", "Automatic (DHCP)", "Manual"];
    private bool applyingNetworkProfile;
#if DEBUG
    private Func<NetworkAdapterChoice[]>? previewNetworkAdapters;
    private Func<NetworkProfile, Task<int>>? previewNetworkApply;
#endif

    private NetworkAdapterChoice[] NetworkAdapters()
    {
#if DEBUG
        if (App.PreviewMode) return previewNetworkAdapters?.Invoke() ?? [];
#endif
        return NetworkProfileBroker.ListAdapters();
    }

    private FrameworkElement BuildNetworkProfileFields(DeclarativeProvider provider, VaultItem? existing, Action<VaultItem> save)
    {
        var stack = new StackPanel();
        var name = Input(stack, "Name", existing?.Label ?? editorSharedLabel);
        name.MaxLength = 300;
        NetworkAdapterChoice[] adapters;
        try { adapters = NetworkAdapters(); }
        catch (NetworkInformationException) { adapters = []; }
        Guid.TryParse(existing?.Field("adapter"), out var savedAdapter);
        var adapterNames = adapters.Select(a => a.Name).ToArray();
        var adapter = Select(stack, "Network adapter", adapterNames,
            adapters.FirstOrDefault(a => a.Id == savedAdapter)?.Name ?? "");
        if (adapters.Length == 0) stack.Children.Add(Text("No IPv4 adapters are available on this PC.", 11, true));
        else if (savedAdapter != Guid.Empty && adapter.SelectedIndex < 0)
            stack.Children.Add(Text("The saved adapter is missing. Choose one on this PC.", 11, true));

        var addressMode = Select(stack, "IPv4 address", NetworkModes, ModeLabel(existing?.Field("addressMode")));
        var addressFields = new StackPanel();
        stack.Children.Add(addressFields);
        var address = Input(addressFields, "IP address", existing?.Field("address") ?? "");
        var mask = Input(addressFields, "Subnet mask", existing?.Field("mask") ?? "");
        mask.ToolTip = "Dotted subnet mask or CIDR prefix, such as 255.255.255.0 or /24.";
        var gateway = Input(addressFields, "Gateway (optional)", existing?.Field("gateway") ?? "");
        gateway.ToolTip = "Leave blank to use no IPv4 default gateway on this adapter.";
        var dnsMode = Select(stack, "IPv4 DNS servers", NetworkModes, ModeLabel(existing?.Field("dnsMode")));
        var dnsFields = new StackPanel();
        stack.Children.Add(dnsFields);
        var primary = Input(dnsFields, "Primary DNS", existing?.Field("primaryDns") ?? "");
        var secondary = Input(dnsFields, "Secondary DNS (optional)", existing?.Field("secondaryDns") ?? "");
        foreach (var input in new[] { address, mask, gateway, primary, secondary }) input.MaxLength = 15;

        void UpdateFields()
        {
            addressFields.Visibility = addressMode.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            dnsFields.Visibility = dnsMode.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }
        addressMode.SelectionChanged += (_, _) => UpdateFields();
        dnsMode.SelectionChanged += (_, _) => UpdateFields();
        UpdateFields();
        var review = Check(stack, "Review changes before applying", existing?.Field("review") != "false");
        review.ToolTip = "Windows administrator approval still applies when this review is turned off.";
        var fields = new Dictionary<string, Control>
        {
            ["adapter"] = adapter, ["addressMode"] = addressMode, ["dnsMode"] = dnsMode, ["address"] = address,
            ["mask"] = mask, ["gateway"] = gateway, ["primaryDns"] = primary, ["secondaryDns"] = secondary
        };

        VaultItem Capture()
        {
            var item = existing is null
                ? new VaultItem { ContentType = provider.Descriptor.Id, FolderId = folder, Order = vault.Document!.Items.Count }
                : Codec.Clone(existing);
            item.Label = name.Text.Trim();
            item.IsSensitive = false;
            item.ModuleSchema = provider.Package.Manifest.SchemaFingerprint();
            item.Payload = JsonSerializer.SerializeToElement(new Dictionary<string, string>
            {
                ["adapter"] = adapter.SelectedIndex >= 0 ? adapters[adapter.SelectedIndex].Id.ToString("D") : "",
                ["addressMode"] = ModeValue(addressMode.SelectedIndex), ["dnsMode"] = ModeValue(dnsMode.SelectedIndex),
                ["address"] = address.Text.Trim(), ["mask"] = mask.Text.Trim(), ["gateway"] = gateway.Text.Trim(),
                ["primaryDns"] = primary.Text.Trim(), ["secondaryDns"] = secondary.Text.Trim(),
                ["review"] = review.IsChecked == true ? "true" : "false"
            });
            return item;
        }
        captureEditorDraft = Capture;
        PageAction(Button("Save entry", () =>
        {
            var item = Capture();
            if (item.Label.Length == 0) { RejectInput(name, "Enter a name for this profile."); return; }
            NetworkProfile profile;
            try { profile = NetworkProfile.Read(item); }
            catch (ArgumentException ex)
            {
                var field = ex.ParamName is { } key && fields.TryGetValue(key, out var target) ? target : adapter;
                field.Focus(); field.BringIntoView(); RejectInput(field, ex.Message.Split('\r', '\n')[0]);
                return;
            }
            save(item);
            if (!profile.Review && screen == "list" && vault.IsUnlocked
                && vault.Document!.Items.Any(i => i.Id == item.Id && i.Payload.GetRawText() == item.Payload.GetRawText()))
                RememberNetworkReview(provider, item);
        }, true));
        return stack;
    }

    private static string ModeLabel(string? value) => NetworkModes[value == "manual" ? 2 : value == "automatic" ? 1 : 0];
    private static string ModeValue(int selected) => selected switch { 2 => "manual", 1 => "automatic", _ => "keep" };
    private static string NetworkReviewKey(DeclarativeProvider provider, VaultItem item) =>
        Convert.ToHexString(SHA256.HashData(Codec.Encode(new
        {
            Purpose = "network-profile-skip-review", item.Id, provider.Package.Fingerprint, item.Payload
        })));

    private void RememberNetworkReview(DeclarativeProvider provider, VaultItem item)
    {
        try { moduleApprovals.Remember(NetworkReviewKey(provider, item)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or Win32Exception)
        { SetStatus("Network review could not be skipped on this PC."); }
    }

    private VaultItem? FindNetworkProfile(Guid id, out DeclarativeProvider? provider)
    {
        provider = null;
        if (!vault.IsUnlocked || busy || applyingNetworkProfile) return null;
        var document = vault.Document!;
        var current = document.Items.FirstOrDefault(i => i.Id == id && i.DeletedAt is null);
        if (current is null || document.Settings.DisabledModules.Contains(current.ContentType) || !ModuleAvailable(current)
            || content.Find(current.ContentType) is not DeclarativeProvider
                { Package: { Official: true, Manifest.Handler: "network-profile" } } selected) return null;
        provider = selected;
        return current;
    }

    private async Task RunNetworkProfile(VaultItem entry)
    {
        var item = FindNetworkProfile(entry.Id, out var provider);
        if (item is null || provider is null) return;
        NetworkProfile profile;
        NetworkAdapterChoice? adapter;
        try
        {
            profile = NetworkProfile.Read(item);
            adapter = NetworkAdapters().FirstOrDefault(a => a.Id == profile.AdapterId);
        }
        catch (ArgumentException)
        {
            ShowError("Edit this entry to correct its network settings.");
            return;
        }
        catch (NetworkInformationException)
        {
            ShowError("Windows could not list network adapters.");
            return;
        }
        if (adapter is null) { ShowError("The saved network adapter is missing. Edit this entry to choose one on this PC."); return; }

        var startEpoch = epoch;
        if (!profile.Review && moduleApprovals.Contains(NetworkReviewKey(provider, item)))
        {
            await ApplyNetworkProfile(item, provider, profile, startEpoch, Body.Content);
            return;
        }

        var stack = Form("Review network changes", "", "module-permission", parent: ShowList, compact: true);
        stack.Children.Add(Text(adapter.Name, 15));
        if (profile.AddressMode != NetworkSetting.Keep)
        {
            stack.Children.Add(Text("IPv4 address", 11, true));
            stack.Children.Add(Text(profile.AddressMode == NetworkSetting.Automatic ? "Automatic (DHCP)"
                : $"{profile.Address}/{profile.PrefixLength} · Gateway {profile.Gateway?.ToString() ?? "none"}", 13));
            stack.Children.Add(Text("Replaces this adapter's IPv4 addresses and default gateway.", 11, true));
        }
        if (profile.DnsMode != NetworkSetting.Keep)
        {
            stack.Children.Add(Text("IPv4 DNS servers", 11, true));
            stack.Children.Add(Text(profile.DnsMode == NetworkSetting.Automatic ? "Automatic (DHCP)"
                : profile.SecondaryDns is null ? profile.PrimaryDns!.ToString() : $"{profile.PrimaryDns}, {profile.SecondaryDns}", 13));
        }
        stack.Children.Add(Text("This can interrupt your connection. Windows will ask for administrator approval.", 11, true));
        var page = Body.Content;
        PageAction(AsyncButton("Apply changes", () => ApplyNetworkProfile(item, provider, profile, startEpoch, page)));
        pageSubmit = null; // Require an explicit click or keyboard activation of Apply.
    }

    private async Task ApplyNetworkProfile(VaultItem item, DeclarativeProvider provider, NetworkProfile profile, int startEpoch, object page)
    {
        bool IsCurrent() => startEpoch == epoch && vault.IsUnlocked && ReferenceEquals(page, Body.Content) && IsVisible;
        var latest = FindNetworkProfile(item.Id, out var currentProvider);
        if (!IsCurrent() || latest is null || currentProvider?.Package.Fingerprint != provider.Package.Fingerprint
            || latest.Payload.GetRawText() != item.Payload.GetRawText()) return;
        applyingNetworkProfile = true;
        modalDepth++;
        try
        {
#if DEBUG
            var result = App.PreviewMode
                ? await (previewNetworkApply?.Invoke(profile) ?? throw new InvalidOperationException("Live network changes are disabled in preview."))
                : await NetworkProfileBroker.Apply(profile);
#else
            var result = await NetworkProfileBroker.Apply(profile);
#endif
            if (!IsCurrent()) return;
            if (result != 0)
            {
                ShowError(result switch
                {
                    11 => "The network adapter is no longer available.",
                    12 => "Windows could not apply the IPv4 settings. Check the adapter before retrying.",
                    13 => "Windows could not apply DNS settings. Any selected IPv4 settings may already be applied.",
                    14 => "Windows could not add the secondary DNS server. The primary server was applied.",
                    _ => "Windows could not finish applying the profile. Check the adapter before retrying."
                });
                return;
            }
            Audit(AuditEvent.EntryActionStarted);
            ShowList();
            SetStatus("Network profile applied");
            if (!profile.Review) RememberNetworkReview(provider, item);
            _ = ShowRunFeedback(item.Id);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { if (IsCurrent()) SetStatus("Network changes cancelled"); }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
        { if (IsCurrent()) ShowError("Windows could not start the network change."); }
        finally { modalDepth--; applyingNetworkProfile = false; }
    }
}
