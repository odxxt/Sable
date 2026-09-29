namespace SableLatch.Desktop;

internal static class EntryColors
{
    public static readonly Dictionary<string, string> All = new()
    { ["default"] = "#00000000", ["rose"] = "#30D78D99", ["amber"] = "#30D4AD70", ["sage"] = "#308FBAA0", ["blue"] = "#308FACD7", ["violet"] = "#30B6A0D5" };
    public static Brush Brush(string? color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(All.GetValueOrDefault(color ?? "default", All["default"])));
}

internal sealed class AdvancedEditorState
{
    public required Func<AdvancedEntry?> Capture { get; init; }
    public required Func<bool> Validate { get; init; }
    public required Action<string> RejectFailure { get; init; }
    public string? Tested { get; set; }
    public static string Fingerprint(EntryAction action) => ActionApprovals.Fingerprint(action);
    public bool NeedsTest => Capture()?.Action is { Enabled: true } action && Tested != Fingerprint(action);
}

public partial class MainWindow
{
    private bool advancedEditor;
    private Action? toggleAdvancedEditor;
    private AdvancedEditorState BuildAdvancedFields(StackPanel stack, AdvancedEntry? existing, Action changed, string contentType)
    {
        var section = new StackPanel { Visibility = advancedEditor ? Visibility.Visible : Visibility.Collapsed };

        stack.Children.Add(Text("Entry color", 11, true));
        var selectedColor = existing?.Color ?? "default";
        var swatches = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) }; stack.Children.Add(swatches);
        var buttons = new Dictionary<string, Button>();
        foreach (var pair in EntryColors.All)
        {
            var choice = Button("", () => { selectedColor = pair.Key; foreach (var button in buttons) button.Value.Content = button.Key == selectedColor ? "✓" : ""; });
            choice.Width = 27; choice.Height = 25; choice.FontSize = 11; choice.Margin = new Thickness(0, 0, 7, 0); choice.Padding = new Thickness(0); choice.HorizontalContentAlignment = HorizontalAlignment.Center;
            choice.Background = pair.Key == "default" ? (Brush)FindResource("ControlSurface") : EntryColors.Brush(pair.Key); choice.Content = selectedColor == pair.Key ? "✓" : ""; choice.ToolTip = pair.Key;
            System.Windows.Automation.AutomationProperties.SetName(choice, "Entry color " + pair.Key); buttons.Add(pair.Key, choice); swatches.Children.Add(choice);
        }
        stack.Children.Add(section); var fields = section;
        var names = new Dictionary<string, string> { ["Choose an action…"] = "", ["Application"] = "application", ["Windows & Control Panel"] = "windows", ["Run"] = "run", ["PowerShell"] = "powershell", ["CMD"] = "cmd" };
        if (existing?.Action is { } unknown && unknown.Kind != "control-panel" && !names.ContainsValue(unknown.Kind)) names["Unavailable · " + unknown.Kind] = unknown.Kind;
        var kind = Select(fields, "Action", names.Keys, names.FirstOrDefault(p => p.Value == (existing?.Action?.Kind == "control-panel" ? "windows" : existing?.Action?.Kind)).Key ?? "Choose an action…");
        var details = new StackPanel(); fields.Children.Add(details);
        var targetLabel = Text("", 11, true); details.Children.Add(targetLabel);
        var target = new TextBox { Text = existing?.Action?.Target ?? "", MinHeight = 36, MaxHeight = 155, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 8), MaxLength = 16000 };
        System.Windows.Automation.AutomationProperties.SetName(target, "Action target or script"); details.Children.Add(target); eraseEditors.Add(target.Clear);
        var browse = Button("Choose application…", () => { }); details.Children.Add(browse);
        var argumentsHost = new StackPanel(); details.Children.Add(argumentsHost);
        var arguments = Input(argumentsHost, "Arguments · optional", existing?.Action?.Arguments ?? ""); arguments.MaxLength = 8000;
        var directoryHost = new StackPanel { Visibility = string.IsNullOrEmpty(existing?.Action?.WorkingDirectory) ? Visibility.Collapsed : Visibility.Visible };
        details.Children.Add(Button("Starting folder · optional", () => directoryHost.Visibility = directoryHost.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible));
        details.Children.Add(directoryHost);
        var directory = Input(directoryHost, "Working folder · optional", existing?.Action?.WorkingDirectory ?? ""); directory.MaxLength = 2048;
        directoryHost.Children.Add(Text("Used for relative file paths, such as .\\report.txt. Leave blank to start in your Windows user folder.", 11, true));
        var hint = Text("", 11, true); details.Children.Add(hint);
        var terminal = Check(details, "Keep terminal open when finished", existing?.Action?.KeepTerminalOpen ?? true);
        terminal.Checked += (_, _) => changed(); terminal.Unchecked += (_, _) => changed();
        var copyOnRun = existing?.Action?.CopyOnRun == true;
        var canCopy = contentType is "sable.text" or "sable.secret" or "sable.login";
        if (canCopy)
        {
            details.Children.Add(Text("When the action runs", 11, true));
            var modes = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
            var group = "copy-mode-" + Guid.NewGuid().ToString("N");
            var runOnly = new RadioButton { Content = "Run", GroupName = group, Style = (Style)FindResource("SegmentChoice"), IsChecked = !copyOnRun };
            var runCopy = new RadioButton { Content = "Run & copy", GroupName = group, Style = (Style)FindResource("SegmentChoice"), IsChecked = copyOnRun };
            runOnly.Checked += (_, _) => { copyOnRun = false; changed(); }; runCopy.Checked += (_, _) => { copyOnRun = true; changed(); };
            System.Windows.Automation.AutomationProperties.SetName(runOnly, "Action mode Run"); System.Windows.Automation.AutomationProperties.SetName(runCopy, "Action mode Run and copy");
            modes.Children.Add(runOnly); modes.Children.Add(runCopy);
            details.Children.Add(new Border { Background = (Brush)FindResource("ControlSurface"), CornerRadius = new CornerRadius(10), Padding = new Thickness(3), Margin = new Thickness(0, 0, 0, 8), Child = modes });
            details.Children.Add(Text(contentType == "sable.login" ? "Copies the username first. Click the entry again for the password." : contentType == "sable.secret" ? "Also copies the secret after the action starts." : "Also copies the text after the action starts.", 11, true));
        }
        var verify = Check(details, "Verify each time", existing?.Action?.VerifyEachTime ?? false);
        void BrowseFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Applications (*.exe)|*.exe", CheckFileExists = true };
            modalDepth++; try { if (dialog.ShowDialog(this) == true) { target.Text = dialog.FileName; arguments.Clear(); directory.Clear(); } } finally { modalDepth--; }
        }
        void ChooseDestination()
        {
            var id = names[(string)kind.SelectedItem];
            if (id is "windows" or "control-panel")
            {
                ShowChoicePicker("Windows & Control Panel", Task.FromResult(WindowsSettingsCatalog.Read()), choice => target.Text = choice.Target, settings: true); return;
            }
            async Task<IReadOnlyList<PickerChoice>> Load()
            {
                IReadOnlyList<InstalledApplication> apps;
#if DEBUG
                apps = App.PreviewMode ? new[] { new InstalledApplication("Fixture application", Environment.ProcessPath!) } : await ApplicationCatalog.ReadAsync();
#else
                apps = await ApplicationCatalog.ReadAsync();
#endif
                return apps.Select(a => new PickerChoice(a.Name, a.Path, a.Arguments, a.Directory, a.Path)).ToList();
            }
            ShowChoicePicker("Choose application", Load(), choice => { target.Text = choice.Target; arguments.Text = choice.Arguments; directory.Text = choice.Directory; }, BrowseFile);
        }
        browse.Click += (_, _) => ChooseDestination();
        void Refresh(bool clear)
        {
            var id = names[(string)kind.SelectedItem]; details.Visibility = id.Length == 0 ? Visibility.Collapsed : Visibility.Visible; var script = id is "cmd" or "powershell";
            if (clear) { target.Clear(); arguments.Clear(); directory.Clear(); terminal.IsChecked = script; directoryHost.Visibility = Visibility.Collapsed; }
            terminal.Visibility = script ? Visibility.Visible : Visibility.Collapsed;
            targetLabel.Text = id switch { "application" => "Application path", "windows" => "Settings destination", "control-panel" => "Control Panel name", "run" => "Run command", _ => "Script" };
            target.AcceptsReturn = id == "powershell"; target.MinHeight = script ? 85 : 36;
            browse.Visibility = id is "application" or "windows" or "control-panel" ? Visibility.Visible : Visibility.Collapsed;
            browse.Content = id switch { "windows" => "Choose a setting…", "control-panel" => "Choose Control Panel item…", _ => "Choose application…" };
            argumentsHost.Visibility = id == "application" ? Visibility.Visible : Visibility.Collapsed;
            hint.Text = id switch { "windows" => "Search Windows Settings and Control Panel together. Availability depends on your PC.", "control-panel" => "Choose an item above, or enter its canonical name.", "run" => "Like Windows+R: notepad, control printers, or %APPDATA%. Quote paths that contain spaces.", "cmd" => "One command line, or call a .cmd file. Test action runs it on your PC.", "powershell" => "Shift+Enter adds a line. Test action runs the script on your PC.", _ => "Test action opens the application before you save." };
            changed();
        }
        kind.SelectionChanged += (_, _) =>
        {
            Refresh(true);
            var requested = names[(string)kind.SelectedItem]; var owner = Body.Content;
            if (requested is "application" or "windows") Dispatcher.BeginInvoke(() => { if (ReferenceEquals(Body.Content, owner) && screen == "editor" && vault.IsUnlocked && kind.IsVisible && names[(string)kind.SelectedItem] == requested && advancedEditor) { kind.IsDropDownOpen = false; ChooseDestination(); } });
        }; Refresh(false);
        toggleAdvancedEditor = () =>
        {
            advancedEditor = !advancedEditor; section.Visibility = advancedEditor ? Visibility.Visible : Visibility.Collapsed; changed();
        };
        AdvancedEntry? Capture() => !advancedEditor && selectedColor == "default" && existing?.Action is null ? null : new AdvancedEntry
        {
            Color = selectedColor,
            Action = advancedEditor || existing?.Action is not null ? new EntryAction
            {
                Enabled = advancedEditor, Kind = names[(string)kind.SelectedItem], Label = "Run", Target = target.Text,
                Arguments = names[(string)kind.SelectedItem] == "application" ? arguments.Text : "", WorkingDirectory = directory.Text.Trim(), VerifyEachTime = verify.IsChecked == true, CopyOnRun = canCopy && copyOnRun,
                KeepTerminalOpen = names[(string)kind.SelectedItem] is "cmd" or "powershell" && terminal.IsChecked == true
            } : null
        };
        bool Invalid(Control field, string message) { if (field == directory) directoryHost.Visibility = Visibility.Visible; field.BringIntoView(); RejectInput(field, message); return false; }
        bool Validate()
        {
            var action = Capture()?.Action; if (action is not { Enabled: true }) return true;
            if (action.Kind.Length == 0) return Invalid(kind, "Choose an action type.");
            if (string.IsNullOrWhiteSpace(action.Target)) return Invalid(target, "Add a destination or script.");
            if (action.WorkingDirectory.Length > 0 && (!Path.IsPathFullyQualified(action.WorkingDirectory) || !Directory.Exists(action.WorkingDirectory))) return Invalid(directory, "Choose an existing full folder path.");
            try { EntryActions.Prepare(action); return true; }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException) { return Invalid(target, ex.Message); }
        }
        foreach (var box in new[] { target, arguments, directory }) box.TextChanged += (_, _) => { if (!clearingEditors) { box.SetResourceReference(Control.BackgroundProperty, "ControlSurface"); changed(); } };
        return new() { Capture = Capture, Validate = Validate, RejectFailure = message => Invalid(target, message), Tested = !editorIsNew && !editorIsDuplicate && existing?.Action is { Enabled: true } old ? AdvancedEditorState.Fingerprint(old) : null };
    }
}
