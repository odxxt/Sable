namespace SableLatch.Desktop;

public partial class MainWindow
{
    private Action<EntryAction?>? refreshEntryDetails;
    private Action? refreshScriptFields;
    private Action? refreshScriptLabels;
    private Action? refreshEditorFooter;
    private string editorClickChoice = "copy";
    private Action? ensureCopyValue;
    private Func<bool>? validateCopyValue;
    private Action<VaultItem> BuildEntryDetails(StackPanel host, IContentProvider provider, VaultItem? existing)
    {
        var panel = new StackPanel();
        var selectedColor = existing?.Advanced?.Color ?? "default";
        panel.Children.Add(Text("Entry color", 11, true));
        var swatches = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) }; panel.Children.Add(swatches);
        var choices = new Dictionary<string, Button>();
        foreach (var pair in EntryColors.All)
        {
            var choice = Button("", () => { selectedColor = pair.Key; foreach (var button in choices) button.Value.Content = button.Key == selectedColor ? "✓" : ""; });
            choice.Width = 24; choice.Height = 24; choice.Padding = new Thickness(0); choice.Margin = new Thickness(0, 0, 8, 0); choice.FontSize = 10; choice.HorizontalContentAlignment = HorizontalAlignment.Center;
            choice.Background = pair.Key == "default" ? (Brush)FindResource("ControlSurface") : EntryColors.Brush(pair.Key);
            choice.Content = selectedColor == pair.Key ? "✓" : ""; choice.ToolTip = pair.Key == "default" ? "No color" : pair.Key;
            System.Windows.Automation.AutomationProperties.SetName(choice, "Entry color " + pair.Key); choices.Add(pair.Key, choice); swatches.Children.Add(choice);
        }
        var description = Input(panel, "Shown in the entry tooltip", existing?.Description ?? "", true); description.MaxLength = 2000;
        host.Children.Add(GroupedOptions("Color & description", panel));
        var modes = IsConnection(provider)
            ? new Dictionary<string, string> { ["Copy password"] = "copy", ["Run"] = "run", ["Run and copy password"] = "run-copy" }
            : new Dictionary<string, string> { ["Copy"] = "copy", ["Run"] = "run", ["Run and copy"] = "run-copy" };
        var initial = !IsShortcut(provider) ? "copy" : existing?.ClickBehavior is { } b && b != "auto" ? b : existing?.Field("copyOnLaunch") == "true" ? "run-copy" : "run";
        var behaviorHost = new StackPanel(); host.Children.Add(behaviorHost);
        var behavior = Select(behaviorHost, "When I click the entry", modes.Keys, modes.First(p => p.Value == initial).Key);
        behavior.ToolTip = "The right-side Run button remains available when the entry has an action.";
        editorClickChoice = initial;
        behavior.SelectionChanged += (_, _) => { editorClickChoice = modes[(string)behavior.SelectedItem]; refreshScriptFields?.Invoke(); refreshEditorFooter?.Invoke(); };
        refreshEntryDetails = action =>
        {
            behaviorHost.Visibility = IsConnection(provider) || provider is ShortcutProvider && action is { Enabled: true, Kind.Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        };
        refreshEntryDetails(existing?.Advanced?.Action);
        return item =>
        {
            item.Description = description.Text.Trim(); item.ClickBehavior = behaviorHost.Visibility == Visibility.Visible ? modes[(string)behavior.SelectedItem] : "copy";
            if (item.Advanced is not null || selectedColor != "default") { item.Advanced ??= new(); item.Advanced.Color = selectedColor; }
        };
    }

    private Func<Dictionary<string, string>> BuildScriptValues(StackPanel host, VaultItem? existing, Func<string> getType)
    {
        var fields = new StackPanel { Visibility = existing?.ScriptValues.Count > 0 ? Visibility.Visible : Visibility.Collapsed };
        var toggle = Button("Script fields · optional", () => fields.Visibility = fields.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible);
        System.Windows.Automation.AutomationProperties.SetName(toggle, "Entry script fields");
        toggle.ToolTip = "Encrypted values available to an explicitly enabled PowerShell action.";
        host.Children.Add(toggle); host.Children.Add(fields);
        var mapping = Text("", 11, true); fields.Children.Add(mapping);
        refreshScriptLabels = () => mapping.Text = getType() == "sable.login" ? "$name = entry name · $value1 = username · $value2 = password" : getType() == "sable.secret" ? "$name = entry name · $value1 = secret" : "$name = entry name";
        refreshScriptLabels();
        var readers = new Dictionary<string, Func<string>>();
        var inputs = new Dictionary<string, PasswordBox>();
        int Next() => getType() == "sable.login" ? 3 : getType() == "sable.secret" ? 2 : 1;
        var add = Button("+ Add field", () => { });
        void Field(string name, string value)
        {
            var slot = new StackPanel(); var input = Password(slot, "$" + name); input.MaxLength = 100_000; input.Password = value;
            var remove = Button("Remove field", () => { input.Clear(); readers.Remove(name); inputs.Remove(name); fields.Children.Remove(slot); add.IsEnabled = readers.Count + Next() <= 20; });
            remove.FontSize = 10; remove.HorizontalAlignment = HorizontalAlignment.Right; remove.Margin = new Thickness(0, -10, 0, 4); slot.Children.Add(remove);
            readers[name] = () => input.Password; inputs[name] = input; fields.Children.Insert(fields.Children.Count - 1, slot);
            add.IsEnabled = readers.Count + Next() <= 20;
        }
        fields.Children.Add(add);
        foreach (var p in existing?.ScriptValues ?? []) Field(p.Key, p.Value);
        ensureCopyValue = () =>
        {
            if (getType() != "sable.shortcut" || editorClickChoice is not ("copy" or "run-copy")) return;
            if (!inputs.ContainsKey("value1")) Field("value1", "");
            fields.Visibility = Visibility.Visible;
        };
        validateCopyValue = () =>
        {
            if (getType() != "sable.shortcut" || editorClickChoice is not ("copy" or "run-copy")) return true;
            ensureCopyValue(); var input = inputs["value1"];
            if (input.Password.Length > 0) return true;
            void FocusValue()
            {
                if (!input.IsLoaded || !vault.IsUnlocked) return;
                input.Focus(); input.BringIntoView();
                if (FindParent<ScrollViewer>(input) is { } viewer)
                    viewer.ScrollToVerticalOffset(viewer.VerticalOffset + input.TranslatePoint(new Point(), viewer).Y - 24);
            }
            FocusValue(); RejectInput(input, "Enter the value to copy in $value1.");
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(FocusValue)); return false;
        };
        add.Click += (_, _) => { var number = Next(); while (readers.ContainsKey("value" + number)) number++; if (number <= 20) Field("value" + number, ""); };
        return () => readers.ToDictionary(p => p.Key, p => p.Value());
    }
}
