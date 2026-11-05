namespace SableLatch.Desktop;

public partial class MainWindow
{
    private static string ActionKindName(string? kind) => kind switch { "ping" => "Ping", "tcp" => "Test port", "lookup" => "DNS lookup", "cmd" => "CMD", "powershell" => "PowerShell", "application" => "Application", "file" => "File or folder", "windows" or "control-panel" => "Windows settings", "run" => "Run command", _ => "Action" };
    private static bool IsShortcut(IContentProvider? provider) => provider is ShortcutProvider or DeclarativeProvider { Package.Manifest.Handler: "rdp" or "putty" };
    private FrameworkElement EntryChoices(IContentProvider provider, VaultItem? existing)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var shortcut = IsShortcut(provider);
        var categories = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        var group = Guid.NewGuid().ToString();
        void Choice(Panel host, string label, string id, bool selected, string name, string tip)
        {
            var b = new RadioButton { Content = label, GroupName = group, IsChecked = selected, Style = (Style)FindResource("SegmentChoice"), ToolTip = tip };
            System.Windows.Automation.AutomationProperties.SetName(b, name);
            b.IsEnabled = !vault.Document!.Settings.DisabledModules.Contains(id);
            b.Click += (_, _) => { if (existing is null && id != provider.Descriptor.Id) ShowEditor(null, id); };
            host.Children.Add(b);
        }
        if (existing is not null) { stack.Children.Add(Text(shortcut ? "Shortcut · " + provider.Descriptor.Name : provider.Descriptor.Name, 11, true)); return stack; }
        Choice(categories, "Secret", "sable.secret", !shortcut, "Entry category Secret", "Save a value or login to copy.");
        Choice(categories, "Shortcut", "sable.shortcut", shortcut, "Entry type Shortcut", "Click to open a file, run a command or connect to a server.");
        stack.Children.Add(new Border { Background = (Brush)FindResource("ControlSurface"), CornerRadius = new CornerRadius(10), Padding = new Thickness(3), Child = categories });
        group = "entry-type-" + Guid.NewGuid().ToString();
        var options = new System.Windows.Controls.Primitives.UniformGrid { Columns = shortcut ? 2 : 3, Margin = new Thickness(0, 7, 0, 0) };
        if (!shortcut)
        {
            Choice(options, "Value", "sable.secret", provider.Descriptor.Id == "sable.secret", "Entry type Secret", "A masked value, including multiline text.");
            Choice(options, "Login", "sable.login", provider.Descriptor.Id == "sable.login", "Entry type Login", "Copy username first, then password.");
        }
        else
        {
            Choice(options, "Action", "sable.shortcut", provider is ShortcutProvider, "Shortcut action", "Files, applications, commands and network tools.");
            var connection = new RadioButton { Content = provider is DeclarativeProvider ? provider.Descriptor.Name : "Connection", Style = (Style)FindResource("SegmentChoice"), GroupName = group, IsChecked = provider is DeclarativeProvider, ToolTip = "Choose or install SSH and Remote Desktop" };
            connection.Click += (_, _) => ShowMoreTypes(); options.Children.Add(connection);
        }
        if (shortcut) { stack.Children.Add(options); return stack; }
        var more = new RadioButton { Content = "More ▾", Style = (Style)FindResource("SegmentChoice"), GroupName = group, ToolTip = "Installed types and modules" };
        System.Windows.Automation.AutomationProperties.SetName(more, "More types"); more.Click += (_, _) => ShowMoreTypes(); options.Children.Add(more);
        stack.Children.Add(options); return stack;
    }
}
