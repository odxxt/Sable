using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace SableLatch.Desktop;

internal sealed record PickerChoice(string Name, string Target, string Arguments = "", string Directory = "", string? IconPath = null, string Category = "", string Keywords = "", string Glyph = "\uE713");

public partial class MainWindow
{
    private Action? closeChoicePicker;
    private async void ShowChoicePicker(string title, Task<IReadOnlyList<PickerChoice>> choicesTask, Action<PickerChoice> select, Action? browse = null, bool settings = false)
    {
        if (!vault.IsUnlocked) return;
        closeChoicePicker?.Invoke();
        if (Body.Content is not FrameworkElement previous) return;
        var oldVisibility = previous.Visibility;
        Body.Content = null; previous.Visibility = Visibility.Hidden;
        var host = new Grid(); host.Children.Add(previous);
        var panel = new Grid { Margin = new Thickness(12) };
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto }); panel.RowDefinitions.Add(new() { Height = GridLength.Auto }); panel.RowDefinitions.Add(new()); panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.Children.Add(Text(title, 14));
        var filter = new TextBox { MinHeight = 36, Margin = new Thickness(0, 0, 0, 10) };
        var searchName = browse is not null ? "Find an application" : "Find a setting";
        System.Windows.Automation.AutomationProperties.SetName(filter, searchName);
        filter.ToolTip = "Search by name";
        var searchHost = new Grid(); Grid.SetRow(searchHost, 1); panel.Children.Add(searchHost); searchHost.Children.Add(filter);
        var placeholder = new TextBlock { Text = searchName + "…", Opacity = .45, Margin = new Thickness(12, 0, 12, 10), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        searchHost.Children.Add(placeholder);
        filter.TextChanged += (_, _) => placeholder.Visibility = filter.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var results = new StackPanel(); var scroll = new ScrollViewer { Content = results, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 2); panel.Children.Add(scroll);
        var footer = new DockPanel { Margin = new Thickness(0, 6, 0, 0) }; Grid.SetRow(footer, 3); panel.Children.Add(footer);
        var border = new Border { Background = new SolidColorBrush(Color.FromRgb(33, 33, 39)), CornerRadius = new CornerRadius(14), Child = panel };
        host.Children.Add(border); Body.Content = host; modalDepth++;
        var icons = new Dictionary<string, ImageSource?>();
        void Close()
        {
            if (!ReferenceEquals(Body.Content, host)) return;
            closeChoicePicker = null; modalDepth--; results.Children.Clear(); icons.Clear(); filter.Clear();
            host.Children.Remove(previous); previous.Visibility = oldVisibility;
            if (ReferenceEquals(Body.Content, host)) Body.Content = previous;
        }
        closeChoicePicker = Close;
        footer.Children.Add(Button("Cancel", Close));
        if (browse is not null) { var other = Button("Browse…", () => { if (!ReferenceEquals(Body.Content, host) || !vault.IsUnlocked) return; Close(); browse(); }); other.HorizontalAlignment = HorizontalAlignment.Right; footer.Children.Add(other); }
        results.Children.Add(Text("Finding choices…", 11, true));
        filter.Loaded += (_, _) => filter.Focus();
        var startEpoch = epoch;
        var choices = await choicesTask;
        if (epoch != startEpoch || !ReferenceEquals(Body.Content, host) || !vault.IsUnlocked) return;
        void Populate()
        {
            results.Children.Clear();
            var matches = settings ? WindowsSettingsCatalog.Search(choices, filter.Text).ToList() : choices.Where(c => c.Name.Contains(filter.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var choice in matches.Take(60))
            {
                var button = Button(choice.Name, () => { if (!ReferenceEquals(Body.Content, host) || !vault.IsUnlocked || startEpoch != epoch) return; Close(); select(choice); }); button.ToolTip = choice.Target; button.FontSize = 12; button.Margin = new Thickness(0, 2, 0, 2);
                System.Windows.Automation.AutomationProperties.SetName(button, choice.Category.Length == 0 ? choice.Name : choice.Name + " · " + choice.Category);
                var row = new DockPanel();
                ImageSource? source = null;
                if (choice.IconPath is { } path)
                {
                    if (!icons.TryGetValue(path, out source))
                    {
                        try { using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path); if (icon is not null) { var bitmap = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(16, 16)); bitmap.Freeze(); source = bitmap; } }
                        catch (Exception ex) when (ex is ArgumentException or IOException or System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
                        icons[path] = source;
                    }
                }
                if (source is not null) row.Children.Add(new Image { Source = source, Width = 16, Height = 16, Margin = new Thickness(0, 0, 9, 0) });
                else row.Children.Add(new TextBlock { Text = choice.Glyph, ToolTip = choice.Category, FontFamily = new FontFamily("Segoe MDL2 Assets"), Width = 16, Margin = new Thickness(0, 0, 9, 0), Opacity = .5 });
                var caption = new StackPanel(); caption.Children.Add(new TextBlock { Text = choice.Name, TextTrimming = TextTrimming.CharacterEllipsis });
                if (choice.Category.Length > 0) caption.Children.Add(new TextBlock { Text = choice.Category, FontSize = 10, Opacity = .5 });
                row.Children.Add(caption); button.Content = row; results.Children.Add(button);
            }
            if (results.Children.Count == 0) results.Children.Add(Text("No matches", 12, true));
            else if (matches.Count > 60) results.Children.Add(Text("Keep typing to narrow the results", 11, true));
            if (settings && filter.Text.Trim().Length == 0) results.Children.Add(Text("Search for more settings", 11, true));
            scroll.ScrollToTop();
        }
        filter.TextChanged += (_, _) => { if (closeChoicePicker is not null && ReferenceEquals(Body.Content, host)) Populate(); };
        filter.KeyDown += (_, e) => { if (e.Key == Key.Enter && !e.IsRepeat && results.Children.OfType<Button>().FirstOrDefault() is { } first) { e.Handled = true; first.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); } };
        Populate(); filter.Focus();
    }
}
