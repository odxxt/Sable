using System.Text.Json;
using System.Windows.Media.Animation;

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
    private string editorSharedLabel = "";
    private bool editorIsNew;
    private bool editorIsDuplicate;
    private string? editorTestedAction;
    private void ShowEditor(VaultItem? existing = null, string? selectedType = null, bool? advanced = null)
    {
        if (!vault.IsUnlocked) return;
        if (selectedType == "sable.text") selectedType = "sable.secret"; // Old callers/archives.
        if (existing is null && selectedType is null) { ShowEntryStarters(); return; }
        if (existing is null) KeepEditorDraft();
        editorIsNew = existing is null;
        refreshEntryDetails = null; refreshScriptFields = null; refreshScriptLabels = null;
        ensureCopyValue = null; validateCopyValue = null;
        editorIsDuplicate = existing is not null && !vault.Document!.Items.Any(i => i.Id == existing.Id);
        var types = content.All.Where(p => !vault.Document!.Settings.DisabledModules.Contains(p.Descriptor.Id)).ToList();
        if (types.Count == 0) { ShowError("Enable at least one content module in Settings."); return; }
        var type = existing?.ContentType ?? selectedType ?? types[0].Descriptor.Id;
        var draftItem = existing ?? editorDrafts.GetValueOrDefault(type);
        advancedEditor = advanced ?? draftItem?.Advanced?.Action?.Enabled == true;
        var provider = content.Find(type);
        if (vault.Document!.Settings.DisabledModules.Contains(type)) { ShowError("Enable this entry type in Settings first."); return; }
        if (provider is null || existing is not null && !ModuleAvailable(existing)) { ShowError("This entry's editor is not installed or its schema is incompatible. Its data is preserved."); return; }
        if (type == "sable.shortcut") advancedEditor = true;
        if (provider is DeclarativeProvider { Package.Manifest.Handler: "rdp" or "putty" or "type" }) advancedEditor = false;
        var title = provider.Descriptor.Id is "sable.secret" or "sable.login" ? "Secret or login" : TemplateName(provider);
        var stack = Form(existing is null ? title : editorIsDuplicate ? "Duplicate entry" : "Edit entry", "", "editor", parent: existing is null ? provider is DeclarativeProvider extra && extra.Package.Manifest.EffectiveCategory == "extras" ? ShowExtraStarters : IsConnection(provider) ? ShowConnectionStarters : ShowEntryStarters : ShowList, accessory: existing is null ? EntryChoices(provider, existing) : null, compact: true);
        if (!extensions.Editors.TryGetValue(type, out var editor)) { stack.Children.Add(Text("This content type needs its editor extension.")); return; }
        var details = new StackPanel();
        var applyDetails = BuildEntryDetails(details, provider, existing ?? editorDrafts.GetValueOrDefault(type));
        stack.Children.Add(editor.Build(existing ?? editorDrafts.GetValueOrDefault(type), item =>
        {
            applyDetails(item);
            if (IsShortcut(provider) && item.ClickBehavior != "auto" && item.Advanced?.Action is { } savedAction) savedAction.CopyOnRun = item.ClickBehavior == "run-copy";
            if (item.ClickBehavior is "run" or "run-copy" && !IsShortcut(provider) && item.Advanced?.Action is not { Enabled: true }) { ShowError("Add an action to use Run, or choose Copy."); return; }
            if (validateCopyValue?.Invoke() == false) return;
            EntryMetadata.MarkSaved(item, vault.Document!.Items.FirstOrDefault(i => i.Id == item.Id), DateTimeOffset.UtcNow);
            if (Change(d => { var index = d.Items.FindIndex(i => i.Id == item.Id); if (index < 0) { d.Items.Add(item); d.Settings.FirstEntryTourPending = false; } else d.Items[index] = item; }))
            {
                if (item.Advanced?.Action is { Enabled: true } action && editorTestedAction == ActionApprovals.Fingerprint(action)) RememberAction(item.Id, action);
                ShowList(); SetStatus("Entry saved");
            }
        }, ShowList));
        stack.Children.Add(details);
        var captureFields = captureEditorDraft;
        if (captureFields is not null) captureEditorDraft = () => { var draft = captureFields(); applyDetails(draft); return draft; };
    }
    private FrameworkElement BuildFields(IContentProvider provider, VaultItem? existing, Action<VaultItem> save)
    {
        var stack = new StackPanel();
        var label = Input(stack, provider.Descriptor.Sensitive || provider.Descriptor.Id == "sable.shortcut" ? "Name" : "Name · optional", editorIsNew ? editorSharedLabel : existing?.Label ?? "");
        var combined = provider.Descriptor.Id is "sable.secret" or "sable.login";
        var withUsername = provider.Descriptor.Id == "sable.login";
        var usernameHost = new StackPanel { Visibility = withUsername ? Visibility.Visible : Visibility.Collapsed };
        TextBox? username = null; TextBlock? secretCaption = null; Button? usernameToggle = null;
        FrameworkElement? secretSurface = null;
        var credentialsCaption = Text(withUsername ? "Username" : "Secret value", 11, true); credentialsCaption.Margin = new Thickness(0); credentialsCaption.VerticalAlignment = VerticalAlignment.Center;
        Action usernameChanged = () => { };
        Func<Dictionary<string, string>> readScriptValues = () => existing?.ScriptValues ?? [];
        if (combined)
        {
            usernameToggle = Button(withUsername ? "Remove username" : "Add username", () =>
            {
                if (!withUsername && readScriptValues().ContainsKey("value2"))
                {
                    RejectInput(usernameToggle!, "$value2 becomes the password. Move your custom $value2 to another script field before adding a username."); return;
                }
                withUsername = !withUsername;
                usernameHost.Visibility = withUsername ? Visibility.Visible : Visibility.Collapsed;
                usernameToggle!.Content = withUsername ? "Remove username" : "Add username";
                credentialsCaption.Text = withUsername ? "Username" : "Secret value";
                if (secretCaption is not null) { secretCaption.Text = "Password"; secretCaption.Visibility = withUsername ? Visibility.Visible : Visibility.Collapsed; }
                usernameChanged(); if (withUsername) username!.Focus();
                foreach (var updated in new FrameworkElement?[] { usernameHost, secretSurface, credentialsCaption, secretCaption })
                {
                    if (updated is null) continue;
                    updated.BeginAnimation(OpacityProperty, null);
                    if (updated.Visibility == Visibility.Visible && SystemParameters.ClientAreaAnimation)
                        updated.BeginAnimation(OpacityProperty, new DoubleAnimation(.4, 1, TimeSpan.FromMilliseconds(190)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
                }
            });
            usernameToggle.FontSize = 11; usernameToggle.Padding = new Thickness(8, 4, 8, 4); usernameToggle.Margin = new Thickness(0); usernameToggle.HorizontalAlignment = HorizontalAlignment.Right;
            usernameToggle.ToolTip = "Add a username to copy it first, then the password on the next click.";
            System.Windows.Automation.AutomationProperties.SetName(usernameToggle, "Toggle username");
            usernameToggle.IsEnabled = !vault.Document!.Settings.DisabledModules.Contains(withUsername ? "sable.secret" : "sable.login");
            var credentialsHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 4) }; DockPanel.SetDock(usernameToggle, Dock.Right); credentialsHeader.Children.Add(usernameToggle); credentialsHeader.Children.Add(credentialsCaption);
            stack.Children.Add(credentialsHeader); stack.Children.Add(usernameHost);
            username = Input(usernameHost, "Username", existing?.Field("username") ?? "");
            usernameHost.Children.RemoveAt(0);
        }
        string CurrentType() => combined ? withUsername ? "sable.login" : "sable.secret" : provider.Descriptor.Id;
        var formattedHtml = provider.Descriptor.Id is "sable.text" or "sable.secret" ? existing?.Field("html") ?? "" : "";
        var formattedPlain = existing?.Field("text") ?? "";
        var readers = new Dictionary<string, Func<string>>();
        var controls = new Dictionary<string, Control>();
        var editorFields = combined ? provider.EditorFields.Where(f => f.Kind == EditorFieldKind.Secret).ToArray() : provider.EditorFields.ToArray();
        foreach (var field in editorFields)
        {
            if (field.Kind == EditorFieldKind.Secret)
            {
                var caption = Text(combined ? withUsername ? "Password" : "Secret value" : field.Label, 11, true); stack.Children.Add(caption);
                caption.Margin = new Thickness(0, 0, 0, 4);
                if (combined) { secretCaption = caption; caption.Visibility = withUsername ? Visibility.Visible : Visibility.Collapsed; }
                var valueBox = new Grid { Margin = new Thickness(0, 0, 0, 8) }; stack.Children.Add(valueBox);
                if (combined) secretSurface = valueBox;
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
                if (provider.Descriptor.Id == "sable.secret" && field.Key == "text")
                {
                    var note = Text("Formatting kept", 10, true); note.ToolTip = "Copies the original formatting to apps that support it. Editing the value uses plain text.";
                    stack.Children.Add(note);
                    void UpdateNote() => note.Visibility = formattedHtml.Length > 0 && readers[field.Key]() == formattedPlain ? Visibility.Visible : Visibility.Collapsed;
                    void ReadFormatting(IDataObject data, string plain)
                    {
                        formattedHtml = ""; formattedPlain = plain;
                        try { if (data.GetData(DataFormats.Html, false) is string html) formattedHtml = FormattedText.ReadClipboardHtml(html); }
                        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or System.Text.DecoderFallbackException) { /* Keep the successfully pasted plain value. */ }
                        UpdateNote();
                    }
                    masked.WholeValuePasted = data => ReadFormatting(data, masked.Password);
                    DataObject.AddPastingHandler(revealed, (_, args) =>
                    {
                        if (revealed.Text.Length != revealed.SelectionLength || args.DataObject.GetData(DataFormats.UnicodeText, true) is not string plain || plain.Length > 100_000) return;
                        args.CancelCommand(); revealed.Text = plain; ReadFormatting(args.DataObject, plain); revealed.CaretIndex = plain.Length;
                    });
                    masked.TextChanged += (_, _) => UpdateNote(); revealed.TextChanged += (_, _) => UpdateNote(); UpdateNote();
                }
            }
            else
            {
                var box = Input(stack, field.Label, existing?.Field(field.Key) ?? "", field.Kind == EditorFieldKind.Multiline); readers[field.Key] = () => box.Text; controls[field.Key] = box;
                if (provider.Descriptor.Id == "sable.text" && field.Key == "text")
                {
                    var note = Text("Formatting preserved · editing uses plain text", 10, true);
                    note.Visibility = formattedHtml.Length > 0 ? Visibility.Visible : Visibility.Collapsed; stack.Children.Add(note);
                    box.TextChanged += (_, _) => { formattedHtml = ""; note.Visibility = Visibility.Collapsed; };
                    DataObject.AddPastingHandler(box, (_, args) =>
                    {
                        if (!args.DataObject.GetDataPresent(DataFormats.Html, false) || box.Text.Length != box.SelectionLength) return;
                        try
                        {
                            if (args.DataObject.GetData(DataFormats.Html, false) is not string input) return;
                            var html = FormattedText.ReadClipboardHtml(input);
                            var text = args.DataObject.GetData(DataFormats.UnicodeText, true) as string;
                            if (text is null || text.Length > 100_000) return;
                            args.CancelCommand(); box.Text = text; formattedHtml = html;
                            note.Visibility = html.Length > 0 ? Visibility.Visible : Visibility.Collapsed; box.CaretIndex = box.Text.Length;
                        }
                        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or System.Text.DecoderFallbackException)
                        { args.CancelCommand(); RejectInput(box, "This formatted paste is too large or invalid. Paste plain text instead."); }
                    });
                }
            }
        }
        Action refreshFooter = () => { };
        var advancedState = BuildAdvancedFields(stack, existing?.Advanced, () => refreshFooter(), provider.Descriptor.Id);
        var scriptHost = new StackPanel(); stack.Children.Add(scriptHost);
        var scriptValues = BuildScriptValues(scriptHost, existing, CurrentType); readScriptValues = scriptValues;
        usernameChanged = () =>
        {
            advancedState.Tested = null; editorTestedAction = null; refreshScriptLabels?.Invoke(); refreshFooter();
            foreach (var control in LogicalDescendants(stack).OfType<Control>().Where(c => c is MaskedSecretBox || c is TextBox && System.Windows.Automation.AutomationProperties.GetName(c).EndsWith(" revealed", StringComparison.Ordinal)))
                System.Windows.Automation.AutomationProperties.SetName(control, (withUsername ? "Password" : "Secret value") + (control is TextBox ? " revealed" : ""));
        };
        refreshScriptFields = () =>
        {
            var action = advancedState.Capture()?.Action;
            scriptHost.Visibility = action is { Enabled: true, Kind: "powershell" } || provider is ShortcutProvider && action is { Enabled: true, Kind.Length: > 0 } && editorClickChoice is "copy" or "run-copy" ? Visibility.Visible : Visibility.Collapsed;
            var toggle = LogicalDescendants(scriptHost).OfType<Button>().First();
            toggle.Content = action?.Kind == "powershell" ? "Script fields · optional" : "Copy value";
            ensureCopyValue?.Invoke();
        };
        if (editorIsNew) advancedState.Tested = editorTestedAction;
        VaultItem Capture()
        {
            var item = existing is null ? new VaultItem { ContentType = provider.Descriptor.Id, FolderId = folder, Order = vault.Document!.Items.Count } : Codec.Clone(existing);
            item.ContentType = CurrentType();
            item.Label = label.Text.Trim(); item.IsSensitive = provider.Descriptor.Sensitive;
            item.ScriptValues = scriptValues(); if (item.ScriptValues.Count > 0) item.IsSensitive = true;
            if (provider is DeclarativeProvider module) item.ModuleSchema = module.Package.Manifest.SchemaFingerprint();
            item.Advanced = advancedState.Capture();
            var values = readers.ToDictionary(x => x.Key, x => x.Value());
            if (combined)
            {
                var secret = values[editorFields[0].Key]; values.Clear();
                if (withUsername) { values["username"] = username!.Text; values["password"] = secret; }
                else values["text"] = secret;
            }
            if (provider.Descriptor.Id is "sable.text" or "sable.secret" && formattedHtml.Length > 0 && values.GetValueOrDefault("text") == formattedPlain) values["html"] = formattedHtml;
            item.Payload = JsonSerializer.SerializeToElement(values); return item;
        }
        captureEditorDraft = Capture;
        void SaveFields()
        {
            var name = label.Text.Trim();
            if ((provider.Descriptor.Sensitive || provider.Descriptor.Id == "sable.shortcut") && name.Length == 0) { RejectInput(label, "Add a label."); return; }
            var values = readers.ToDictionary(x => x.Key, x => x.Value());
            if (withUsername && string.IsNullOrWhiteSpace(username!.Text)) { username.BringIntoView(); RejectInput(username, "Enter a username, or remove the username field."); return; }
            var missing = editorFields.FirstOrDefault(f => f.Required && values[f.Key].Length == 0);
            if (missing is not null) { controls[missing.Key].BringIntoView(); RejectInput(controls[missing.Key], "Enter " + missing.Label.ToLowerInvariant() + "."); return; }
            if (name.Length > 300 || values.Values.Any(v => v.Length > 100_000) || withUsername && username!.Text.Length > 100_000) { ShowError("Use at most 300 characters for the name and 100,000 per field."); return; }
            if (!advancedState.Validate()) return;
            if (advancedState.NeedsTest) return;
            var item = Capture();
            try { ScriptBindings.For(item); } catch (InvalidDataException ex) { ShowError(ex.Message); return; }
            save(item);
        }
        void TestAction()
        {
            if (!vault.IsUnlocked || busy || modalDepth > 0 || !advancedState.Validate() || advancedState.Capture()?.Action is not { Enabled: true } action) return;
            try { ScriptBindings.For(Capture()); } catch (InvalidDataException ex) { ShowError(ex.Message); return; }
            async void LaunchTest()
            {
                if (!await LaunchEntryAction(action, advancedState.RejectFailure, Capture())) return;
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
            refreshEntryDetails?.Invoke(advancedState.Capture()?.Action); refreshScriptFields();
            primary.Opacity = advancedState.NeedsTest ? .35 : 1;
            System.Windows.Automation.AutomationProperties.SetHelpText(primary, advancedState.NeedsTest ? "Test the action before saving" : "Save entry");
            run.Visibility = advancedEditor ? Visibility.Visible : Visibility.Collapsed;
            testHost.Visibility = run.Visibility;
            footer.ColumnDefinitions[0].Width = advancedEditor ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            footer.ColumnDefinitions[1].Width = new GridLength(advancedEditor ? 8 : 0);
            run.IsEnabled = advancedState.Capture()?.Action is { Enabled: true };
        };
        refreshEditorFooter = refreshFooter; refreshFooter(); PageAction(footer); pageSubmit = primary;
        if (provider.Descriptor.Id == "sable.secret" && controls.GetValueOrDefault("text") is MaskedSecretBox secretInput && editorIsNew) GuideSecretFields(label, secretInput, primary);
        Dispatcher.BeginInvoke(() => { if (screen == "editor" && !contextOpen && label.IsVisible) { label.Focus(); FindParent<ScrollViewer>(label)?.ScrollToTop(); } });
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
