using System.Net.NetworkInformation;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private const string ManualNetworkMethod = "Manual";
    private const string AutomaticNetworkMethod = "Automatic (DHCP)";
    private const string DefaultSubnetMask = "255.255.255.0";
    private static readonly string[] NetworkMethods = [ManualNetworkMethod, AutomaticNetworkMethod];

    private sealed record NetworkChangeBlock(OptionGroup Section, StackPanel ManualFields, ComboBox Method)
    {
        public Border View => Section.View;
        public Button Header => Section.Header;
    }

    private FrameworkElement BuildNetworkProfileFields(DeclarativeProvider provider, VaultItem? existing, Action<VaultItem> save)
    {
        var stack = new StackPanel();
        var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        header.ColumnDefinitions.Add(new());
        header.ColumnDefinitions.Add(new() { Width = new GridLength(8) });
        header.ColumnDefinitions.Add(new());
        header.RowDefinitions.Add(new() { Height = GridLength.Auto });
        header.RowDefinitions.Add(new() { Height = new GridLength(32) });
        stack.Children.Add(header);

        header.Children.Add(InputCaption("Entry name"));
        var adapterCaption = InputCaption("Network adapter");
        Grid.SetColumn(adapterCaption, 2);
        header.Children.Add(adapterCaption);
        var name = InputControl("Entry name", existing?.Label ?? editorSharedLabel);
        name.MaxLength = 300;
        name.Margin = new Thickness(0);
        name.VerticalContentAlignment = VerticalAlignment.Center;
        name.ToolTip = "Optional. Sable can name the entry after its changes.";
        Grid.SetRow(name, 1);
        header.Children.Add(name);

        NetworkAdapterChoice[] adapters;
        try { adapters = NetworkAdapters(); }
        catch (NetworkInformationException) { adapters = []; }
        Guid.TryParse(existing?.Field("adapter"), out var savedAdapter);
        var adapter = SelectControl("Network adapter", adapters.Select(a => a.Name), "");
        adapter.SelectedIndex = Array.FindIndex(adapters, a => a.Id == savedAdapter);
        adapter.Margin = new Thickness(0);
        Grid.SetRow(adapter, 1);
        Grid.SetColumn(adapter, 2);
        header.Children.Add(adapter);
        if (adapters.Length == 0)
            stack.Children.Add(Text("No IPv4 adapters are available on this PC.", 11, true));
        else if (savedAdapter != Guid.Empty && adapter.SelectedIndex < 0)
            stack.Children.Add(Text("The saved adapter is missing. Choose one on this PC.", 11, true));

        var removeIp = NetworkRemoveButton("Remove IP address change");
        var removeDns = NetworkRemoveButton("Remove DNS change");
        var ip = BuildNetworkChangeBlock("IP address", existing?.Field("addressMode"), removeIp);
        var dns = BuildNetworkChangeBlock("DNS", existing?.Field("dnsMode"), removeDns);
        // Layout order is fixed even when DNS is added first.
        stack.Children.Add(ip.View);
        stack.Children.Add(dns.View);
        var address = new Ipv4EntryField("IP address", existing?.Field("address") ?? "");
        var mask = new Ipv4EntryField("Subnet mask",
            existing?.Field("mask") is { Length: > 0 } savedMask ? savedMask : DefaultSubnetMask, subnetMask: true);
        var primary = new Ipv4EntryField("DNS server", existing?.Field("primaryDns") ?? "");
        ip.ManualFields.Children.Add(address);
        ip.ManualFields.Children.Add(mask);
        var gateway = AddOptionalNetworkField(ip.ManualFields, "Default gateway", existing?.Field("gateway") ?? "",
            "Manual IPv4 without a gateway sets no default gateway on this adapter.");
        dns.ManualFields.Children.Add(primary);
        var secondary = AddOptionalNetworkField(dns.ManualFields, "Secondary DNS", existing?.Field("secondaryDns") ?? "");

        var menu = new ContextMenu { Placement = PlacementMode.Bottom };
        var add = Button("Add a change  ⌄", () => menu.IsOpen = true);
        add.HorizontalAlignment = HorizontalAlignment.Left;
        add.MinHeight = 33;
        add.Margin = new Thickness(0, 2, 0, 8);
        add.SetResourceReference(BackgroundProperty, "ControlSurface");
        AutomationProperties.SetName(add, "Add a change");
        add.ContextMenu = menu;
        menu.PlacementTarget = add;
        TrackContextMenu(menu);
        var addIp = new MenuItem { Header = "IP address" };
        var addDns = new MenuItem { Header = "DNS" };
        menu.Items.Add(addIp);
        menu.Items.Add(addDns);
        stack.Children.Add(add);
        var review = Check(stack, "Review before applying", existing?.Field("review") != "false");
        review.Margin = new Thickness(2, 3, 0, 6);
        review.ToolTip = "Windows administrator approval is still required.";

        NetworkChangeBlock? open = IsAdded(ip) ? ip : IsAdded(dns) ? dns : null;
        var page = Body.Content;
        var submit = Button("Save entry", () => { }, true);
        submit.Click += (_, _) => Commit();
        void Update()
        {
            UpdateBlock(ip, address);
            UpdateBlock(dns, primary);
            addIp.IsEnabled = !IsAdded(ip);
            addDns.IsEnabled = !IsAdded(dns);
            add.Visibility = addIp.IsEnabled || addDns.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
            submit.IsEnabled = adapters.Length > 0 && (IsAdded(ip) || IsAdded(dns));
        }
        void UpdateBlock(NetworkChangeBlock block, Ipv4EntryField value)
        {
            var expanded = ReferenceEquals(open, block) && IsAdded(block);
            var automatic = block.Method.SelectedIndex == 1;
            block.Section.SetExpanded(expanded);
            block.ManualFields.Visibility = automatic ? Visibility.Collapsed : Visibility.Visible;
            block.Section.Summary.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
            block.Section.Summary.Text = automatic ? AutomaticNetworkMethod : value.HasValue ? value.Value : ManualNetworkMethod;
        }
        void Toggle(NetworkChangeBlock block)
        {
            open = ReferenceEquals(open, block) ? null : block;
            Update();
            if (open is not null) block.Header.BringIntoView();
        }
        void Add(NetworkChangeBlock block)
        {
            if (!ReferenceEquals(page, Body.Content) || !vault.IsUnlocked || IsAdded(block)) return;
            menu.IsOpen = false;
            block.View.Visibility = Visibility.Visible;
            open = block;
            Update();
            block.Header.Focus();
            block.View.BringIntoView();
        }
        void Remove(NetworkChangeBlock block, params Ipv4EntryField[] inputs)
        {
            block.View.Visibility = Visibility.Collapsed;
            block.Method.SelectedIndex = 0;
            foreach (var input in inputs) input.Clear();
            if (ReferenceEquals(open, block)) open = IsAdded(ip) ? ip : IsAdded(dns) ? dns : null;
            Update();
            add.Focus();
        }

        ip.Header.Click += (_, _) => Toggle(ip);
        dns.Header.Click += (_, _) => Toggle(dns);
        ip.Method.SelectionChanged += (_, _) => Update();
        dns.Method.SelectionChanged += (_, _) => Update();
        addIp.Click += (_, _) => Add(ip);
        addDns.Click += (_, _) => Add(dns);
        removeIp.Click += (_, _) =>
        {
            Remove(ip, address);
            mask.Reset(DefaultSubnetMask);
            gateway.Hide();
        };
        removeDns.Click += (_, _) =>
        {
            Remove(dns, primary);
            secondary.Hide();
        };
        eraseEditors.Add(() =>
        {
            address.Clear();
            mask.Clear();
            primary.Clear();
            gateway.Input.Clear();
            secondary.Input.Clear();
        });
        Update();
        PageAction(submit);
        captureEditorDraft = Capture;
        return stack;

        VaultItem Capture()
        {
            var item = existing is null
                ? new VaultItem { ContentType = provider.Descriptor.Id, FolderId = folder, Order = vault.Document!.Items.Count }
                : Codec.Clone(existing);
            item.Label = name.Text.Trim();
            item.IsSensitive = false;
            item.ModuleSchema = provider.Package.Manifest.SchemaFingerprint();
            var ipMode = ModeValue(ip);
            var dnsMode = ModeValue(dns);
            item.Payload = JsonSerializer.SerializeToElement(new Dictionary<string, string>
            {
                ["adapter"] = adapter.SelectedIndex >= 0 ? adapters[adapter.SelectedIndex].Id.ToString("D") : "",
                ["addressMode"] = ipMode,
                ["dnsMode"] = dnsMode,
                ["address"] = ipMode == "manual" ? address.Value : "",
                ["mask"] = ipMode == "manual" ? mask.Value : "",
                ["gateway"] = ipMode == "manual" ? gateway.Input.Value : "",
                ["primaryDns"] = dnsMode == "manual" ? primary.Value : "",
                ["secondaryDns"] = dnsMode == "manual" ? secondary.Input.Value : "",
                ["review"] = review.IsChecked == true ? "true" : "false"
            });
            return item;
        }

        void Commit()
        {
            if (!ReferenceEquals(page, Body.Content) || !vault.IsUnlocked || adapters.Length == 0 || !IsAdded(ip) && !IsAdded(dns)) return;
            var item = Capture();
            NetworkProfile profile;
            try { profile = NetworkProfile.Read(item); }
            catch (ArgumentException ex)
            {
                var message = ex.Message.Split('\r', '\n')[0];
                switch (ex.ParamName)
                {
                    case "address": Invalid(ip, address, message); break;
                    case "mask": Invalid(ip, mask, message); break;
                    case "gateway": Invalid(ip, gateway.Input, message); break;
                    case "primaryDns": Invalid(dns, primary, message); break;
                    case "secondaryDns": Invalid(dns, secondary.Input, message); break;
                    default: adapter.Focus(); adapter.BringIntoView(); RejectInput(adapter, message); break;
                }
                return;
            }
            if (item.Label.Length == 0)
                item.Label = IsAdded(ip) ? IsAdded(dns) ? "Set IP and DNS" : "Set IP address" : "Set DNS";
            save(item);
            if (!profile.Review && screen == "list" && vault.IsUnlocked
                && vault.Document!.Items.Any(i => i.Id == item.Id && i.Payload.GetRawText() == item.Payload.GetRawText()))
                RememberNetworkReview(provider, item);
        }

        void Invalid(NetworkChangeBlock block, Ipv4EntryField input, string message)
        {
            open = block;
            Update();
            input.ShowError(message);
        }
    }

    private static bool IsAdded(NetworkChangeBlock block) => block.View.Visibility == Visibility.Visible;
    private static string ModeValue(NetworkChangeBlock block) =>
        !IsAdded(block) ? "keep" : block.Method.SelectedIndex == 1 ? "automatic" : "manual";

    private NetworkChangeBlock BuildNetworkChangeBlock(string title, string? storedMode, Button remove)
    {
        var fields = new StackPanel();
        var section = CreateOptionGroup(title, fields, remove);
        section.View.Visibility = storedMode is "manual" or "automatic" ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(section.Header, title + " settings");
        var methodRow = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        methodRow.ColumnDefinitions.Add(new());
        methodRow.ColumnDefinitions.Add(new() { Width = new GridLength(135) });
        var methodLabel = Text("Method", 11, true);
        methodLabel.VerticalAlignment = VerticalAlignment.Center;
        methodLabel.Margin = new Thickness(0);
        methodRow.Children.Add(methodLabel);
        var method = SelectControl(title + " method", NetworkMethods,
            storedMode == "automatic" ? AutomaticNetworkMethod : ManualNetworkMethod);
        method.Margin = new Thickness(0);
        Grid.SetColumn(method, 1);
        methodRow.Children.Add(method);
        fields.Children.Add(methodRow);
        var manual = new StackPanel();
        fields.Children.Add(manual);
        return new(section, manual, method);
    }

    private Button NetworkRemoveButton(string label)
    {
        var remove = new Button { Style = (Style)FindResource("StarterButton") };
        remove.Content = new TextBlock
        {
            Text = "\uE711", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        remove.Width = 32;
        remove.Height = 32;
        remove.Padding = new Thickness(0);
        remove.Margin = new Thickness(0);
        remove.VerticalAlignment = VerticalAlignment.Center;
        remove.HorizontalContentAlignment = HorizontalAlignment.Center;
        remove.ToolTip = label;
        AutomationProperties.SetName(remove, label);
        return remove;
    }

    private (Ipv4EntryField Input, Action Hide) AddOptionalNetworkField(StackPanel host, string label, string value, string? tooltip = null)
    {
        // Build both states once so disclosing a different block keeps the draft.
        var add = Button("+ " + label, () => { });
        AutomationProperties.SetName(add, "Add " + label);
        add.Margin = new Thickness(0, 0, 0, 3);
        add.HorizontalAlignment = HorizontalAlignment.Left;
        add.Padding = new Thickness(6, 5, 6, 5);
        add.ToolTip = tooltip;
        var remove = NetworkRemoveButton("Remove " + label);
        var field = new Ipv4EntryField(label, value, remove: remove) { ToolTip = tooltip };
        add.Click += (_, _) => { Show(); field.FocusFirst(); };
        remove.Click += (_, _) => { Hide(); add.Focus(); };
        host.Children.Add(field);
        host.Children.Add(add);
        if (field.HasValue) Show();
        else field.Visibility = Visibility.Collapsed;
        return (field, Hide);

        void Show()
        {
            field.Visibility = Visibility.Visible;
            add.Visibility = Visibility.Collapsed;
        }
        void Hide()
        {
            field.Clear();
            field.Visibility = Visibility.Collapsed;
            add.Visibility = Visibility.Visible;
        }
    }
}
