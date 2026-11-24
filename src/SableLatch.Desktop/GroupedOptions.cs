using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private sealed record OptionGroup(Border View, Button Header, TextBlock Summary, Action<bool> SetExpanded);

    private FrameworkElement GroupedOptions(string title, FrameworkElement content, bool expanded = false)
    {
        var section = CreateOptionGroup(title, content);
        section.Header.Click += (_, _) =>
        {
            var opening = content.Visibility != Visibility.Visible;
            section.SetExpanded(opening);
            if (opening && SystemParameters.ClientAreaAnimation)
                content.BeginAnimation(OpacityProperty, new DoubleAnimation(.45, 1, TimeSpan.FromMilliseconds(150)) { FillBehavior = FillBehavior.Stop });
        };
        section.SetExpanded(expanded);
        return section.View;
    }

    private OptionGroup CreateOptionGroup(string title, FrameworkElement content, Button? trailing = null)
    {
        var group = new StackPanel();
        var surface = new Border { Child = group, CornerRadius = new CornerRadius(11), Margin = new Thickness(0, 0, 0, 8) };
        var toggle = Button(title, () => { }); toggle.Margin = new Thickness(0); toggle.Padding = new Thickness(12, 10, 12, 10);
        var heading = new DockPanel(); var arrow = new TextBlock { Text = "⌄", Opacity = .6, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(arrow, Dock.Right);
        heading.Children.Add(arrow);
        var summary = Text("", 11, true);
        summary.Margin = new Thickness(0, 2, 0, 0);
        summary.Visibility = Visibility.Collapsed;
        var labels = new StackPanel();
        labels.Children.Add(new TextBlock { Text = title, FontSize = 12 });
        labels.Children.Add(summary);
        heading.Children.Add(labels);
        toggle.Content = heading; toggle.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        System.Windows.Automation.AutomationProperties.SetName(toggle, title);
        content.Margin = new Thickness(12, 4, 12, 12);
        if (trailing is not null)
        {
            var header = new DockPanel();
            trailing.Margin = new Thickness(5, 0, 5, 0);
            DockPanel.SetDock(trailing, Dock.Right);
            header.Children.Add(trailing);
            header.Children.Add(toggle);
            group.Children.Add(header);
        }
        else group.Children.Add(toggle);
        group.Children.Add(content);
        void Appearance()
        {
            var open = content.Visibility == Visibility.Visible;
            if (open) surface.SetResourceReference(Border.BackgroundProperty, "OptionGroupSurface");
            else surface.Background = Brushes.Transparent;
            arrow.Text = open ? "⌃" : "⌄";
            System.Windows.Automation.AutomationProperties.SetHelpText(toggle, open ? "Expanded" : "Collapsed");
        }
        content.IsVisibleChanged += (_, _) => Appearance();
        void Expand(bool open)
        {
            content.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            Appearance();
        }
        return new(surface, toggle, summary, Expand);
    }
    private TextBox FolderInput(StackPanel host, string label, string value)
    {
        host.Children.Add(Text(label, 11, true));
        var row = new Grid { Margin = new Thickness(0, 0, 0, 13) }; row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var input = new TextBox { Text = value, MinWidth = 0, Height = 36, VerticalContentAlignment = VerticalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(input, label); row.Children.Add(input); eraseEditors.Add(input.Clear);
        var browse = Button("Browse…", () =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog(); modalDepth++;
            try { if (dialog.ShowDialog(this) == true) input.Text = dialog.FolderName; } finally { modalDepth--; }
        });
        browse.Background = (Brush)FindResource("ControlSurface"); browse.Height = 36; browse.Margin = new Thickness(7, 0, 0, 0); browse.ToolTip = "Choose " + label.ToLowerInvariant();
        Grid.SetColumn(browse, 1); row.Children.Add(browse); host.Children.Add(row); return input;
    }
    private void BindMenuToggle(Button trigger, ContextMenu menu)
    {
        var dismissedByTrigger = false;
        menu.Closed += (_, _) => dismissedByTrigger = Mouse.LeftButton == MouseButtonState.Pressed && new Rect(trigger.RenderSize).Contains(Mouse.GetPosition(trigger));
        menu.Opened += (_, _) => { if (SystemParameters.ClientAreaAnimation) menu.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(110)) { FillBehavior = FillBehavior.Stop }); };
        trigger.Click += (_, _) =>
        {
            if (dismissedByTrigger) { dismissedByTrigger = false; return; }
            menu.IsOpen = !menu.IsOpen;
        };
    }
}
