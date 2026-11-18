using System.Text.Json;
using Forms = System.Windows.Forms;

namespace SableLatch.Desktop;

internal sealed record TypeHostSettings
{
    public TypeClipboardOptions Options { get; set; } = new();
    public bool Explained { get; set; }
    public bool Enabled { get; set; } = true;
    public string Package { get; set; } = "";
    public int TypingVersion { get; set; }
}

public partial class MainWindow
{
    private readonly Dictionary<string, TypeHostSettings> typeSettings = [];
    private NativeTyping? typing;
    private bool typeStarting;
#if DEBUG
    private Action<Window>? previewTypePromptShown;
#endif
    private Forms.ToolStripMenuItem? typeTray;
    private string TypeSettingsPath => Path.Combine(App.DataDirectory, "type-host.json");
    private void LoadTypeSettings()
    {
        typeSettings.Clear();
        try
        {
            if (!File.Exists(TypeSettingsPath) || new FileInfo(TypeSettingsPath).Length > 65536) return;
            foreach (var pair in Codec.Decode<Dictionary<string, TypeHostSettings>>(File.ReadAllBytes(TypeSettingsPath)))
            {
                if (typeSettings.Count >= 100 || pair.Value is null || pair.Value.Options is null) break;
                if (pair.Value.TypingVersion < 2 && pair.Value.Options.InputMode == "unicode") pair.Value.Options.InputMode = "auto";
                pair.Value.TypingVersion = 2;
                pair.Value.Options.Validate(); typeSettings[pair.Key] = pair.Value;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException or NullReferenceException) { typeSettings.Clear(); }
    }
    private void SaveTypeSettings()
    {
        var bytes = Codec.Encode(typeSettings); if (bytes.Length > 65536) throw new InvalidDataException("Too many Type preferences.");
        var temp = TypeSettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, bytes); File.Move(temp, TypeSettingsPath, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private TypeHostSettings TypeSettings(ModulePackage package) => typeSettings.TryGetValue(package.Manifest.Id, out var s) && s.Package == package.Fingerprint ? s : new() { Package = package.Fingerprint };
    private ModulePackage? TrayTypePackage => installedModules.FirstOrDefault(p => p.Manifest.Handler == "type" && typeSettings.TryGetValue(p.Manifest.Id, out var s) && s.Enabled && s.Package == p.Fingerprint);
    private void SyncTypeAvailability()
    {
        LoadTypeSettings();
        if (vault.IsUnlocked)
        {
            foreach (var package in installedModules.Where(p => p.Manifest.Handler == "type"))
            {
                var setting = TypeSettings(package); setting.Enabled = !vault.Document!.Settings.DisabledModules.Contains(package.Manifest.ContentType); typeSettings[package.Manifest.Id] = setting;
            }
            SaveTypeSettings();
        }
        if (typeTray is not null) typeTray.Visible = TrayTypePackage is not null;
    }
    private Func<TypeClipboardOptions> TypeOptionsForm(StackPanel host, TypeClipboardOptions initial, Action<Func<Dictionary<string, string>>>? draft = null)
    {
        var delay = Input(host, "Start delay · milliseconds", initial.StartDelayMs.ToString());
        var speed = Input(host, "Between characters · milliseconds", initial.CharacterDelayMs.ToString());
        var modes = new Dictionary<string, string> { ["Automatic · apps & remote control"] = "auto", ["Unicode text"] = "unicode", ["Keyboard compatibility"] = "keyboard" };
        var mode = Select(host, "Typing mode", modes.Keys, modes.First(p => p.Value == initial.InputMode).Key);
        mode.ToolTip = "Automatic sends normal keyboard events where possible, including VK and scan codes for Teams and browser consoles. Unicode handles characters unavailable in the destination layout. Keyboard compatibility rejects such characters.";
        var confirm = Check(host, "Confirm multiline text", initial.ConfirmMultiline);
        draft?.Invoke(() => new() { ["startDelay"] = delay.Text, ["characterDelay"] = speed.Text, ["inputMode"] = modes[(string)mode.SelectedItem], ["confirmMultiline"] = confirm.IsChecked == true ? "true" : "false", ["typingVersion"] = "2" });
        return () =>
        {
            if (!int.TryParse(delay.Text, out var start) || start is < 0 or > 10000) { delay.BringIntoView(); RejectInput(delay, "Use 0 to 10,000 milliseconds."); throw new InvalidDataException(); }
            if (!int.TryParse(speed.Text, out var gap) || gap is < 1 or > 1000) { speed.BringIntoView(); RejectInput(speed, "Use 1 to 1,000 milliseconds."); throw new InvalidDataException(); }
            return new() { StartDelayMs = start, CharacterDelayMs = gap, InputMode = modes[(string)mode.SelectedItem], ConfirmMultiline = confirm.IsChecked == true };
        };
    }
    private FrameworkElement BuildTypeFields(DeclarativeProvider provider, VaultItem? existing, Action<VaultItem> save)
    {
        var stack = new StackPanel();
        var label = Input(stack, "Name", existing?.Label ?? (editorSharedLabel.Length > 0 ? editorSharedLabel : "Type clipboard"));
        stack.Children.Add(Text("Click this entry, then choose where to type the current clipboard text. Esc stops typing.", 12, true));
        var global = TypeSettings(provider.Package).Options;
        var defaults = Check(stack, "Use module defaults", existing?.Field("custom") != "true");
        var custom = new StackPanel { Visibility = defaults.IsChecked == true ? Visibility.Collapsed : Visibility.Visible }; stack.Children.Add(custom);
        TypeClipboardOptions initial;
        try { initial = existing is null ? global : TypeClipboardOptions.From(existing, global); } catch (InvalidDataException) { initial = global; }
        Func<Dictionary<string, string>> captureOptions = () => [];
        var read = TypeOptionsForm(custom, initial, callback => captureOptions = callback);
        defaults.Checked += (_, _) => custom.Visibility = Visibility.Collapsed; defaults.Unchecked += (_, _) => custom.Visibility = Visibility.Visible;
        VaultItem Capture()
        {
            var item = existing is null ? new VaultItem { ContentType = provider.Descriptor.Id, FolderId = folder, Order = vault.Document!.Items.Count } : Codec.Clone(existing);
            item.Label = label.Text.Trim(); item.IsSensitive = false; item.ModuleSchema = provider.Package.Manifest.SchemaFingerprint();
            var values = captureOptions(); values["custom"] = defaults.IsChecked == true ? "false" : "true";
            item.Payload = JsonSerializer.SerializeToElement(values); return item;
        }
        captureEditorDraft = Capture;
        PageAction(Button("Save entry", () =>
        {
            if (label.Text.Trim().Length is < 1 or > 300) { RejectInput(label, "Use a name from 1 to 300 characters."); return; }
            try { if (defaults.IsChecked != true) read(); save(Capture()); } catch (InvalidDataException) { }
        }, true)); return stack;
    }
    private void ShowTypeModuleSettings(ModulePackage package)
    {
        var stack = Form("Type clipboard", "", "settings", parent: ShowModules);
        var settings = TypeSettings(package); var enabled = Check(stack, "Enabled", !vault.Document!.Settings.DisabledModules.Contains(package.Manifest.ContentType));
        stack.Children.Add(Text("Default timing for the tray action and entries using module defaults. These non-secret preferences are stored locally so typing works while locked.", 11, true));
        var read = TypeOptionsForm(stack, settings.Options);
        stack.Children.Add(Text("Preserves internal line breaks and tabs; removes trailing Enter/Tab characters. Never appends Enter. Esc, destination changes and Windows locking stop typing.", 11, true));
        SettingsSave(stack, Button("Save Type settings", () =>
        {
            try
            {
                var opts = read();
                if (!Change(d => { d.Settings.DisabledModules.Remove(package.Manifest.ContentType); if (enabled.IsChecked != true) d.Settings.DisabledModules.Add(package.Manifest.ContentType); })) return;
                settings.Options = opts; settings.TypingVersion = 2; settings.Enabled = enabled.IsChecked == true; typeSettings[package.Manifest.Id] = settings; SaveTypeSettings(); ReloadModules(); ShowModules();
            }
            catch (InvalidDataException) { }
            catch (IOException) { ShowError("Type settings could not be saved. Check this data folder's permissions."); }
        }, true));
        var armed = false; Button? remove = null;
        remove = Button("Uninstall module", () =>
        {
            if (!armed) { armed = true; remove!.Content = "Confirm uninstall"; return; }
            try { typing?.Cancel(); modules.Remove(package); ReloadModules(); ShowModules(); }
            catch (IOException) { ShowError("The module could not be removed."); }
        }); stack.Children.Add(remove);
    }
    private bool ConfirmType(bool firstUse, int length, bool multiline, string? error = null)
    {
        var dialog = new Window { Title = "Type clipboard", Width = 326, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false, Background = (Brush)FindResource("Surface"), Foreground = Brushes.White, FontFamily = FontFamily, FontSize = 12, Topmost = true };
        dialog.Loaded += (_, _) => PositionTypePrompt(dialog);
#if DEBUG
        dialog.ContentRendered += (_, _) => previewTypePromptShown?.Invoke(dialog);
#endif
        CaptureProtection.ConfigureWindow(dialog); dialog.SourceInitialized += (_, _) => CaptureProtection.Protect(new System.Windows.Interop.WindowInteropHelper(dialog).Handle);
        var body = new StackPanel { Margin = new Thickness(22) }; dialog.Content = body;
        dialog.PreviewKeyDown += (_, args) => { if (args.Key == Key.Escape) { dialog.DialogResult = false; args.Handled = true; } };
        body.Children.Add(Heading(error is null ? "Type clipboard" : "Typing stopped"));
        if (error is not null) body.Children.Add(Text(error, 12));
        if (firstUse) body.Children.Add(Text("Choose a destination after this message. Sable types the clipboard there, including any password you copied. Esc stops remaining typing; already typed text stays in the destination.", 12));
        if (multiline) body.Children.Add(Text($"Type {length:N0} characters with line breaks or tabs? Internal Enter and Tab keys may submit commands or move between fields. Trailing Enter/Tab keys are removed.", 12));
        var buttons = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        if (error is null) buttons.Children.Add(Button("Cancel", () => dialog.DialogResult = false));
        else buttons.Columns = 1;
        buttons.Children.Add(Button(error is null ? "Continue" : "Close", () => dialog.DialogResult = true, true)); body.Children.Add(buttons);
        return dialog.ShowDialog() == true;
    }
    private static void PositionTypePrompt(Window dialog)
    {
        var area = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
        var source = System.Windows.PresentationSource.FromVisual(dialog); var transform = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var bottomRight = transform.Transform(new Point(area.Right, area.Bottom)); var topLeft = transform.Transform(new Point(area.Left, area.Top));
        dialog.Left = Math.Max(topLeft.X, bottomRight.X - dialog.ActualWidth - 18);
        dialog.Top = Math.Max(topLeft.Y, bottomRight.Y - dialog.ActualHeight - 18);
    }
    private async void StartTypeClipboard(ModulePackage package, VaultItem? entry = null)
    {
        if (typeStarting || typing is not null || package.Manifest.Handler != "type") return;
        if (!installedModules.Any(p => p.Fingerprint == package.Fingerprint)) return;
        var settings = TypeSettings(package);
        if (!settings.Enabled || entry is null && TrayTypePackage is null) return;
        if (entry is not null && (!vault.IsUnlocked || !ModuleAvailable(entry) || vault.Document!.Settings.DisabledModules.Contains(entry.ContentType))) return;
        typeStarting = true; string text = "";
        var startEpoch = epoch;
        try
        {
            text = TypeClipboardText.Prepare(TypeClipboardReader.Read());
            var options = entry is null ? settings.Options with { } : TypeClipboardOptions.From(entry, settings.Options);
            options.Validate();
            var multiline = options.ConfirmMultiline && text.Any(c => c is '\n' or '\r' or '\t');
            if ((!settings.Explained || multiline) && !ConfirmType(!settings.Explained, text.Length, multiline)) return;
            if (epoch != startEpoch) return;
            if (!settings.Explained) { settings.Explained = true; typeSettings[package.Manifest.Id] = settings; SaveTypeSettings(); }
            Hide();
            using var operation = new NativeTyping(); typing = operation;
            var target = await operation.SelectTarget(); await operation.TypeTo(target, text, options);
            SetStatus("Clipboard typed");
        }
        catch (OperationCanceledException) { SetStatus("Typing cancelled"); }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or System.Runtime.InteropServices.ExternalException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        { ConfirmType(false, 0, false, ex is InvalidDataException or InvalidOperationException ? ex.Message : "Type clipboard could not start. Check clipboard access and Windows input permissions."); }
        finally { typing = null; typeStarting = false; text = ""; }
    }
}
