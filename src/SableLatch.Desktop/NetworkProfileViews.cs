using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text.Json;

namespace SableLatch.Desktop;

public partial class MainWindow
{
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

        var stack = Form("Review changes", "", "module-permission", back: false, parent: ShowList, compact: true);
        var identity = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        identity.ColumnDefinitions.Add(new());
        identity.ColumnDefinitions.Add(new() { Width = new GridLength(115) });
        var entryName = Text(item.Label, 13);
        entryName.FontWeight = FontWeights.SemiBold;
        entryName.Margin = new Thickness(0, 0, 9, 0);
        identity.Children.Add(entryName);
        var adapterName = Text(adapter.Name, 11, true);
        adapterName.Margin = new Thickness(0);
        adapterName.TextAlignment = TextAlignment.Right;
        adapterName.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(adapterName, 1);
        identity.Children.Add(adapterName);
        stack.Children.Add(identity);

        var values = new StackPanel();
        var summary = new Border
        {
            Child = values, CornerRadius = new CornerRadius(11),
            Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 0, 7)
        };
        summary.SetResourceReference(Border.BackgroundProperty, "OptionGroupSurface");
        stack.Children.Add(summary);
        void ReviewValue(string label, string value)
        {
            var section = new StackPanel { Margin = new Thickness(0, 6, 0, 7) };
            if (values.Children.Count > 0)
            {
                var rule = new Border { Height = 1 };
                rule.SetResourceReference(Border.BackgroundProperty, "Line");
                values.Children.Add(rule);
            }
            var caption = Text(label, 11, true);
            caption.Margin = new Thickness(0, 0, 0, 2);
            section.Children.Add(caption);
            var content = Text(value, 16);
            content.Margin = new Thickness(0);
            content.FontWeight = FontWeights.Medium;
            section.Children.Add(content);
            values.Children.Add(section);
        }
        if (profile.AddressMode != NetworkSetting.Keep)
        {
            ReviewValue("IP address", profile.AddressMode == NetworkSetting.Automatic ? AutomaticNetworkMethod : profile.Address!.ToString());
            if (profile.AddressMode == NetworkSetting.Manual)
            {
                ReviewValue("Subnet mask", Ipv4EntryField.FormatMask(profile.PrefixLength));
                if (profile.Gateway is not null) ReviewValue("Default gateway", profile.Gateway.ToString());
            }
        }
        if (profile.DnsMode != NetworkSetting.Keep)
        {
            ReviewValue("DNS", profile.DnsMode == NetworkSetting.Automatic ? AutomaticNetworkMethod : profile.PrimaryDns!.ToString());
            if (profile.SecondaryDns is not null) ReviewValue("Secondary DNS", profile.SecondaryDns.ToString());
        }
        if (profile.AddressMode == NetworkSetting.Manual && profile.Gateway is null)
        {
            var noGateway = Text("No default gateway will be set.", 11, true);
            noGateway.Margin = new Thickness(0, 0, 0, 4);
            stack.Children.Add(noGateway);
        }
        stack.Children.Add(Text("Windows will ask for administrator approval.", 11, true));
        var page = Body.Content;
        var actions = new DockPanel();
        var cancel = Button("Cancel", ShowList);
        cancel.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(cancel, Dock.Left);
        actions.Children.Add(cancel);
        actions.Children.Add(AsyncButton("Apply changes", () => ApplyNetworkProfile(item, provider, profile, startEpoch, page)));
        PageAction(actions);
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
            SetStatus("Network change applied");
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
