using System.Windows.Automation;
using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private static string ActionKindName(string? kind) => kind switch { "ping" => "Ping", "tcp" => "Test port", "lookup" => "DNS lookup", "cmd" => "CMD", "powershell" => "PowerShell", "application" => "Application", "file" => "File or folder", "windows" or "control-panel" => "Windows settings", "run" => "Run command", _ => "Action" };
    private static bool IsConnection(IContentProvider provider) => provider is DeclarativeProvider { Package.Manifest.Handler: "rdp" or "putty" };
    private static bool IsShortcut(IContentProvider? provider) => provider is ShortcutProvider || provider is not null && IsConnection(provider);
    private IEnumerable<IContentProvider> AvailableStarters => content.All.Where(p => !vault.Document!.Settings.DisabledModules.Contains(p.Descriptor.Id));
    private void KeepEditorDraft()
    {
        if (screen != "editor" || !editorIsNew || captureEditorDraft is null) return;
        var draft = captureEditorDraft(); editorDrafts[draft.ContentType] = draft; editorSharedLabel = draft.Label;
        if (draft.ContentType is "sable.secret" or "sable.login") editorDrafts.Remove(draft.ContentType == "sable.secret" ? "sable.login" : "sable.secret");
    }
    private Button StarterRow(string title, string description, string icon, Action choose)
    {
        var row = Button("", choose); row.Style = (Style)FindResource("StarterButton");
        row.Height = 76; row.Padding = new Thickness(14, 12, 14, 12); row.Margin = new Thickness(0, 0, 0, 4); row.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new GridLength(32) }); grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new() { Width = new GridLength(12) });
        var iconHost = new Grid { Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        if (icon == "shortcut") iconHost.Children.Add(ShortcutSymbol());
        else if (icon is "connection" or "rdp" or "ssh") iconHost.Children.Add(ConnectionIcon(icon));
        else if (icon == "\uE72E") iconHost.Children.Add(new EntryIdentity { Kind = "secret" });
        else iconHost.Children.Add(new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 17, Opacity = .8, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
        grid.Children.Add(iconHost);
        var copy = new StackPanel(); Grid.SetColumn(copy, 1); grid.Children.Add(copy);
        copy.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.Medium });
        copy.Children.Add(new TextBlock { Text = description, FontSize = 11, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 4, 8, 0), TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 32 });
        var arrow = new TextBlock { Text = "\uE76C", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10, Opacity = .5, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(arrow, 2); grid.Children.Add(arrow);
        row.Content = grid; row.ToolTip = description; AutomationProperties.SetName(row, title); AutomationProperties.SetHelpText(row, description); return row;
    }
    private void ShowEntryStarters()
    {
        if (!vault.IsUnlocked) return;
        KeepEditorDraft(); editorIsNew = true;
        var stack = Form("New entry", "", "editor", parent: ShowList, compact: true);
        stack.Children.Add(Text("Choose a starting point", 12, true));
        var available = AvailableStarters.ToList();
        var secret = available.FirstOrDefault(p => p.Descriptor.Id == "sable.secret") ?? available.FirstOrDefault(p => p.Descriptor.Id == "sable.login");
        if (secret is not null)
        {
            var row = StarterRow("Secret or login", "Copy a saved value, or a username and password", "\uE72E", () =>
            {
                var type = editorDrafts.ContainsKey("sable.login") && available.Any(p => p.Descriptor.Id == "sable.login") ? "sable.login" : secret.Descriptor.Id;
                ShowEditor(selectedType: type);
            });
            stack.Children.Add(row);
            if (FirstEntryTour) row.Loaded += (_, _) => TourHint(row, "3 · Secret or login", "Start here to save a value you can copy later.");
        }
        if (available.Any(p => p is ShortcutProvider)) stack.Children.Add(StarterRow("Shortcut", "Open an app or file, or run a command", "shortcut", () => ShowEditor(selectedType: "sable.shortcut")));
        if (available.OfType<DeclarativeProvider>().Any(p => p.Package.Manifest.EffectiveCategory == "connection")) stack.Children.Add(StarterRow("Connection", "Connect with a saved sign-in", "connection", ShowConnectionStarters));
        if (available.OfType<DeclarativeProvider>().Any(p => p.Package.Manifest.EffectiveCategory == "extras")) stack.Children.Add(StarterRow("Extras", "Tools from installed modules", "\uE713", ShowExtraStarters));
        var rows = stack.Children.OfType<Button>().ToList();
        if (rows.Count == 4) foreach (var row in rows) { row.Height = 56; row.Padding = new Thickness(14, 8, 14, 8); }
        if (stack.Children.OfType<Button>().Any()) FocusStarter(stack);
        else stack.Children.Add(Text("Enable an entry type in Settings → Plugins & modules.", 12, true));
    }
    private void ShowConnectionStarters()
    {
        if (!vault.IsUnlocked) return;
        KeepEditorDraft();
        var stack = Form("Connection", "", "editor", parent: ShowEntryStarters, compact: true);
        stack.Children.Add(Text("Choose how to connect", 12, true));
        foreach (var provider in AvailableStarters.OfType<DeclarativeProvider>().Where(p => p.Package.Manifest.EffectiveCategory == "connection"))
            stack.Children.Add(StarterRow(TemplateName(provider), provider.Package.Manifest.Handler switch { "putty" => "Open PuTTY with your saved SSH sign-in", "rdp" => "Open Windows Remote Desktop with your saved sign-in", _ => provider.Package.Manifest.Description }, provider.Package.Manifest.Handler switch { "putty" => "ssh", "rdp" => "rdp", _ => provider.Descriptor.Symbol }, () => ShowEditor(selectedType: provider.Descriptor.Id)));
        FocusStarter(stack);
    }
    private void ShowExtraStarters()
    {
        if (!vault.IsUnlocked) return;
        KeepEditorDraft();
        var stack = Form("Extras", "", "editor", parent: ShowEntryStarters, compact: true);
        stack.Children.Add(Text("Choose a tool", 12, true));
        foreach (var provider in AvailableStarters.OfType<DeclarativeProvider>().Where(p => p.Package.Manifest.EffectiveCategory == "extras"))
            stack.Children.Add(StarterRow(provider.Descriptor.Name, provider.Package.Manifest.Description, provider.Descriptor.Symbol, () => ShowEditor(selectedType: provider.Descriptor.Id)));
        FocusStarter(stack);
    }
    private void FocusStarter(StackPanel stack)
    {
        var owner = Body.Content;
        Dispatcher.BeginInvoke(() => { if (ReferenceEquals(owner, Body.Content)) stack.Children.OfType<Button>().FirstOrDefault()?.Focus(); });
        if (SystemParameters.ClientAreaAnimation) stack.BeginAnimation(OpacityProperty, new DoubleAnimation(.5, 1, TimeSpan.FromMilliseconds(140)) { FillBehavior = FillBehavior.Stop });
    }
    private FrameworkElement EntryChoices(IContentProvider provider, VaultItem? existing)
    {
        var row = new DockPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        if (existing is null)
        {
            var change = Button("Change", provider is DeclarativeProvider extra && extra.Package.Manifest.EffectiveCategory == "extras" ? ShowExtraStarters : IsConnection(provider) ? ShowConnectionStarters : ShowEntryStarters);
            change.Content = new TextBlock { Text = "Change", FontSize = 11, Foreground = (Brush)FindResource("Muted") };
            change.FontSize = 11; change.Margin = new Thickness(0); change.Padding = new Thickness(8, 4, 8, 4); change.ToolTip = "Choose another starter. Your draft is kept until you leave the editor.";
            AutomationProperties.SetName(change, "Change starter"); DockPanel.SetDock(change, Dock.Right); row.Children.Add(change);
        }
        return row;
    }
    private static FrameworkElement ShortcutSymbol() => new EntryIdentity { Kind = "shortcut", Width = 20, Height = 20 };
    private static FrameworkElement ConnectionIcon(string kind) => new EntryIdentity { Kind = kind == "ssh" ? "terminal" : "desktop" };
    private static string TemplateName(IContentProvider provider) => provider.Descriptor.Id switch
    {
        "sable.secret" => "Secret", "sable.login" => "Login", "sable.shortcut" => "Shortcut",
        "module.sablelatch.putty" => "SSH · PuTTY", "module.sablelatch.rdp" => "Remote Desktop", _ => provider.Descriptor.Name
    };
}
