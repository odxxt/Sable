#if DEBUG
using System.Text.Json;
using System.Windows.Media.Imaging;
#endif

namespace SableLatch.Desktop;

#if DEBUG
internal sealed class PreviewDeviceProtector : IDeviceProtector
{
    private readonly byte[] key = Crypto.Random();
    public byte[] Protect(byte[] bytes) => Codec.Encode(Crypto.Seal(key, bytes, "disposable-preview"));
    public byte[] Unprotect(byte[] bytes) => Crypto.Open(key, Codec.Decode<SealedBox>(bytes), "disposable-preview");
}
internal sealed class PreviewClipboard : IClipboardService
{
    public string LastText { get; private set; } = "";
    public bool LastSensitive { get; private set; }
    public void Copy(CopyValue value, Preferences settings) { LastText = System.Text.Encoding.UTF8.GetString(value.Data); LastSensitive = value.Sensitive; }
    public void ClearOwned() => LastText = "";
    public void Dispose() => ClearOwned();
}
#endif

public partial class MainWindow
{
    public async void ShowPreview()
    {
#if DEBUG
        try
        {
            if (vault.Exists) throw new InvalidOperationException("Preview requires an empty, disposable data directory.");
            var output = Path.Combine(App.DataDirectory, "renders"); Directory.CreateDirectory(output);
            var checks = new List<string>();
            void CheckView(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add("PASS: " + name); }
            bool PressEnter(UIElement? target = null)
            {
                UpdateLayout();
                target ??= (UIElement?)Descendants<PasswordBox>(Body).LastOrDefault(b => b.IsVisible) ?? Descendants<TextBox>(Body).LastOrDefault(b => b.IsVisible) ?? (UIElement)Body;
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), Environment.TickCount, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                target.RaiseEvent(key); return key.Handled;
            }
            ShowUnlock(); await Render("01-setup");
            CheckView(Native.GetWindowDisplayAffinity(handle, out var mainAffinity) && mainAffinity == CaptureProtection.RequestedAffinity, "Windows confirms capture exclusion on the main panel HWND");
            CheckView(((SolidColorBrush)FindResource("Surface")).Color.A == 242 && ThemeCatalog.All().All(t => t.Surface.StartsWith("#F2", StringComparison.OrdinalIgnoreCase)), "Default and bundled themes use approximately 95 percent background opacity");
            CheckView(Descendants<Button>(Body).Any(b => b.Content as string == "Restore from backup") && Descendants<Button>(Body).Any(b => b.Content as string == "Create your vault  →") && !Descendants<Button>(Body).Any(b => b.Content as string == "Import an encrypted export  →"), "Welcome focuses on create and restore; import remains in settings");
            var setupDraft = new SetupDraft(); ShowSetupPassword(setupDraft); UpdateLayout();
            foreach (var input in Descendants<PasswordBox>(Body)) input.Password = "Setup-only-passphrase-2026";
            CheckView(PressEnter(), "Enter invokes the setup Next action");
            await Render("22-recovery-hidden");
            CheckView(screen == "recovery-key" && Descendants<TextBox>(Body).All(b => !b.IsVisible && b.Text.Length == 0), "Recovery key starts hidden without plaintext in the textbox");
            CheckView(Descendants<ScrollViewer>(Body).First().ScrollableHeight == 0, "Recovery key step fits without scrolling");
            Descendants<Button>(Body).Single(b => b.Content as string == "Reveal key").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Render("23-recovery-revealed");
            CheckView(Descendants<TextBox>(Body).Any(b => b.IsVisible && b.Text == setupDraft.Recovery), "Recovery key is revealed only on request");
            var copyKeyButton = Descendants<Button>(Body).Single(b => b.Content as string == "Copy key");
            copyKeyButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(copyKeyButton.Content is TextBlock { Text: "✓ Copied", Foreground: SolidColorBrush copiedInk } && copiedInk.Color.G > copiedInk.Color.R, "Recovery copy shows muted green confirmation");
            await Render("28-recovery-copied");
            GoBack(); UpdateLayout(); CheckView(Descendants<PasswordBox>(Body).All(b => b.Password == "Setup-only-passphrase-2026"), "Setup Back preserves the password draft");
            GoBack(); CheckView(screen == "setup" && !vault.Exists, "Setup Back reaches welcome without creating a vault");
            ShowSetupPassword(new SetupDraft()); UpdateLayout();
            foreach (var input in Descendants<PasswordBox>(Body)) input.Password = "Preview-only-passphrase-2026";
            await Render("26-setup-password");
            CheckView(PressEnter(), "Enter invokes the setup Next action");
            UpdateLayout(); Descendants<CheckBox>(Body).Single().IsChecked = true;
            CheckView(PressEnter(), "Enter invokes the setup Next action");
            await Render("27-setup-pin");
            CheckView(screen == "setup" && !vault.Exists, "Setup does not create files before its final step");
            GoBack(); UpdateLayout();
            CheckView(screen == "recovery-key" && Descendants<TextBox>(Body).All(b => !b.IsVisible), "PIN setup Back returns to the hidden recovery step");
            Descendants<CheckBox>(Body).Single().IsChecked = true;
            CheckView(PressEnter(), "Enter invokes the setup Next action");
            UpdateLayout(); foreach (var input in Descendants<PasswordBox>(Body)) input.Password = "803194";
            CheckView(PressEnter(), "Enter finishes setup from the PIN confirmation field");
            for (var wait = 0; wait < 100 && busy; wait++) await Task.Delay(100);
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            CheckView(vault.IsUnlocked && screen == "list" && vault.Document!.QuickUnlock is not null, "Setup Finish creates the vault, enrolls PIN and opens the list");
            vault.Mutate(d =>
            {
                d.Settings.Startup = false;
                d.Folders.Add(new() { Name = "Work", Order = 0 }); d.Folders.Add(new() { Name = "Personal", Order = 1 }); d.Folders.Add(new() { Name = "Ideas", Order = 2 });
                d.Items.Add(new() { Label = "Email address", Payload = JsonSerializer.SerializeToElement(new { text = "hello@example.com" }), Order = 0 });
                d.Items.Add(new() { Label = "GitHub", ContentType = "sable.login", IsSensitive = true, Payload = JsonSerializer.SerializeToElement(new { username = "sable-demo", password = "fictional-demo-secret" }), Order = 1 });
                d.Items.Add(new() { Label = "Wi-Fi password", ContentType = "sable.secret", IsSensitive = true, Payload = JsonSerializer.SerializeToElement(new { text = "fictional-wifi-secret" }), Order = 2 });
                d.Items.Add(new() { Label = "A thought for later", Payload = JsonSerializer.SerializeToElement(new { text = "Make space for the things worth keeping.\nOne small idea at a time." }), Order = 3 });
                d.Items.Add(new() { Label = "Meeting link", Payload = JsonSerializer.SerializeToElement(new { text = "https://example.com/meet" }), Order = 4 });
            });
            session.Unlock(); ShowList(); await Render("02-vault");
            CheckView(!Descendants<Button>(Body).Any(b => b.Content as string == "All items"), "All items is a plain label with no button hover");
            CheckView(BrandTitle.Effect is System.Windows.Media.Effects.DropShadowEffect shadow && shadow.ShadowDepth == 1, "Text has a subtle contrast shadow");
            Left = SystemParameters.WorkArea.Left + 90; Top = SystemParameters.WorkArea.Top + 80; ResetWindowPosition();
            CheckView(Math.Abs(Left - Math.Max(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Right - Width - 18)) < 1 && Math.Abs(Top - Math.Max(SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Bottom - Height - 18)) < 1, "Position reset returns to the default bottom-right placement");
            CheckView(!searchVisible && Descendants<TextBox>(Body).All(b => !b.IsVisible), "Search is hidden by default");
            CheckView(Status.Visibility == Visibility.Collapsed, "No permanent footer text");
            CheckView(entryList!.ActualHeight <= 252 && Descendants<ListBoxItem>(entryList).First().ActualHeight == 56, "List is capped at four and a half 56-pixel rows");
            var addButton = Descendants<Button>(Body).Single(b => b.Content as string == "＋  Add entry");
            var listEnd = entryList.TranslatePoint(new Point(0, entryList.ActualHeight), Body).Y;
            CheckView(addButton.TranslatePoint(new Point(), Body).Y - listEnd <= 8, "Add entry follows the viewport without a large blank gap");
            CheckView((Native.GetWindowLongPtr(handle, -20).ToInt64() & 0x80) != 0, "Unpinned window has the native Alt+Tab exclusion style");
            TogglePin(this, new RoutedEventArgs());
            CheckView((Native.GetWindowLongPtr(handle, -20).ToInt64() & 0x40080) == 0x40000, "Pinned window has the native Alt+Tab inclusion style");
            TogglePin(this, new RoutedEventArgs());
            PinButton.Focus(); TogglePin(this, new RoutedEventArgs()); TogglePin(this, new RoutedEventArgs());
            CheckView(!session.IsPinned && PinButton.Background is SolidColorBrush inactive && inactive.Color.A == 0, "Unpin removes active background even while focused");
            ToggleOrder(this, new RoutedEventArgs()); ToggleOrder(this, new RoutedEventArgs());
            CheckView(!rearranging && OrderButton.Background is SolidColorBrush orderInk && orderInk.Color.A == 0, "Rearrange button clears active state when switched off");
            ShowSettings(); ShowSecuritySettings(); GoBack(); UpdateLayout();
            var backPosition = Descendants<Button>(Body).Single(b => b.Content as string == "‹  Back").TranslatePoint(new Point(), Body).Y;
            ShowClipboardSettings(); UpdateLayout();
            CheckView(Descendants<Button>(Body).Single(b => b.Content as string == "‹  Back").TranslatePoint(new Point(), Body).Y == backPosition, "Settings Back remains at the same position across subsections");
            GoBack(); UpdateLayout();
            CheckView(Descendants<Button>(Body).Any(b => b.Content as string == "Security & locking  ›"), "Settings subsection Back returns to settings");
            GoBack(); UpdateLayout(); CheckView(screen == "list", "Settings root Back returns to entries");
            ShowSecuritySettings(); ShowChangePassword(); GoBack(); UpdateLayout();
            CheckView(Descendants<Button>(Body).Any(b => b.Content as string == "Change app password"), "Nested security Back returns to security settings");
            ShowList(); await Render("17-compact-list");
            var loginRow = entryList!.Items.Cast<RowModel>().Single(r => r.Item.ContentType == "sable.login");
            entryList.ScrollIntoView(loginRow); UpdateLayout();
            var loginContainer = (ListBoxItem)entryList.ItemContainerGenerator.ContainerFromItem(loginRow);
            var loginCopy = Descendants<Button>(loginContainer).Single(b => b.Tag as string == "copy");
            loginCopy.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Task.Delay(280);
            CheckView(((PreviewClipboard)clipboard).LastText == "sable-demo" && pendingLogin == loginRow.Item.Id && holdForPassword && Topmost && IsVisible && screen == "list", "First login click copies username, stays on list and holds topmost without pinning");
            await Render("09-username-copied");
            loginCopy.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Task.Delay(280);
            CheckView(((PreviewClipboard)clipboard).LastText == "fictional-demo-secret" && pendingLogin is null && !holdForPassword && !Topmost && !IsVisible, "Second login click copies password and hides the unpinned panel");
            Show(); session.IsPinned = true; Topmost = true; ShowList(); UpdateLayout();
            loginRow = entryList!.Items.Cast<RowModel>().Single(r => r.Item.ContentType == "sable.login");
            entryList.ScrollIntoView(loginRow); UpdateLayout();
            loginContainer = (ListBoxItem)entryList.ItemContainerGenerator.ContainerFromItem(loginRow);
            loginCopy = Descendants<Button>(loginContainer).Single(b => b.Tag as string == "copy");
            await CopyEntry(loginRow.Item, "Username", loginCopy); await CopyEntry(loginRow.Item, "Password", loginCopy);
            CheckView(IsVisible && Topmost && session.IsPinned, "Pinned login remains open after password copy");
            session.IsPinned = false; Topmost = false;
            ArmDelete(loginRow); UpdateLayout(); await Render("10-delete-confirmation");
            CheckView(vault.Document!.Items.Any(i => i.Id == loginRow.Item.Id) && Descendants<Button>(loginContainer).Any(b => b.Content as string == "DELETE" && b.IsVisible), "Arming deletion shows DELETE without removing the entry");
            Descendants<Button>(loginContainer).Single(b => b.Tag as string == "confirm-delete").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(vault.Document!.Items.Single(i => i.Id == loginRow.Item.Id).DeletedAt is not null, "DELETE confirmation sends a secret login to encrypted trash");
            vault.Mutate(d => d.Items.Single(i => i.Id == loginRow.Item.Id).DeletedAt = null);
            ShowEditor(vault.Document!.Items.First(i => i.ContentType == "sable.login")); await Render("03-login-editor");
            var save = Descendants<Button>(Body).Single(b => b.Content as string == "Save entry");
            var savePosition = save.TranslatePoint(new Point(), Body).Y;
            CheckView(save.IsVisible && FindParent<ScrollViewer>(save) is null && savePosition + save.ActualHeight <= Body.ActualHeight + 1, "Editor Save is always visible outside the scrolling form");
            ShowEditor(null, "sable.text"); UpdateLayout();
            CheckView(Descendants<Button>(Body).Single(b => b.Content as string == "Save entry").TranslatePoint(new Point(), Body).Y == savePosition, "Save position is consistent between login and text editors");
            ShowSettings(); await Render("04-settings");
            ShowClipboardSettings(); await Render("05-clipboard-settings");
            ShowList(); rearranging = true; ShowList(); await Render("06-rearrange");
            UpdateLayout();
            var itemBefore = vault.Document!.Items.Where(i => i.DeletedAt is null && i.FolderId == folder).OrderBy(i => i.Order).Select(i => i.Id).ToArray();
            var dropTarget = (ListBoxItem)entryList!.ItemContainerGenerator.ContainerFromIndex(2);
            var location = dropTarget.TranslatePoint(new Point(10, dropTarget.ActualHeight - 1), entryList);
            UpdateDropPoint(location); UpdateLayout();
            CheckView(insertionMarker is not null && insertionIndex == 3, "Drag shows an insertion marker after the lower half of the target row");
            await Render("11-insertion-marker"); PlaceRow(itemBefore[0], insertionIndex); ClearDropMarker();
            var itemAfter = vault.Document!.Items.Where(i => i.DeletedAt is null && i.FolderId == folder).OrderBy(i => i.Order).Select(i => i.Id).ToArray();
            CheckView(itemAfter[2] == itemBefore[0] && itemAfter[0] == itemBefore[1], "Drop inserts at the displayed position");
            ShowRecover(); await Render("07-recovery");
            ShowList();
            var secretCanaries = new[] { "fictional-demo-secret", "fictional-wifi-secret" };
            var labels = Descendants<TextBlock>(this).Select(x => x.Text).Concat(Descendants<TextBox>(this).Select(x => x.Text)).ToArray();
            if (labels.Any(x => secretCanaries.Any(x.Contains))) throw new InvalidOperationException("Secret leaked in list rendering.");
            ShowEditor(null, "sable.text"); UpdateLayout();
            var fields = Descendants<TextBox>(Body).ToList();
            fields.Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "Label · optional").Text = "View-save fixture";
            fields.Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "Text").Text = "First line\nSecond line";
            await Render("12-new-entry");
            CheckView(!Descendants<ComboBox>(Body).Any(c => c.IsVisible), "New entry uses type buttons instead of a dropdown");
            ShowEditor(null, "sable.login"); ShowEditor(null, "sable.text"); UpdateLayout();
            CheckView(Descendants<TextBox>(Body).Single(x => System.Windows.Automation.AutomationProperties.GetName(x) == "Text").Text == "First line\nSecond line", "Switching entry types preserves unfinished drafts");
            CheckView(PressEnter(), "Enter saves a multiline entry through the shared page action");
            if (!vault.Document!.Items.Any(i => i.Label == "View-save fixture" && i.Field("text") == "First line\nSecond line")) throw new InvalidOperationException("View save did not persist the multiline entry.");
            search = "View-save fixture"; ShowList();
            if (entryList!.Items.Count != 1) throw new InvalidOperationException("Search did not filter the list.");
            search = "";
            vault.Mutate(d => d.Settings.DisabledModules.Add("sable.secret")); ShowList();
            if (entryList!.Items.Cast<RowModel>().Single(r => r.Item.ContentType == "sable.secret").Available) throw new InvalidOperationException("Disabled module remained available.");
            vault.Mutate(d => d.Settings.DisabledModules.Clear());
            ShowAppearanceSettings(); UpdateLayout(); await Render("13-shortcut-recorder");
            CheckView(Descendants<Button>(Body).Any(b => b.ToolTip as string == "Record a shortcut") && !Descendants<TextBox>(Body).Any(), "Shortcut setting uses a record control");
            var recorder = Descendants<Button>(Body).Single(b => b.ToolTip as string == "Record a shortcut");
            recorder.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(recordingShortcut && recorder.Content as string == "Press keys…", "Shortcut recorder enters capture mode");
            Descendants<Button>(Body).Single(b => b.Content as string == "Restore default").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(!recordingShortcut && recorder.Content as string == "Ctrl+Alt+Space", "Restore default resets the shortcut capture control");
            vault.Mutate(d => d.QuickUnlock = PinConfiguration.Create("803194")); RefreshQuickUnlock();
            ShowList();
            TogglePin(this, new RoutedEventArgs());
            if (!session.IsPinned || !Topmost) throw new InvalidOperationException("Pin failed.");
            ForceLock("Locked"); await Render("08-locked");
            if (vault.Document is not null || entryList is not null || session.IsUnlocked) throw new InvalidOperationException("Lock retained active document or list references.");
            CheckView(screen == "pin" && quickUnlock?.Available == true, "Lock retains only a PIN ticket and shows typed PIN input");
            CheckView(!Descendants<System.Windows.Controls.Primitives.UniformGrid>(Body).Any() && !Descendants<Button>(Body).Any(b => b.Content as string == "Number pad"), "PIN screen has no number pad");
            var centered = Descendants<StackPanel>(Body).First();
            CheckView(centered.VerticalAlignment == VerticalAlignment.Center && Descendants<TextBlock>(Body).Any(t => t.Text == "Locked"), "Locked screen is centered and explicitly marked locked");
            CheckView(Descendants<PasswordBox>(Body).Single().Background is SolidColorBrush bare && bare.Color.A == 0, "PIN input has no filled box");
            await Render("14-centered-pin");
            Descendants<PasswordBox>(Body).Single().Password = "803194";
            PressEnter();
            for (var attempt = 0; attempt < 100 && (busy || !vault.IsUnlocked); attempt++) await Task.Delay(100);
            CheckView(vault.IsUnlocked && screen == "list" && session.IsUnlocked, "PIN unlock reopens the actual list after system-style locking");
            ShowPinSetup(true); await Render("15-pin-settings");
            var invalidField = Descendants<PasswordBox>(Body).First();
            RejectInput(invalidField, "Test invalid input");
            CheckView(invalidField.RenderTransform is TranslateTransform && invalidField.Background is SolidColorBrush brush && brush.Color.R > brush.Color.G, "Invalid input gets a shake transform and red feedback");
            await Render("16-invalid-input");
            var pinFields = Descendants<PasswordBox>(Body).ToList();
            pinFields.Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Current app password").Password = "Preview-only-passphrase-2026";
            pinFields.Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "PIN · 4–12 digits").Password = "6290";
            pinFields.Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Confirm PIN").Password = "6290";
            CheckView(PressEnter(), "Enter invokes Save PIN from the confirmation field");
            for (var attempt = 0; attempt < 100 && busy; attempt++) await Task.Delay(100);
            CheckView(screen == "list" && quickUnlock?.Available == true, "PIN settings save succeeds after full-password verification");
            ForceLock("Locked");
            await Task.Run(() => vault.UnlockWithPin("6290", quickUnlock!)); OnUnlocked(false);
            CheckView(vault.IsUnlocked && screen == "list", "New PIN from the settings form unlocks successfully");
            ForceLock("Locked");
            for (var wrong = 1; wrong <= 5; wrong++)
            {
                UpdateLayout();
                nextUnlockAttempt = default; // Skip only wall-clock delay; exercise actual handler and core attempt budget.
                Descendants<PasswordBox>(Body).Single().Password = "0000";
                PressEnter();
                var activePin = Descendants<PasswordBox>(Body).Single();
                CheckView(pinAttemptRunning && !activePin.IsEnabled, "PIN input is disabled synchronously when a submission starts");
                for (var burst = 0; burst < 10; burst++)
                {
                    activePin.Password = "0000";
                    PressEnter();
                }
                for (var wait = 0; wait < 100 && busy; wait++) await Task.Delay(100);
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                if (wrong < 5)
                {
                    CheckView(quickUnlock!.AttemptsRemaining == 5 - wrong && !vault.IsUnlocked, $"PIN failure {wrong} consumes exactly one attempt");
                    var text = Descendants<TextBlock>(Body).Select(t => t.Text).ToArray();
                    CheckView(!text.Any(t => t.Contains("4–12")) && (wrong == 1 ? !text.Any(t => t.Contains("left")) : text.Any(t => t == $"{5 - wrong} {(wrong == 4 ? "attempt" : "attempts")} left")), $"PIN failure {wrong} shows only the requested remaining-attempt hint");
                    PressEnter();
                    CheckView(quickUnlock.AttemptsRemaining == 5 - wrong, "Cooldown blocks rapid retries without changing the attempt budget");
                    if (wrong == 2) await Render("18-three-attempts");
                    for (var wait = 0; wait < 100 && pinAttemptRunning; wait++) await Task.Delay(50);
                    CheckView(activePin.Password.Length == 0 && activePin.IsEnabled, "PIN feedback finishes with an empty input before the next attempt");
                    ShowUnlock();
                }
            }
            CheckView(quickUnlock is null && screen == "unlock" && !vault.IsUnlocked, $"Fifth failed PIN requires the full password (screen={screen}, ticket={quickUnlock?.Available}, remaining={quickUnlock?.AttemptsRemaining}, unlocked={vault.IsUnlocked}, busy={busy})");
            await Render("19-password-required");
            CheckView(screen == "unlock", "Without a live process ticket the full password is required");
            await Task.Run(() => vault.Unlock("Preview-only-passphrase-2026")); OnUnlocked();
            var savedMetadata = vault.Document!.Items.Single(i => i.Label == "View-save fixture");
            CheckView(savedMetadata.CreatedAt is not null && savedMetadata.UpdatedAt is not null, "New entries persist creation and modification timestamps");
            CheckView(audit.Read().Any(r => r.Event == AuditEvent.PinExhausted), "PIN exhaustion is recorded in protected audit history");
            File.WriteAllText(Path.Combine(output, "audit-preview.html"), AuditReport.Build(vault.Document!, audit.Read()));
            ShowAudit(); await Render("20-audit-export");
            ShowMove(vault.Document!.Items.First()); await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout();
            CheckView(Descendants<ComboBox>(Body).Single().IsDropDownOpen, "Move to folder opens its choices immediately");
            Descendants<ComboBox>(Body).Single().IsDropDownOpen = false;
            ShowExport(); await Render("24-export");
            ShowRestore(); await Render("25-restore");
            ShowClipboardSettings(); UpdateLayout();
            var secondsInput = Descendants<TextBox>(Body).Single(); var originalSeconds = vault.Document!.Settings.ClipboardSeconds;
            secondsInput.Text = (originalSeconds + 1).ToString();
            var copyingSave = Descendants<Button>(Body).Single(b => b.Content as string == "Save copying settings");
            var copyingSaveY = copyingSave.TranslatePoint(new Point(), Body).Y;
            GoBack(); UpdateLayout();
            CheckView(ReferenceEquals(copyingSave, settingsSave) && discardArmed && draftHint!.IsVisible && copyingSave.RenderTransform is TranslateTransform, "First Back on changed settings stays and shakes Save with a discard hint");
            await Render("29-unsaved-settings");
            CheckView(Math.Abs(copyingSave.TranslatePoint(new Point(), Body).Y - copyingSaveY) <= 1 && FindParent<ScrollViewer>(copyingSave) is null, $"Settings Save stays fixed while the unsaved prompt appears (before={copyingSaveY}, after={copyingSave.TranslatePoint(new Point(), Body).Y})");
            await Render("29-unsaved-settings");
            secondsInput.Text = (originalSeconds + 2).ToString();
            CheckView(!discardArmed, "Further editing resets the discard confirmation");
            GoBack(); GoBack(); UpdateLayout();
            CheckView(vault.Document.Settings.ClipboardSeconds == originalSeconds && Descendants<Button>(Body).Any(b => b.Content as string == "Security & locking  ›"), "Second Back discards only unsaved settings and returns normally");
            ShowClipboardSettings(); UpdateLayout(); secondsInput = Descendants<TextBox>(Body).Single();
            secondsInput.Text = (originalSeconds + 1).ToString(); secondsInput.Text = originalSeconds.ToString(); GoBack(); UpdateLayout();
            CheckView(Descendants<Button>(Body).Any(b => b.Content as string == "Security & locking  ›"), "Reverting a setting does not trigger a false unsaved warning");
            ShowClipboardSettings(); UpdateLayout();
            Descendants<TextBox>(Body).Single().Text = (originalSeconds + 1).ToString();
            CheckView(PressEnter() && vault.Document.Settings.ClipboardSeconds == originalSeconds + 1, "Enter saves settings through the fixed footer action");
            vault.Mutate(d => d.Settings.ClipboardSeconds = originalSeconds);
            ShowList(); var revisionBeforeEnter = vault.Document.Revision;
            CheckView(!PressEnter(Body) && vault.Document.Revision == revisionBeforeEnter, "Leaving a form clears its Enter action instead of resubmitting an old page");
            ShowFolderEditor(vault.Document.Folders.First()); UpdateLayout();
            var iconOptions = Descendants<System.Windows.Controls.Primitives.UniformGrid>(Body).Single();
            CheckView(iconOptions.Visibility == Visibility.Collapsed, "Folder customization is initially collapsed");
            Descendants<Button>(Body).Single(b => b.Content as string == "Customize").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(iconOptions.Visibility == Visibility.Visible && Descendants<TextBox>(Body).Single().Text == "Work", "Folder customization expands in the existing page without losing its name");
            Descendants<Button>(Body).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Work icon").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Render("30-folder-customize");
            Descendants<Button>(Body).Single(b => b.Content as string == "Save folder").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(vault.Document.Folders.First().Icon == "work", "Selected folder icon is saved in the encrypted document");
            ShowBackupSettings(); await Render("31-scroll-top");
            var scroll = Descendants<ScrollViewer>(Body).First();
            var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(scroll);
            var edge = layer.GetAdorners(scroll).OfType<ScrollEdgeOverlay>().Single();
            CheckView(scroll.ScrollableHeight > 0 && !edge.HasAbove && edge.HasBelow, "Scrollable settings show only the lower cue at the top");
            var scrollbar = Descendants<System.Windows.Controls.Primitives.ScrollBar>(scroll).First(b => b.Orientation == Orientation.Vertical && b.IsVisible && FindParent<ScrollViewer>(b) == scroll);
            var track = (System.Windows.Controls.Primitives.Track)scrollbar.Template.FindName("PART_Track", scrollbar);
            var thumbTop = track.Thumb.TranslatePoint(new Point(), scrollbar).Y;
            CheckView(track.Orientation == Orientation.Vertical && thumbTop < 10, "Thin scrollbar thumb starts at the actual top rather than the middle");
            scroll.ScrollToVerticalOffset(scroll.ScrollableHeight / 2); await Render("32-scroll-middle");
            CheckView(edge.HasAbove && edge.HasBelow, "Middle of a page shows both scroll cues");
            scroll.ScrollToEnd(); await Render("33-scroll-bottom");
            CheckView(edge.HasAbove && !edge.HasBelow && track.Thumb.TranslatePoint(new Point(), scrollbar).Y > thumbTop, "Scroll thumb follows the content and the lower cue clears at the end");
            var offsetBeforeDrag = scroll.VerticalOffset;
            track.Thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(0, -20) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout();
            CheckView(scroll.VerticalOffset < offsetBeforeDrag, "Dragging the thin thumb actually scrolls the page");
            ShowSetupGuide(); await Render("34-setup-guide");
            CheckView(layer.GetAdorners(scroll) is null, "Unloaded scroll overlays are removed instead of retaining old pages");
            ShowGuideBackup(); UpdateLayout();
            CheckView(Descendants<CheckBox>(Body).Single().IsChecked == false && !vault.Document!.Settings.BackupEnabled, "Backup setup stays opt-in with its checkbox initially off");
            Descendants<CheckBox>(Body).Single().IsChecked = false;
            Descendants<Button>(Body).Single(b => b.Content as string == "Save & continue").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            for (var wait = 0; wait < 100 && busy; wait++) await Task.Delay(100);
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout();
            CheckView(vault.Document.Settings.CompletedSetupSteps.Contains("backups") && !vault.Document.Settings.BackupEnabled, "Setup guide saves an explicit backup preference before continuing");
            Descendants<TextBox>(Body).Single().Text = "14";
            Descendants<Button>(Body).Single(b => b.Content as string == "Save & continue").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            UpdateLayout(); CheckView(vault.Document.Settings.TrashDays == 14 && vault.Document.Settings.CompletedSetupSteps.Contains("trash"), "Setup guide saves Trash retention and records progress");
            await Render("35-guide-shortcut");
            Descendants<Button>(Body).Single(b => b.Content as string == "Save & continue").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            UpdateLayout(); CheckView(CompletedGuideSteps == 3, "Setup guide finishes after the three essential choices");
            ShowSettings(); UpdateLayout(); CheckView(!Descendants<Button>(Body).Any(b => b.Content as string == "Continue setup" || b.Content is StackPanel p && LogicalDescendants(p).OfType<TextBlock>().Any(t => t.Text == "Continue setup" || t.Text == "Finish setting up")), "Completed setup card disappears from Settings");
            CheckView(Descendants<Button>(Body).Any(b => b.Content as string == "Advanced  ›") && !Descendants<Button>(Body).Any(b => b.Content as string == "Import & export  ›"), "Settings separates everyday controls from advanced vault tools");
            ShowAdvancedSettings(); ShowTransfers(); GoBack(); UpdateLayout();
            CheckView(Descendants<Button>(Body).Any(b => b.Content as string == "Backups & restore  ›"), "Advanced subsection Back returns to the Advanced hub");
            await Render("36-advanced-settings");
            ShowSettings(); await Render("37-everyday-settings");
            foreach (var mode in new[] { "never", "always", "pinned" })
            {
                vault.Mutate(d => d.Settings.TaskbarMode = mode); session.IsPinned = false; ApplyTaskbarVisibility();
                CheckView(ShowInTaskbar == (mode == "always") && ((Native.GetWindowLongPtr(handle, -20).ToInt64() & 0x40000) != 0) == (mode == "always"), "Taskbar and native style agree for unpinned mode " + mode);
                session.IsPinned = true; ApplyTaskbarVisibility();
                CheckView(ShowInTaskbar == (mode != "never"), "Taskbar honors pinned mode " + mode);
            }
            session.IsPinned = false; Topmost = false; ApplyTaskbarVisibility();
            folder = vault.Document.Folders.First().Id;
            vault.Mutate(d => { for (var i = 0; i < 6; i++) d.Items.Add(new() { FolderId = folder, Label = "Folder note " + i, Payload = JsonSerializer.SerializeToElement(new { text = "Folder fixture" }), Order = i }); });
            ShowList(); await Render("38-folder-spacing");
            var folderAdd = Descendants<Button>(Body).Single(b => b.Content as string == "＋  Add entry");
            CheckView(Body.ActualHeight - folderAdd.TranslatePoint(new Point(0, folderAdd.ActualHeight), Body).Y < 5 && entryList!.ActualHeight <= 252, "Folder shell ends after Add entry and keeps the 4.5-row viewport");
            var listScroll = Descendants<ScrollViewer>(entryList!).First();
            var listEdge = System.Windows.Documents.AdornerLayer.GetAdornerLayer(listScroll).GetAdorners(listScroll).OfType<ScrollEdgeOverlay>().Single();
            CheckView(listEdge.HasBelow && !listEdge.HasAbove, "Folder list exposes a lower overflow cue");
            CheckView(!Descendants<Button>(Body).Any(b => b.Content as string == "Advanced"), "Advanced is absent from the list footer");
            Descendants<Button>(Body).Single(b => b.Content as string == "＋  Add entry").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); UpdateLayout();
            var advancedSwitch = LogicalDescendants(Body).OfType<CheckBox>().Single(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Advanced options");
            advancedSwitch.IsChecked = true; advancedSwitch.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); UpdateLayout();
            CheckView(FindParent<ScrollViewer>(advancedSwitch) is null && !Descendants<Button>(Body).Any(b => b.Content as string == "Advanced"), "Advanced is a fixed header switch, separate from content type tabs");
            await Render("44-advanced-switch");
            TextBox NamedInput(string name) => LogicalDescendants(Body).OfType<TextBox>().Single(t => System.Windows.Automation.AutomationProperties.GetName(t) == name);
            CheckView(advancedEditor && screen == "editor", "Advanced button opens the advanced entry editor");
            NamedInput("Label · optional").Text = "Custom note";
            NamedInput("Text").Text = "copy only this value";
            Descendants<Button>(Body).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Entry color sage").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout();
            var initialKind = LogicalDescendants(Body).OfType<ComboBox>().Single();
            CheckView(!initialKind.IsDropDownOpen && closeChoicePicker is null && !NamedInput("Action target or script").IsVisible, "Advanced reveals a closed dropdown with no unselected action settings");
            CheckView(!LogicalDescendants(Body).OfType<CheckBox>().Any(c => c.Content is TextBlock { Text: "Show an action button" }), "The redundant Show an action button checkbox is removed");
            initialKind.SelectedItem = "Application";
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout();
            CheckView(closeChoicePicker is not null && NamedInput("Find an application").IsKeyboardFocused, "Choosing Application opens its picker after the type is selected");
            closeChoicePicker!(); UpdateLayout();
            var launchCount = 0; previewActionLaunch = _ => launchCount++;
            Button TestButton() => Descendants<Button>(Body).Single(b => b.Content as string == "Test action");
            void ClickTest() => TestButton().RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(pageSubmit!.Content as string == "Save entry" && pageSubmit.Opacity < .5, "Untested actions keep Save entry named consistently and dimmed");
            PressEnter(NamedInput("Text"));
            CheckView(launchCount == 0 && TestButton().RenderTransform is TranslateTransform && screen == "editor", "Pressing dimmed Save shakes Test action without launching or saving");
            var pathInput = NamedInput("Action target or script"); ClickTest(); UpdateLayout();
            CheckView(pathInput.RenderTransform is TranslateTransform && NamedInput("Label · optional").RenderTransform is not TranslateTransform && launchCount == 0, "Missing action path shakes the path field and prevents launch");
            ClickTest(); UpdateLayout();
            CheckView(!Descendants<TextBlock>(Body).Any(t => t.Text == "Add a destination or script." && t.IsVisible), "First two field failures show animation without inline error text");
            ClickTest(); UpdateLayout();
            CheckView(Descendants<TextBlock>(Body).Any(t => t.Text == "Add a destination or script." && t.IsVisible), "Third field failure explains the problem under the field");
            Descendants<Button>(Body).Single(b => b.Content as string == "Choose application…").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout();
            CheckView(closeChoicePicker is not null && modalDepth == 1 && NamedInput("Find an application").IsKeyboardFocused, "Application picker is highlighted and focuses its fixed search field");
            CheckView(Descendants<Image>(Body).Any(i => i.IsVisible && i.Source is not null), "Application choices display extracted small application icons");
            CheckView(Descendants<ScrollViewer>(Body).Where(s => !s.IsVisible).All(s => (System.Windows.Documents.AdornerLayer.GetAdornerLayer(s)?.GetAdorners(s) ?? []).OfType<ScrollEdgeOverlay>().All(e => !e.HasAbove && !e.HasBelow)), "Covered editor scroll cues stay hidden behind the picker");
            await Render("43-application-picker");
            CheckView(Descendants<Button>(Body).Any(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Fixture application") && Descendants<Button>(Body).Any(b => b.Content as string == "Browse…"), "Application picker offers detected apps and a separate Browse fallback");
            Descendants<Button>(Body).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Fixture application").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(pathInput.Text == Environment.ProcessPath, "Selecting an application fills its executable path");
            CheckView(closeChoicePicker is null && modalDepth == 0, "Selecting an app closes the overlay and resumes the unchanged editor");
            LogicalDescendants(Body).OfType<ComboBox>().Single().SelectedItem = "Windows & Control Panel"; UpdateLayout();

            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout();
            CheckView(closeChoicePicker is not null && NamedInput("Find a setting").IsKeyboardFocused, "Selecting Windows and Control Panel opens the combined picker immediately");
            CheckView(Descendants<Button>(Body).Count(b => System.Windows.Automation.AutomationProperties.GetName(b).Contains(" · ")) == 6, "Combined picker starts with six useful destinations instead of the full catalog");
            NamedInput("Find a setting").Text = "Sound"; UpdateLayout();
            CheckView(Descendants<Button>(Body).Any(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Sound · Windows Settings") && Descendants<Button>(Body).Any(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Sound · Control Panel"), "Combined search distinguishes both sound destinations by source");
            await Render("45-settings-search");
            Descendants<Button>(Body).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Sound · Control Panel").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(pathInput.Text == "Microsoft.Sound" && closeChoicePicker is null && modalDepth == 0, "Control Panel picker searches friendly names and fills the selected destination");
            LogicalDescendants(Body).OfType<ComboBox>().Single().SelectedItem = "Application"; UpdateLayout();
            Descendants<Button>(Body).Single(b => b.Content as string == "Choose application…").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout();
            Descendants<Button>(Body).Single(b => b.Content as string == "Cancel").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(closeChoicePicker is null && modalDepth == 0 && screen == "editor" && pathInput.Text.Length == 0, "Cancel closes the app picker without selecting or changing the draft");
            var actionSelect = LogicalDescendants(Body).OfType<ComboBox>().Single();
            var beforeWheel = actionSelect.SelectedItem;
            var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent, Source = actionSelect };
            actionSelect.RaiseEvent(wheel);
            CheckView(wheel.Handled && Equals(beforeWheel, actionSelect.SelectedItem) && closeChoicePicker is null, "Mouse wheel over a closed dropdown scrolls without changing the action or opening a picker");
            actionSelect.IsDropDownOpen = true; UpdateLayout();
            wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent, Source = actionSelect }; actionSelect.RaiseEvent(wheel);
            CheckView(wheel.Handled && Equals(beforeWheel, actionSelect.SelectedItem), "Mouse wheel over an open action menu does not select another action");
            actionSelect.IsDropDownOpen = false;
            NamedInput("Arguments · optional").Text = "old args"; NamedInput("Working folder · optional").Text = "old folder";
            LogicalDescendants(Body).OfType<ComboBox>().Single().SelectedItem = "PowerShell";
            CheckView(pathInput.Text.Length == 0 && NamedInput("Arguments · optional").Text.Length == 0 && NamedInput("Working folder · optional").Text.Length == 0, "Changing action type clears target, arguments and working folder");
            CheckView(!LogicalDescendants(Body).OfType<TextBox>().Any(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Button tooltip"), "Action labels are fixed to Run without a tooltip input");
            NamedInput("Action target or script").Text = "Write-Output 'fictional-action'";
            UpdateLayout(); var advancedScroll = Descendants<ScrollViewer>(Body).First(); advancedScroll.ScrollToEnd(); await Render("39-advanced-editor");
            CheckView(pageSubmit!.IsVisible && FindParent<ScrollViewer>(pageSubmit) is null, "Advanced entry Save remains fixed outside the scrolling options");
            ShowEditor(null, "sable.login"); UpdateLayout();
            CheckView(NamedInput("Action target or script").Text == "Write-Output 'fictional-action'", "Changing content type preserves advanced color and action drafts");
            ShowEditor(null, "sable.text"); UpdateLayout();
            CheckView(NamedInput("Text").Text == "copy only this value", "Changing type back preserves the text draft");
            previewActionLaunch = _ => throw new System.ComponentModel.Win32Exception("Fixture launch failed.");
            ClickTest();
            CheckView(pageSubmit!.Opacity < .5 && launchCount == 0 && NamedInput("Action target or script").RenderTransform is TranslateTransform, "A failed launch keeps Save dimmed and marks the action field");
            previewActionLaunch = _ => launchCount++;
            ClickTest();
            CheckView(launchCount == 1 && screen == "editor" && pageSubmit!.Content as string == "Save entry" && pageSubmit.Opacity == 1, "Testing enables Save entry without renaming either button");
            NamedInput("Action target or script").Text += " ";
            CheckView(pageSubmit!.Opacity < .5 && pageSubmit.Content as string == "Save entry", "Changing a tested script dims Save again");
            NamedInput("Action target or script").Text = "Write-Output 'fictional-action'";
            CheckView(pageSubmit.Opacity == 1, "Unchanged tested action does not require a redundant test");
            CheckView(PressEnter(NamedInput("Text")), "Enter saves the tested advanced entry");
            var custom = vault.Document.Items.Single(i => i.Label == "Custom note");
            CheckView(custom.Advanced is { Color: "sage", Action.Kind: "powershell" } && custom.FolderId == folder, "Advanced save persists color, action and current folder");
            entryList!.ScrollIntoView(entryList.Items.Cast<RowModel>().Single(r => r.Item.Id == custom.Id)); await Render("40-advanced-row");
            var customRow = entryList.Items.Cast<RowModel>().Single(r => r.Item.Id == custom.Id);
            var customContainer = (ListBoxItem)entryList.ItemContainerGenerator.ContainerFromItem(customRow);
            CheckView(Descendants<Button>(customContainer).Any(b => b.Tag as string == "entry-action" && b.IsVisible), "Custom row has a separate action button beside Edit");
            vault.Mutate(d => d.Settings.CloseAfterCopy = false);
            await CopyEntry(custom, "Copy", Descendants<Button>(customContainer).Single(b => b.Tag as string == "copy"));
            CheckView(((PreviewClipboard)clipboard).LastText == "copy only this value" && screen == "list", "Copying an advanced entry copies only its payload and never opens the action");
            ReviewEntryAction(custom);
            CheckView(launchCount == 2 && screen == "list", "Verify off launches directly without a destination page");
            vault.Mutate(d => d.Items.Single(i => i.Id == custom.Id).Advanced!.Action!.VerifyEachTime = true);
            custom = vault.Document.Items.Single(i => i.Id == custom.Id);
            ReviewEntryAction(custom); await Render("41-action-review");
            CheckView(pageSubmit is null && !PressEnter(Body) && screen == "entry-action", "Action review requires an explicit Run button instead of form Enter");
            GoBack(); CheckView(screen == "list", "Backing out of an action review returns without executing");
            var transferred = Codec.Clone(custom); transferred.Id = Guid.NewGuid(); transferred.Label = "Transferred action"; transferred.Advanced!.Action!.VerifyEachTime = false;
            vault.Mutate(d => d.Items.Add(transferred)); var beforeReview = launchCount;
            ReviewEntryAction(transferred); UpdateLayout();
            CheckView(screen == "entry-action" && launchCount == beforeReview && Descendants<TextBlock>(Body).Any(t => t.Text == "First run on this PC"), "Unapproved transferred action shows first-run review before executing");
            await Render("46-first-run-review");
            var cancelledRun = Descendants<Button>(Body).Single(b => b.Content as string == "Run action");
            GoBack(); cancelledRun.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(launchCount == beforeReview, "A queued click on a cancelled review cannot execute the action");
            CheckView(!actionApprovals.Contains(transferred.Id, transferred.Advanced.Action), "Cancelling first-run review does not grant execution consent");
            ReviewEntryAction(transferred); UpdateLayout();
            previewActionLaunch = _ => throw new System.ComponentModel.Win32Exception("Fixture launch failed");
            Descendants<Button>(Body).Single(b => b.Content as string == "Run action").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(!actionApprovals.Contains(transferred.Id, transferred.Advanced.Action) && screen == "entry-action", "Failed first launch never records local consent");
            previewActionLaunch = _ => launchCount++;
            Descendants<Button>(Body).Single(b => b.Content as string == "Run action").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(launchCount == beforeReview + 1 && screen == "list" && actionApprovals.Contains(transferred.Id, transferred.Advanced.Action), "Explicit first-run confirmation records consent after successful launch");
            ReviewEntryAction(transferred);
            CheckView(launchCount == beforeReview + 2 && screen == "list", "A second unchanged run on this device no longer asks for verification");
            vault.Mutate(d => d.Items.Single(i => i.Id == transferred.Id).Advanced!.Action!.Target += " changed");
            transferred = vault.Document.Items.Single(i => i.Id == transferred.Id);
            ReviewEntryAction(transferred);
            CheckView(screen == "entry-action" && launchCount == beforeReview + 2, "Changing an approved command requires review again");
            GoBack(); ShowEditor(transferred); UpdateLayout();
            ClickTest(); UpdateLayout();
            CheckView(closeChoicePicker is not null && modalDepth == 1 && launchCount == beforeReview + 2, "Testing an unapproved existing entry also requires first-run review");
            var cancelledTest = Descendants<Button>(Body).Single(b => b.IsVisible && b.Content as string == "Test action");
            closeChoicePicker!(); UpdateLayout(); cancelledTest.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(launchCount == beforeReview + 2, "A queued click on a cancelled test review cannot execute the action");
            CheckView(screen == "editor" && !actionApprovals.Contains(transferred.Id, transferred.Advanced!.Action!), "Cancelling test review preserves the editor without consent");
            ClickTest(); UpdateLayout();
            Descendants<Button>(Body).Single(b => b.IsVisible && b.Content as string == "Test action").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(screen == "editor" && closeChoicePicker is null && modalDepth == 0 && actionApprovals.Contains(transferred.Id, transferred.Advanced!.Action!), "Confirmed test returns to the editor and grants local consent");
            ShowList();
            var catalog = WindowsSettingsCatalog.Read();
            CheckView(catalog.Count > 180 && WindowsSettingsCatalog.Search(catalog, "credential").Any(c => c.Target == "Microsoft.CredentialManager"), "Search includes uncommon Control Panel items in a broad compact catalog");
            CheckView(WindowsSettingsCatalog.Search(catalog, "microphone privacy").Any(c => c.Target == "ms-settings:privacy-microphone") && WindowsSettingsCatalog.Search(catalog, "wifi").Any(c => c.Target == "ms-settings:network-wifi"), "Settings search accepts multiword and alternate keyword queries");
            CheckView(catalog.Select(c => c.Target).Distinct().Count() == catalog.Count && catalog.All(c => EntryActions.Prepare(new EntryAction { Kind = "windows", Target = c.Target }) is not null), "Every built-in destination has a unique valid Windows launch plan");
            ShowEditor(advanced: false); UpdateLayout();
            NamedInput("Text").Text = "ordinary colored text";
            Descendants<Button>(Body).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Entry color blue").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            PressEnter(NamedInput("Text"));
            var colored = vault.Document.Items.Single(i => i.Field("text") == "ordinary colored text");
            CheckView(colored.Advanced is { Color: "blue", Action: null }, "Ordinary entries save their color without enabling Advanced");
            ShowEditor(colored); UpdateLayout();
            CheckView(!advancedEditor, "A color-only entry reopens with Advanced off");
            ShowList(); ShowEditor(null, "sable.secret", false); UpdateLayout();
            NamedInput("Label").Text = "Secret action fixture";
            var secretBox = LogicalDescendants(Body).OfType<MaskedSecretBox>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Secret value");
            secretBox.Password = "copy-secret-value-canary";
            var eye = Descendants<Button>(Body).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Reveal Secret value");
            CheckView(eye.Parent is Grid && !LogicalDescendants(Body).OfType<CheckBox>().Any(c => c.Content is TextBlock { Text: "Reveal while editing" }), "Secret reveal is an eye inside the value box without a checkbox row");
            eye.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); UpdateLayout();
            var revealed = NamedInput("Secret value revealed");
            CheckView(revealed.IsVisible && revealed.Text == "copy-secret-value-canary" && secretBox.Password.Length == 0, "Eye reveals the value and clears the hidden password control");
            await Render("54-eye-revealed");
            eye.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); UpdateLayout();
            CheckView(secretBox.IsVisible && secretBox.Password == "copy-secret-value-canary" && revealed.Text.Length == 0, "Eye hides the value and clears the plaintext editor");
            await Render("47-secret-eye");
            secretBox.Password = "first\nsecond"; secretBox.Select(5, 0); secretBox.ReplaceSelection("\r\ninserted");
            CheckView(secretBox.Password == "first\r\ninserted\nsecond" && secretBox.Text.All(c => c is '●' or '\r' or '\n'), "Masked multiline editing preserves mixed line endings without exposing text");
            secretBox.SelectAll();
            var pasteData = new DataObject(DataFormats.UnicodeText, "pasted-one\r\npasted-two");
            secretBox.RaiseEvent(new DataObjectPastingEventArgs(pasteData, false, DataFormats.UnicodeText));
            CheckView(secretBox.Password == "pasted-one\r\npasted-two" && !secretBox.CanUndo && !ApplicationCommands.Copy.CanExecute(null, secretBox), "Multiline paste stays masked and cannot be copied or recovered through undo");
            var secretPeer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(secretBox)!;
            CheckView(secretPeer.IsPassword() && secretPeer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Value) is null && secretPeer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Text) is null, "Masked editor automation exposes no value or text pattern");
            eye.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); UpdateLayout();
            CheckView(revealed.Text == "pasted-one\r\npasted-two", "Eye reveal preserves every pasted secret line");
            eye.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); UpdateLayout();
            await Render("50-multiline-secret");
            secretBox.Password = "copy-secret-value-canary";
            var secretAdvanced = LogicalDescendants(Body).OfType<CheckBox>().Single(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Advanced options");
            secretAdvanced.IsChecked = true; secretAdvanced.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout();
            var trackInk = (Border)secretAdvanced.Template.FindName("Track", secretAdvanced);
            CheckView(((SolidColorBrush)trackInk.Background).Color == ((SolidColorBrush)FindResource("Accent")).Color, "Enabled Advanced switch uses the active theme accent");
            var secretKind = LogicalDescendants(Body).OfType<ComboBox>().Single(); secretKind.SelectedItem = "PowerShell"; secretKind.IsDropDownOpen = false; UpdateLayout();
            NamedInput("Action target or script").Text = "Write-Output 'secret-action-fixture'";
            LogicalDescendants(Body).OfType<RadioButton>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Action mode Run and copy").IsChecked = true;
            Descendants<Button>(Body).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Entry color violet").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            var clipboardBeforeTest = ((PreviewClipboard)clipboard).LastText;
            ClickTest();
            CheckView(((PreviewClipboard)clipboard).LastText == clipboardBeforeTest, "Testing an action does not copy the secret being edited");
            PressEnter(NamedInput("Label"));
            var secretAction = vault.Document.Items.Single(i => i.Label == "Secret action fixture");
            CheckView(secretAction.Advanced!.Action!.CopyOnRun, "Run and copy choice persists on the secret entry");
            entryList!.ScrollIntoView(entryList.Items.Cast<RowModel>().Single(r => r.Item.Id == secretAction.Id)); await Render("48-run-copy-row");
            var secretRow = entryList.Items.Cast<RowModel>().Single(r => r.Item.Id == secretAction.Id);
            var secretContainer = (ListBoxItem)entryList.ItemContainerGenerator.ContainerFromItem(secretRow);
            var actionButton = Descendants<Button>(secretContainer).Single(b => b.Tag as string == "entry-action");
            CheckView(secretRow.ActionLabel == "Run & copy" && secretRow.ActionCopyVisibility == Visibility.Visible && actionButton.ActualWidth == Descendants<Button>(secretContainer).Single(b => b.Tag as string == "edit").ActualWidth, "Run and copy uses a small badge without increasing the action button width");
            ReviewEntryAction(secretAction); await Task.Delay(800);
            CheckView(((PreviewClipboard)clipboard).LastText == "copy-secret-value-canary" && ((PreviewClipboard)clipboard).LastSensitive, "Run and copy copies the actual secret with sensitive clipboard handling");
            var protectedClipboard = ((PreviewClipboard)clipboard).LastText;
            previewActionLaunch = _ => throw new System.ComponentModel.Win32Exception("Fixture failed launch");
            ReviewEntryAction(secretAction);
            CheckView(((PreviewClipboard)clipboard).LastText == protectedClipboard, "A failed action launch leaves the clipboard unchanged");
            previewActionLaunch = _ => launchCount++;
            var loginAction = Codec.Clone(secretAction); loginAction.Id = Guid.NewGuid(); loginAction.ContentType = "sable.login"; loginAction.Label = "Login action fixture"; loginAction.Payload = JsonSerializer.SerializeToElement(new { username = "fixture-user", password = "login-copy-password-canary" });
            vault.Mutate(d => d.Items.Add(loginAction)); ShowList(); ReviewEntryAction(loginAction); UpdateLayout();
            CheckView(screen == "entry-action" && !Descendants<TextBox>(Body).Any(b => b.Text.Contains("login-copy-password-canary")) && ((PreviewClipboard)clipboard).LastText == protectedClipboard, "First-run copy review reveals no secret and copies nothing before confirmation");
            GoBack(); CheckView(((PreviewClipboard)clipboard).LastText == protectedClipboard, "Cancelling Run and copy leaves the clipboard unchanged");
            ReviewEntryAction(loginAction); UpdateLayout();
            Descendants<Button>(Body).Single(b => b.Content as string == "Run action").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Task.Delay(800);
            CheckView(((PreviewClipboard)clipboard).LastText == "login-copy-password-canary" && ((PreviewClipboard)clipboard).LastSensitive, "Login Run and copy copies its password, not its label or username");
            var clipboardBeforeRunOnly = ((PreviewClipboard)clipboard).LastText;
            ShowEditor(secretAction); UpdateLayout();
            LogicalDescendants(Body).OfType<RadioButton>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Action mode Run").IsChecked = true;
            ClickTest(); UpdateLayout();
            Descendants<Button>(Body).Single(b => b.IsVisible && b.Content as string == "Test action").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            PressEnter(NamedInput("Label"));
            var runOnlySecret = vault.Document.Items.Single(i => i.Id == secretAction.Id); ReviewEntryAction(runOnlySecret);
            CheckView(!runOnlySecret.Advanced!.Action!.CopyOnRun && ((PreviewClipboard)clipboard).LastText == clipboardBeforeRunOnly, "Selecting Run disables copying while preserving the secret action");
            ShowList(); ShowEditor(null, "sable.text", true); UpdateLayout();
            NamedInput("Label · optional").Text = "Text run and copy fixture"; NamedInput("Text").Text = "ordinary-copy-canary";
            var textKind = LogicalDescendants(Body).OfType<ComboBox>().Single(); textKind.SelectedItem = "CMD";
            NamedInput("Action target or script").Text = "echo terminal-fixture";
            var keepTerminal = LogicalDescendants(Body).OfType<CheckBox>().Single(c => c.Content is TextBlock { Text: "Keep terminal open when finished" });
            CheckView(keepTerminal.IsVisible && keepTerminal.IsChecked == false, "Script keep-open option starts off");
            keepTerminal.IsChecked = true;
            LogicalDescendants(Body).OfType<RadioButton>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Action mode Run and copy").IsChecked = true;
            ClickTest(); UpdateLayout();
            var testFooter = Descendants<Button>(Body).Single(b => b.Content as string == "Test action");
            CheckView(testFooter.ActualHeight == pageSubmit!.ActualHeight && Math.Abs(testFooter.ActualWidth * 2 - pageSubmit.ActualWidth) <= 2 && Math.Abs(testFooter.TranslatePoint(new Point(), Body).Y - pageSubmit.TranslatePoint(new Point(), Body).Y) < .1, "Test action uses one third and Save two thirds while sharing height and vertical alignment");
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout();
            var editorScroll = FindParent<ScrollViewer>(NamedInput("Text"))!; editorScroll.ScrollToEnd(); await Render("51-action-options");
            keepTerminal.IsChecked = false; CheckView(pageSubmit!.Opacity < .5, "Changing terminal lifetime invalidates the tested action"); keepTerminal.IsChecked = true;
            PressEnter(NamedInput("Text"));
            var textAction = vault.Document.Items.Single(i => i.Label == "Text run and copy fixture");
            CheckView(textAction.Advanced!.Action is { CopyOnRun: true, KeepTerminalOpen: true }, "Text entries save Run and copy and keep-terminal settings");
            var textRow = entryList!.Items.Cast<RowModel>().Single(r => r.Item.Id == textAction.Id);
            var actionMenu = BuildEntryContext(textRow); actionMenu.PlacementTarget = Body; actionMenu.IsOpen = true;
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout();
            var contextSource = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(actionMenu);
            CheckView(Native.GetWindowDisplayAffinity(contextSource.Handle, out var contextAffinity) && contextAffinity == CaptureProtection.RequestedAffinity, "Entry context menus have native capture exclusion");
            await Render("52-context-action", actionMenu);
            var contextAction = actionMenu.Items.OfType<MenuItem>().Single(i => i.Header as string == "Run & copy"); actionMenu.IsOpen = false;
            contextAction.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Task.Delay(800);
            CheckView(((PreviewClipboard)clipboard).LastText == "ordinary-copy-canary" && !((PreviewClipboard)clipboard).LastSensitive, "Right-click Run and copy launches then copies ordinary text with its normal clipboard policy");
            ShowEditor(textAction); UpdateLayout(); textKind = LogicalDescendants(Body).OfType<ComboBox>().Single(); textKind.IsDropDownOpen = true;
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout();
            var actionPopup = (System.Windows.Controls.Primitives.Popup)textKind.Template.FindName("PART_Popup", textKind);
            var popupSource = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(actionPopup.Child);
            CheckView(Native.GetWindowDisplayAffinity(popupSource.Handle, out var popupAffinity) && popupAffinity == CaptureProtection.RequestedAffinity, "Action dropdowns have native capture exclusion"); textKind.IsDropDownOpen = false;
            textKind.SelectedItem = "Run"; UpdateLayout();
            CheckView(!LogicalDescendants(Body).OfType<CheckBox>().Single(c => c.Content is TextBlock { Text: "Keep terminal open when finished" }).IsVisible && captureEditorDraft!().Advanced!.Action!.KeepTerminalOpen == false, "Switching away from a script clears and hides keep-terminal settings");
            var tooltip = new ToolTip { Content = "Test action", PlacementTarget = Body, IsOpen = true };
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); UpdateLayout();
            CheckView(tooltip.Background is SolidColorBrush { Color.R: < 65, Color.G: < 65, Color.B: < 65 }, "Tooltips use the shared dark theme");
            var tipSource = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(tooltip);
            CheckView(Native.GetWindowDisplayAffinity(tipSource.Handle, out var tipAffinity) && tipAffinity == CaptureProtection.RequestedAffinity, "Tooltips have native capture exclusion"); await Render("53-dark-tooltip", tooltip); tooltip.IsOpen = false;
            CheckView(!CaptureProtection.HasFailure, "Capture protection reported no failures for the windows opened in this fixture");
            ShowSettings(); UpdateLayout();
            var quitButton = Descendants<Button>(Body).Single(b => b.Content as string == "Quit");
            var aboutButton = Descendants<Button>(Body).Single(b => b.Content as string == "ⓘ  About SableLatch");
            CheckView(FindParent<ScrollViewer>(quitButton) is null && quitButton.TranslatePoint(new Point(), Body).X > aboutButton.TranslatePoint(new Point(), Body).X, "Quit sits in the fixed Settings footer to the right of About");
            await Render("49-settings-quit");
            quitButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(!quitting && quitButton.Content as string == "Confirm quit", "First Quit asks for confirmation without closing the vault");
            await Render("55-confirm-quit");
            busy = true; quitButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); CheckView(!quitting, "Settings Quit respects the existing busy-operation guard"); busy = false;
            ShowList(); ShowSettings(); UpdateLayout();
            CheckView(Descendants<Button>(Body).Any(b => b.Content as string == "Quit") && !Descendants<Button>(Body).Any(b => b.Content as string == "Confirm quit"), "Leaving settings cancels pending Quit confirmation");
            ShowList();
            var psAction = EntryActions.Prepare(custom.Advanced!.Action!);
            CheckView(!psAction.UseShellExecute && psAction.FileName.EndsWith(@"WindowsPowerShell\v1.0\powershell.exe") && psAction.ArgumentList.Contains("-NoProfile") && System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(psAction.ArgumentList.Last())) == custom.Advanced!.Action!.Target, "PowerShell uses a fixed system executable and exact encoded script without a temporary file");
            var appAction = EntryActions.Prepare(new() { Target = Environment.ProcessPath!, Arguments = "--help" });
            CheckView(!appAction.UseShellExecute && appAction.FileName == Environment.ProcessPath && appAction.Arguments == "--help", "Application action retains an explicit executable and separate arguments");
            var winAction = EntryActions.Prepare(new() { Kind = "windows", Target = "Microsoft.Mouse" });
            CheckView(winAction.ArgumentList.SequenceEqual(new[] { "/name", "Microsoft.Mouse" }), "Control Panel action passes a canonical name as one argument");
            var settingsAction = EntryActions.Prepare(new() { Kind = "windows", Target = "ms-settings:display" });
            CheckView(settingsAction.UseShellExecute && settingsAction.FileName == "ms-settings:display", "Windows Settings action accepts the settings scheme");
            var controlAction = EntryActions.Prepare(new() { Kind = "control-panel", Target = "control" });
            CheckView(controlAction.FileName.EndsWith("control.exe") && controlAction.ArgumentList.Count == 0, "Control Panel action can open the main panel");
            CheckView(WindowsSettingsCatalog.Read().Any(c => c.Name == "Sound" && c.Target == "Microsoft.Sound"), "Control Panel picker exposes readable names for canonical destinations");
            var runCommand = EntryActions.Prepare(new() { Kind = "run", Target = "control printers" });
            CheckView(runCommand.UseShellExecute && runCommand.FileName == "control" && runCommand.Arguments == "printers", "Run action uses Windows shell resolution with separate arguments");
            var runQuoted = EntryActions.Prepare(new() { Kind = "run", Target = "\"C:\\Program Files\\Example\\app.exe\" --flag" });
            CheckView(runQuoted.FileName == @"C:\Program Files\Example\app.exe" && runQuoted.Arguments == "--flag", "Run preserves quoted application paths and their arguments");
            var runFolder = EntryActions.Prepare(new() { Kind = "run", Target = "%USERPROFILE%" });
            CheckView(runFolder.FileName == Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Run expands environment variables for folder destinations");
            CheckView(runFolder.WorkingDirectory == Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Blank starting folder defaults to the Windows user folder");
            var cmdAction = EntryActions.Prepare(new() { Kind = "cmd", Target = "echo fixture" });
            CheckView(EntryActions.Prepare(new() { Kind = "cmd", Target = "echo fixture", KeepTerminalOpen = true }).Arguments.StartsWith("/d /s /k "), "CMD keep-open uses /k while retaining disabled AutoRun");
            CheckView(EntryActions.Prepare(new() { Kind = "powershell", Target = "Write-Output 'fixture'", KeepTerminalOpen = true }).ArgumentList.Contains("-NoExit") && !psAction.ArgumentList.Contains("-NoExit"), "PowerShell keep-open uses NoExit only when requested");
            CheckView(cmdAction.FileName.EndsWith("cmd.exe") && cmdAction.Arguments.StartsWith("/d /s /c "), "CMD action disables AutoRun and prepares only explicit command text");
            async Task<string> RunBenignFixture(EntryAction fixtureAction)
            {
                // Only the hard-coded no-write fixtures below run; user-defined actions remain blocked in preview mode.
                var start = EntryActions.Prepare(fixtureAction); start.CreateNoWindow = true; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
                using var process = System.Diagnostics.Process.Start(start)!;
                var outputTask = process.StandardOutput.ReadToEndAsync(); var errorTask = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try { await process.WaitForExitAsync(timeout.Token); }
                catch { if (!process.HasExited) process.Kill(); throw; }
                if (process.ExitCode != 0) throw new InvalidOperationException("Benign process fixture failed: " + await errorTask);
                return (await outputTask).Trim();
            }
            CheckView(await RunBenignFixture(new() { Kind = "powershell", Target = "Write-Output 'quoted & literal'\nWrite-Output 'second line'" }) == "quoted & literal\r\nsecond line", "Actual PowerShell launch preserves multiline script and quoted metacharacters");
            CheckView(await RunBenignFixture(new() { Kind = "cmd", Target = "echo \"quoted & literal\"" }) == "\"quoted & literal\"", "Actual CMD launch preserves quoted metacharacters");
            CheckView(await RunBenignFixture(new() { Target = EntryActions.SystemFile("cmd.exe"), Arguments = "/d /c echo application-fixture" }) == "application-fixture", "Application handler launches the selected executable with its configured arguments");
            async Task<bool> KeepsBenignTerminalOpen(string kind, string command)
            {
                var start = EntryActions.Prepare(new() { Kind = kind, Target = command, KeepTerminalOpen = true });
                start.CreateNoWindow = true; start.RedirectStandardInput = true; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
                using var process = new System.Diagnostics.Process { StartInfo = start };
                var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                process.OutputDataReceived += (_, e) => { if (e.Data?.Contains("keepalive-fixture") == true) ready.TrySetResult(true); };
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                try
                {
                    if (await Task.WhenAny(ready.Task, Task.Delay(8000)) != ready.Task) return false;
                    await Task.Delay(250); return !process.HasExited;
                }
                finally
                {
                    if (!process.HasExited) { try { await process.StandardInput.WriteLineAsync("exit"); await process.StandardInput.FlushAsync(); } catch (IOException) { } }
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { if (!process.HasExited) process.Kill(); await process.WaitForExitAsync(); }
                }
            }
            CheckView(await KeepsBenignTerminalOpen("cmd", "echo keepalive-fixture"), "Actual CMD remains alive after its fixture command until explicitly closed");
            // PowerShell NoExit is checked as a launch plan; redirected stdin changes its interactive-host behavior.
            ShowAppearanceSettings(); await Render("42-app-preferences");
            CheckView(LogicalDescendants(Body).OfType<ComboBox>().Any(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Taskbar & Alt+Tab" && c.Items.Count == 3), "App preferences expose all three taskbar modes");
            foreach (var invalid in new EntryAction[] { new() { Target = "notepad.exe" }, new() { Kind = "windows", Target = "file:///bad.exe" }, new() { Kind = "windows", Target = "Microsoft.Mouse & bad" }, new() { Kind = "unknown", Target = "anything" }, new() { Kind = "cmd", Target = "echo first\necho second" }, new() { Kind = "powershell", Target = "echo disabled", Enabled = false } })
            {
                var rejected = false; try { EntryActions.Prepare(invalid); } catch (InvalidOperationException) { rejected = true; }
                CheckView(rejected, "Execution rejects invalid or unsupported action: " + invalid.Kind + " / " + invalid.Target);
            }
            folder = null; vault.Mutate(d => d.Settings.CloseAfterCopy = true); ShowList();
            var detectedApps = await ApplicationCatalog.ReadAsync();
            CheckView(detectedApps.Count <= 2000 && detectedApps.All(a => Path.IsPathFullyQualified(a.Path) && File.Exists(a.Path) && a.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)), $"On-demand app discovery returns {detectedApps.Count} bounded existing executable paths");
            previewActionLaunch = null;
            var fixedTop = Top; var fixedHeight = Height;
            folder = vault.Document!.Folders.First().Id; ShowList(); ShowSettings(); ShowAppearanceSettings(); ShowEditor(); UpdateLayout();
            CheckView(Math.Abs(Height - 411) < 1 && Math.Abs(fixedHeight - Height) < 1 && Math.Abs(Top - fixedTop) < 1, $"Folders, settings and editor keep fixed size and position (height {fixedHeight} to {Height}, top {fixedTop} to {Top})");
            ShowSettings(); UpdateLayout();
            CheckView(Descendants<Button>(Body).Any(b => b.Content as string == "Plugins & modules  ›") && Descendants<Button>(Body).Any(b => b.Content as string == "ⓘ  About SableLatch"), "Main Settings includes Plugins after preferences and About in its footer");
            ShowAppearanceSettings(); UpdateLayout();
            CheckView(!LogicalDescendants(Body).OfType<ComboBox>().Any(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Entry layout"), "App preferences omit the entry-layout selector");
            ShowModules(); UpdateLayout();
            CheckView(!LogicalDescendants(Body).OfType<ComboBox>().Any(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Entry layout"), "Plugin layout choices stay hidden until another layout is registered");
            GoBack(); UpdateLayout(); CheckView(Descendants<Button>(Body).Any(b => b.Content as string == "App preferences  ›"), "Plugin settings Back returns to the main Settings page");
            var menu = new ContextMenu();
            foreach (var label in new[] { "Edit", "Move to folder…", "Delete…" }) menu.Items.Add(new MenuItem { Header = label });
            menu.ApplyTemplate(); menu.Measure(new Size(190, double.PositiveInfinity)); menu.Arrange(new Rect(0, 0, 190, menu.DesiredSize.Height)); menu.UpdateLayout();
            CheckView(Descendants<TextBlock>(menu).Where(t => t.Name == "Check").All(t => t.Visibility == Visibility.Collapsed), "Ordinary context actions have no checkbox gutter");
            await Render("21-context-menu", menu);
            ShowSettings(); UpdateLayout();
            var finalQuit = Descendants<Button>(Body).Single(b => b.Content as string == "Quit");
            finalQuit.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            finalQuit.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            CheckView(quitting && !vault.IsUnlocked && !IsVisible, "Confirmed second Quit closes the app and releases the unlocked vault");
            File.WriteAllLines(Path.Combine(output, "interaction-checks.txt"), checks);
            File.WriteAllText(Path.Combine(output, "ui-checks.txt"), "PASS: setup, list, login editor, settings, clipboard settings, rearrange, recovery and lock render.\nPASS: secret canaries absent from list text controls.\nPASS: editor Save persists a multiline text item.\nPASS: search filters the actual list.\nPASS: disabling a content module makes its rows unavailable.\nPASS: pin sets both session policy and the native topmost property.\nPASS: lock drops the document and list references even when pinned.\n");
            File.WriteAllText(Path.Combine(output, "complete.txt"), "complete");
            async Task Render(string name, FrameworkElement? element = null)
            {
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                UpdateLayout();
                var target = element ?? this;
                var bitmap = new RenderTargetBitmap(Math.Max(1, (int)target.ActualWidth), Math.Max(1, (int)target.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(target);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
            }
        }
        catch (Exception ex) { Directory.CreateDirectory(App.DataDirectory); File.WriteAllText(Path.Combine(App.DataDirectory, "preview-error.txt"), ex.ToString()); }
        finally { if (!quitting) { quitting = true; Close(); } Application.Current.Shutdown(); }
#else
        await Task.CompletedTask;
#endif
    }
}
