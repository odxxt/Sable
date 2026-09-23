using System.Windows.Automation;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private static TextBlock Text(string text, double size = 13, bool muted = false) => new() { Text = Localizer.Translate(text), FontSize = size, TextWrapping = TextWrapping.Wrap, Foreground = muted ? new SolidColorBrush(Color.FromRgb(170,170,175)) : Brushes.White, Margin = new Thickness(0, 0, 0, 8) };
    private static TextBlock Heading(string text) => new() { Text = Localizer.Translate(text).TrimEnd('.'), FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 7, 0, 10) };
    private static Button Button(string text, Action click, bool primary = false)
    {
        var b = new Button { Content = Localizer.Translate(text), HorizontalContentAlignment = primary ? HorizontalAlignment.Center : HorizontalAlignment.Left, Margin = new Thickness(0, 5, 0, 0) };
        if (primary) b.Style = (Style)Application.Current.FindResource("PrimaryButton");
        b.Click += (_, _) => click(); return b;
    }
    private static Button AsyncButton(string text, Func<Task> click, bool primary = true) => Button(text, async () => await click(), primary);
    private Action? backAction;
    private ContentControl? pageFooter;
    private Button? pageSubmit;
    private void PageAction(FrameworkElement action)
    {
        var buttons = action is Button single ? new[] { single } : LogicalDescendants(action).OfType<Button>().ToArray();
        foreach (var button in buttons) { button.MinHeight = 38; button.Padding = new Thickness(10, 7, 10, 7); button.HorizontalContentAlignment = HorizontalAlignment.Center; }
        if (pageFooter is not null) pageFooter.Content = action;
        pageSubmit = action as Button ?? LogicalDescendants(action).OfType<Button>().FirstOrDefault();
    }
    private bool HandlePageEnter(KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || recordingShortcut || contextOpen || modalDepth > 0) return false;
        if (e.IsRepeat || busy || pinAttemptRunning) { e.Handled = true; return true; }
        var source = e.OriginalSource as DependencyObject;
        // A focused button or open picker retains its ordinary keyboard action.
        if (FindParent<Button>(source) is not null || FindParent<ComboBox>(source)?.IsDropDownOpen == true) return false;
        if (pageSubmit is not { IsEnabled: true, IsVisible: true } submit) return false;
        e.Handled = true;
        submit.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, submit));
        return true;
    }
    private void GoBack() => NavigateSettings(() => { if (backAction is not null) backAction(); else if (vault.IsUnlocked) ShowList(); else ShowUnlock(); });
    private StackPanel Form(string title, string subtitle = "", string mode = "form", bool back = true, Action? parent = null, FrameworkElement? accessory = null)
    {
        var stack = new StackPanel { Margin = new Thickness(2, 0, 6, 4) };
        var page = new Grid();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition());
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (back)
        {
            var previous = Button("‹  Back", GoBack); previous.Height = 31; previous.Margin = new Thickness(0);
            previous.Padding = new Thickness(10, 6, 10, 6); page.Children.Add(previous);
        }
        var heading = Heading(title); var headingRow = new DockPanel(); Grid.SetRow(headingRow, 1); page.Children.Add(headingRow);
        if (accessory is not null) { DockPanel.SetDock(accessory, Dock.Right); headingRow.Children.Add(accessory); }
        headingRow.Children.Add(heading);
        if (subtitle.Length > 0 && mode is "recovery-key" or "recover" or "restore" or "restored") stack.Children.Add(Text(subtitle, 12, true));
        var scroll = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 2); page.Children.Add(scroll);
        SetBody(page, mode);
        pageFooter = new ContentControl { Margin = new Thickness(0, 6, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        Grid.SetRow(pageFooter, 3); page.Children.Add(pageFooter);
        backAction = parent ?? (mode is "settings" or "about" or "audit" ? ShowSettings : null);
        formError = Text("", 11); formError.Foreground = new SolidColorBrush(Color.FromRgb(255, 150, 156)); formError.Visibility = Visibility.Collapsed; stack.Children.Add(formError);
        return stack;
    }
    private TextBox Input(StackPanel stack, string label, string value = "", bool multiline = false)
    {
        stack.Children.Add(Text(label, 11, true));
        var box = new TextBox { Text = value, Margin = new Thickness(0, 0, 0, 13), AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden, MinHeight = multiline ? 68 : 35, MaxHeight = multiline ? 220 : 36 };
        AutomationProperties.SetName(box, label); stack.Children.Add(box); eraseEditors.Add(() => box.Clear()); return box;
    }
    private PasswordBox Password(StackPanel stack, string label)
    {
        stack.Children.Add(Text(label, 11, true));
        var box = new PasswordBox { Margin = new Thickness(0, 0, 0, 13), MinHeight = 36, MaxLength = 1024 };
        AutomationProperties.SetName(box, label); stack.Children.Add(box); eraseEditors.Add(box.Clear); return box;
    }
    private static CheckBox Check(StackPanel stack, string label, bool value)
    {
        var box = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, FontSize = 12 }, IsChecked = value };
        stack.Children.Add(box); return box;
    }
    private static ComboBox Select(StackPanel stack, string label, IEnumerable<string> values, string selected)
    {
        stack.Children.Add(Text(label, 11, true));
        var box = new ComboBox { ItemsSource = values.ToList(), SelectedItem = selected, Margin = new Thickness(0, 0, 0, 13) };
        box.PreviewMouseWheel += (_, e) =>
        {
            var menu = box.IsDropDownOpen ? box.Template.FindName("PART_Popup", box) as System.Windows.Controls.Primitives.Popup : null;
            e.Handled = true;
            var scroll = menu?.Child is { } child ? Descendants<ScrollViewer>(child).FirstOrDefault() : FindParent<ScrollViewer>(box);
            if (scroll is not null) SmoothScrolling.Wheel(scroll, e.Delta);
        };
        AutomationProperties.SetName(box, label); stack.Children.Add(box); return box;
    }
}
