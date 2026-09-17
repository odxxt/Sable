using System.Text.Json;

namespace SableLatch.Desktop;

// The desktop editor registry is separate from content storage and clipboard semantics.
public interface IEntryEditorFactory
{
    string ContentType { get; }
    FrameworkElement Build(VaultItem? item, Action<VaultItem> save, Action cancel);
}

public partial class MainWindow
{
    private Func<VaultItem>? captureEditorDraft;
    private readonly Dictionary<string, VaultItem> editorDrafts = [];
    private bool editorIsNew;
    private AdvancedEntry? advancedDraft;
    private string? editorTestedAction;
    private void ShowEditor(VaultItem? existing = null, string? selectedType = null, bool? advanced = null)
    {
        if (!vault.IsUnlocked) return;
        if (screen == "editor" && editorIsNew && existing is null && captureEditorDraft is not null)
        {
            var draft = captureEditorDraft(); editorDrafts[draft.ContentType] = draft; advancedDraft = draft.Advanced;
        }
        editorIsNew = existing is null;
        advancedEditor = advanced ?? (existing is not null ? existing.Advanced?.Action?.Enabled == true : screen == "editor" && advancedEditor);
        var types = content.All.Where(p => !vault.Document!.Settings.DisabledModules.Contains(p.Descriptor.Id)).ToList();
        if (types.Count == 0) { ShowError("Enable at least one content module in Settings."); return; }
        var type = existing?.ContentType ?? selectedType ?? types[0].Descriptor.Id;
        var provider = content.Find(type);
        if (provider is null) { ShowError("This entry's editor is not installed. Its data is preserved."); return; }
        var advancedSwitch = new CheckBox { Content = "Advanced", IsChecked = advancedEditor, Style = (Style)FindResource("CompactSwitch"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        System.Windows.Automation.AutomationProperties.SetName(advancedSwitch, "Advanced options");
        advancedSwitch.Click += (_, _) => { if (advancedSwitch.IsChecked != advancedEditor) toggleAdvancedEditor?.Invoke(); };
        var stack = Form(existing is null ? "New entry" : "Edit entry", "", "editor", accessory: advancedSwitch);
        {
            var picker = new System.Windows.Controls.Primitives.UniformGrid { Columns = existing is null ? types.Count : 1, Margin = new Thickness(0, 0, 0, 16) };
            foreach (var option in types.Where(p => existing is null || p.Descriptor.Id == type))
            {
                var choice = Button(option.Descriptor.Name, () => { if (existing is null) ShowEditor(null, option.Descriptor.Id); });
                choice.HorizontalContentAlignment = HorizontalAlignment.Center;
                choice.Padding = new Thickness(5, 9, 5, 9); choice.Margin = new Thickness(2, 0, 2, 0);
                choice.Background = option.Descriptor.Id == type ? (Brush)FindResource("HoverSurface") : Brushes.Transparent;
                choice.Opacity = option.Descriptor.Id == type ? 1 : .55;
                picker.Children.Add(choice);
            }
            stack.Children.Add(picker);
        }
        if (!extensions.Editors.TryGetValue(type, out var editor)) { stack.Children.Add(Text("This content type needs its editor extension.")); return; }
        stack.Children.Add(editor.Build(existing ?? editorDrafts.GetValueOrDefault(type), item =>
        {
            EntryMetadata.MarkSaved(item, vault.Document!.Items.FirstOrDefault(i => i.Id == item.Id), DateTimeOffset.UtcNow);
            if (Change(d => { var index = d.Items.FindIndex(i => i.Id == item.Id); if (index < 0) d.Items.Add(item); else d.Items[index] = item; }))
            {
                if (item.Advanced?.Action is { Enabled: true } action && editorTestedAction == ActionApprovals.Fingerprint(action)) RememberAction(item.Id, action);
                ShowList(); SetStatus("Entry saved");
            }
        }, ShowList));
    }
    private FrameworkElement BuildFields(IContentProvider provider, VaultItem? existing, Action<VaultItem> save)
    {
        var stack = new StackPanel();
        var label = Input(stack, provider.Descriptor.Sensitive ? "Label" : "Label · optional", existing?.Label ?? "");
        var readers = new Dictionary<string, Func<string>>();
        var controls = new Dictionary<string, Control>();
        foreach (var field in provider.EditorFields)
        {
            if (field.Kind == EditorFieldKind.Secret)
            {
                stack.Children.Add(Text(field.Label, 11, true));
                var valueBox = new Grid { Margin = new Thickness(0, 0, 0, 13) }; stack.Children.Add(valueBox);
                var inputs = new StackPanel(); valueBox.Children.Add(inputs);
                var masked = new MaskedSecretBox { MinHeight = 36, MaxHeight = 180, Padding = new Thickness(10, 8, 38, 8), Password = existing?.Field(field.Key) ?? "", ToolTip = "Shift+Enter adds a line" };
                System.Windows.Automation.AutomationProperties.SetName(masked, field.Label); inputs.Children.Add(masked); eraseEditors.Add(masked.Clear);
                var revealed = new TextBox { Visibility = Visibility.Collapsed, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 36, MaxHeight = 180, MaxLength = 100_000, IsUndoEnabled = false, Padding = new Thickness(10, 8, 38, 8), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                System.Windows.Automation.AutomationProperties.SetName(revealed, field.Label + " revealed"); inputs.Children.Add(revealed); eraseEditors.Add(revealed.Clear);
                var eyeGraphic = new Grid { Width = 16, Height = 16, IsHitTestVisible = false };
                eyeGraphic.Children.Add(new TextBlock { Text = "\uE890", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                var eyeSlash = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 3,13 L 13,3"), Stroke = Brushes.White, StrokeThickness = 1.35, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Visibility = Visibility.Collapsed };
                eyeGraphic.Children.Add(eyeSlash);
                Button? eye = null;
                eye = Button("\uE890", () =>
                {
                    var show = revealed.Visibility != Visibility.Visible;
                    if (show) { revealed.Text = masked.Password; masked.Clear(); masked.Visibility = Visibility.Collapsed; revealed.Visibility = Visibility.Visible; controls[field.Key] = revealed; revealed.Focus(); }
                    else { masked.Password = revealed.Text; revealed.Clear(); masked.Visibility = Visibility.Visible; revealed.Visibility = Visibility.Collapsed; controls[field.Key] = masked; masked.Focus(); }
                    eyeSlash.Visibility = show ? Visibility.Visible : Visibility.Collapsed; eye!.ToolTip = show ? "Hide value" : "Reveal value";
                    System.Windows.Automation.AutomationProperties.SetName(eye, show ? "Hide " + field.Label : "Reveal " + field.Label);
                });
                eye.Content = eyeGraphic;
                eye.Style = (Style)FindResource("RowIcon"); eye.Width = 30; eye.Height = 32; eye.Margin = new Thickness(0, 2, 3, 0); eye.Opacity = .55;
                eye.HorizontalAlignment = HorizontalAlignment.Right; eye.VerticalAlignment = VerticalAlignment.Top; eye.ToolTip = "Reveal value";
                System.Windows.Automation.AutomationProperties.SetName(eye, "Reveal " + field.Label); valueBox.Children.Add(eye);
                readers[field.Key] = () => revealed.Visibility == Visibility.Visible ? revealed.Text : masked.Password;
                controls[field.Key] = masked;
            }
            else { var box = Input(stack, field.Label, existing?.Field(field.Key) ?? "", field.Kind == EditorFieldKind.Multiline); readers[field.Key] = () => box.Text; controls[field.Key] = box; }
        }
        Action refreshFooter = () => { };
        var advancedState = BuildAdvancedFields(stack, editorIsNew ? advancedDraft ?? existing?.Advanced : existing?.Advanced, () => refreshFooter(), provider.Descriptor.Id);
        if (editorIsNew) advancedState.Tested = editorTestedAction;
        VaultItem Capture()
        {
            var item = existing is null ? new VaultItem { ContentType = provider.Descriptor.Id, FolderId = folder, Order = vault.Document!.Items.Count } : Codec.Clone(existing);
            item.Label = label.Text.Trim(); item.IsSensitive = provider.Descriptor.Sensitive;
            item.Advanced = advancedState.Capture();
            item.Payload = JsonSerializer.SerializeToElement(readers.ToDictionary(x => x.Key, x => x.Value())); return item;
        }
        captureEditorDraft = Capture;
        void SaveFields()
        {
            var name = label.Text.Trim();
            if (provider.Descriptor.Sensitive && name.Length == 0) { RejectInput(label, "Add a label."); return; }
            var values = readers.ToDictionary(x => x.Key, x => x.Value());
            var missing = provider.EditorFields.FirstOrDefault(f => f.Required && values[f.Key].Length == 0);
            if (missing is not null) { controls[missing.Key].BringIntoView(); RejectInput(controls[missing.Key], "Enter " + missing.Label.ToLowerInvariant() + "."); return; }
            if (name.Length > 300 || values.Values.Any(v => v.Length > 100_000)) { ShowError("Use at most 300 characters for the label and 100,000 per field."); return; }
            if (!advancedState.Validate()) return;
            if (advancedState.NeedsTest) return;
            save(Capture());
        }
        void TestAction()
        {
            if (!vault.IsUnlocked || busy || modalDepth > 0 || !advancedState.Validate() || advancedState.Capture()?.Action is not { Enabled: true } action) return;
            void LaunchTest()
            {
                if (!LaunchEntryAction(action, advancedState.RejectFailure)) return;
                advancedState.Tested = editorTestedAction = AdvancedEditorState.Fingerprint(action);
                if (!editorIsNew && existing is not null) RememberAction(existing.Id, action);
                refreshFooter();
            }
            if (!editorIsNew && existing is not null && !actionApprovals.Contains(existing.Id, action)) ReviewFirstTest(action, LaunchTest);
            else LaunchTest();
        }
        var run = Button("Test action", TestAction); run.ToolTip = "Runs the configured action on your PC";
        var primary = Button("Save entry", () => { if (advancedState.NeedsTest) { RejectInput(run, "Test the action once before saving."); return; } SaveFields(); }, true);
        var footer = new Grid(); footer.ColumnDefinitions.Add(new()); footer.ColumnDefinitions.Add(new() { Width = new GridLength(8) }); footer.ColumnDefinitions.Add(new() { Width = new GridLength(2, GridUnitType.Star) });
        var testHost = new StackPanel { VerticalAlignment = VerticalAlignment.Top }; testHost.Children.Add(run); footer.Children.Add(testHost); Grid.SetColumn(primary, 2); footer.Children.Add(primary);
        foreach (var button in new[] { run, primary }) { button.Height = 38; button.Margin = new Thickness(0); button.Padding = new Thickness(10, 7, 10, 7); button.HorizontalContentAlignment = HorizontalAlignment.Center; button.VerticalAlignment = VerticalAlignment.Top; }
        run.Background = (Brush)FindResource("ControlSurface");
        refreshFooter = () =>
        {
            primary.Opacity = advancedState.NeedsTest ? .35 : 1;
            System.Windows.Automation.AutomationProperties.SetHelpText(primary, advancedState.NeedsTest ? "Test the action before saving" : "Save entry");
            run.Visibility = advancedEditor ? Visibility.Visible : Visibility.Collapsed;
            testHost.Visibility = run.Visibility;
            footer.ColumnDefinitions[0].Width = advancedEditor ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            footer.ColumnDefinitions[1].Width = new GridLength(advancedEditor ? 8 : 0);
            run.IsEnabled = advancedState.Capture()?.Action is { Enabled: true };
        };
        refreshFooter(); PageAction(footer); pageSubmit = primary;
        Dispatcher.BeginInvoke(() => label.Focus());
        return stack;
    }
    private void ShowTrash()
    {
        if (!vault.IsUnlocked) return;
        var s = vault.Document!.Settings;
        var stack = Form("Encrypted Trash.", s.ExpireTrash ? $"Only deleted entries expire, after {s.TrashDays} days. Older backups may still contain them." : "Automatic cleanup is disabled. Saved entries never expire.", "trash", parent: ShowDeletionSettings);
        var items = vault.Document.Items.Where(x => x.DeletedAt is not null).OrderByDescending(x => x.DeletedAt).ToList();
        if (items.Count == 0) stack.Children.Add(Text("Nothing here. Your saved entries are safe in the vault.", 12, true));
        foreach (var item in items)
        {
            stack.Children.Add(Text(string.IsNullOrWhiteSpace(item.Label) ? item.IsSensitive ? "Secret" : item.Field("text").Split('\n')[0] : item.Label));
            stack.Children.Add(Text("Deleted " + item.DeletedAt!.Value.ToLocalTime().ToString("g"), 10, true));
            var actions = new WrapPanel(); stack.Children.Add(actions);
            actions.Children.Add(Button("Restore", () => { if (Change(d => d.Items.Single(i => i.Id == item.Id).DeletedAt = null)) ShowTrash(); }));
            var armed = false; Button? delete = null;
            delete = Button("Delete forever", () =>
            {
                if (!armed) { armed = true; delete!.Content = "DELETE"; delete.Background = new SolidColorBrush(Color.FromRgb(170, 53, 67)); return; }
                if (Change(d => d.Items.RemoveAll(i => i.Id == item.Id))) { vault.CleanOrphanAttachments(); ShowTrash(); }
            }); actions.Children.Add(delete);
        }
    }
}
