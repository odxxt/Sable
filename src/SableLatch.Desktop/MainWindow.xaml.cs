using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace SableLatch.Desktop;

public partial class MainWindow : Window
{
    private readonly VaultStore vault;
    private readonly AuditJournal audit;
    private readonly ActionApprovals actionApprovals;
    private bool auditUnavailable;
    private bool auditStarted;
    private readonly DateTimeOffset auditStartedAt = DateTimeOffset.UtcNow;
    private readonly ContentRegistry content = ContentRegistry.BuiltIns();
    private readonly DesktopExtensions extensions = new();
    private readonly IClipboardService clipboard;
    private readonly SessionPolicy session = new();
    private readonly DispatcherTimer heartbeat = new() { Interval = TimeSpan.FromSeconds(1) };
    private Forms.NotifyIcon? tray;
    private IntPtr handle;
    private bool quitting, busy, holdForPassword, contextOpen;
    private bool clearingEditors;
    private bool systemNotificationsReady;
    private int epoch, modalDepth;
    private int copyGeneration;
    private QuickUnlockSession? quickUnlock;
    private bool searchVisible, recordingShortcut;
    private Guid? folder, armedDelete;
    private string screen = "unlock", search = "";
    private DateTimeOffset nextMaintenance;
    private DateTimeOffset nextUnlockAttempt;
    private int failedUnlocks;
    private readonly List<Action> eraseEditors = [];
    private CancellationTokenSource sessionCancellation = new();
    private TextBlock? formError;
    private double? savedLeft, savedTop;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        clipboard = App.PreviewMode ? new PreviewClipboard() : new ClipboardService(extensions);
#else
        clipboard = new ClipboardService(extensions);
#endif
        FlowDirection = Localizer.Flow;
        extensions.RegisterLayout(new RowsLayout());
        foreach (var provider in content.All)
            extensions.RegisterEditor(new FieldEditorFactory(provider.Descriptor.Id, (item, save, cancel) => BuildFields(provider, item, save)));
#if DEBUG
        IDeviceProtector device = App.PreviewMode ? new PreviewDeviceProtector() : new WindowsDeviceProtector();
#else
        IDeviceProtector device = new WindowsDeviceProtector();
#endif
        vault = new VaultStore(App.DataDirectory, device);
        audit = new AuditJournal(App.DataDirectory, device);
        actionApprovals = new ActionApprovals(App.DataDirectory, device);
        moduleApprovals = new ModuleApprovals(App.DataDirectory, device);
        modules = new ModuleStore(Path.Combine(App.DataDirectory, "modules"), ModuleTrust.Verify);
        ReloadModules();
        SourceInitialized += (_, _) => InitializePlatform();
        Loaded += (_, _) => { PositionWindow(); if (!App.PreviewMode) ShowUnlock(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) ResetLoginCopy(); else { ApplyTaskbarVisibility(); FocusLockInput(); } };
        Activated += (_, _) => FocusLockInput();
        Deactivated += (_, _) => { if (screen == "list" && !session.IsPinned && !holdForPassword && !busy && !contextOpen && modalDepth == 0) Hide(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && closeChoicePicker is not null) { closeChoicePicker(); e.Handled = true; return; }
            if (HandlePageEnter(e)) return;
            if (e.Key != Key.Escape || recordingShortcut) return;
            e.Handled = true;
            if (armedDelete is not null) CancelDelete();
            else if (vault.IsUnlocked && screen != "list") GoBack();
            else Hide();
        };
        Closing += OnClosing;
        PreviewMouseDown += (_, e) =>
        {
            if (screen is not ("pin" or "unlock") || !vault.Exists || busy || pinAttemptRunning || FindParent<Button>(e.OriginalSource as DependencyObject) is not null) return;
            var input = FindLockInput(Body);
            if (input is not null) Dispatcher.BeginInvoke(() => input.Focus());
        };
        heartbeat.Tick += (_, _) => Tick();
    }
    private void InitializePlatform()
    {
        handle = new WindowInteropHelper(this).Handle;
        CaptureProtection.Protect(handle);
        ApplyTaskbarVisibility();
        HwndSource.FromHwnd(handle)?.AddHook(WindowMessage);
        if (!App.PreviewMode)
        {
            systemNotificationsReady = Native.WTSRegisterSessionNotification(handle, 0);
            var shortcut = "Ctrl+Alt+Space";
            try { var file = Path.Combine(App.DataDirectory, "host.json"); if (File.Exists(file)) shortcut = Codec.Decode<string>(File.ReadAllBytes(file)); } catch { }
            try { SetShortcut(shortcut); } catch { Status.Text = "Shortcut unavailable · use the tray"; }
            var icon = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/SableLatch.ico"));
            tray = new Forms.NotifyIcon { Text = "SableLatch · locked", Icon = new System.Drawing.Icon(icon!.Stream), Visible = true };
            tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Dispatcher.Invoke(ToggleVisibility); };
            var menu = new Forms.ContextMenuStrip();
            menu.BackColor = System.Drawing.Color.FromArgb(32, 32, 35); menu.ForeColor = System.Drawing.Color.White; menu.ShowImageMargin = false;
            menu.Opened += (_, _) => CaptureProtection.Protect(menu.Handle);
            menu.Items.Add("Open SableLatch", null, (_, _) => Dispatcher.Invoke(OpenPanel));
            menu.Items.Add("Lock", null, (_, _) => Dispatcher.Invoke(() => ForceLock("Locked")));
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApp));
            tray.ContextMenuStrip = menu;
        }
    }
    private IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (msg == 0x0312 && wp.ToInt32() == 71) { if (!recordingShortcut) ToggleVisibility(); handled = true; }
        if (msg == 0x02B1 && wp.ToInt32() is 6 or 7 or 8)
        {
            if (wp.ToInt32() == 6) { quickUnlock?.Dispose(); quickUnlock = null; }
            ForceLock("Windows session locked");
        }
        if (msg == 0x0218 && wp.ToInt32() is 4 or 6 or 7 or 18) ForceLock("Locked after sleep");
        if (msg == 0x0011) { quickUnlock?.Dispose(); quickUnlock = null; ForceLock("Windows is signing out"); }
        return IntPtr.Zero;
    }
    private void SetShortcut(string shortcut)
    {
        var (mods, key) = Native.ParseShortcut(shortcut);
        Native.UnregisterHotKey(handle, 71);
        if (!Native.RegisterHotKey(handle, 71, mods, key)) throw new InvalidOperationException("That shortcut is already in use. Choose another in Settings.");
    }
    private void PositionWindow()
    {
        var work = SystemParameters.WorkArea;
        try
        {
            var file = Path.Combine(App.DataDirectory, "window.json");
            if (File.Exists(file)) { var p = Codec.Decode<double[]>(File.ReadAllBytes(file)); if (p.Length == 2 && p.All(double.IsFinite)) { savedLeft = p[0]; savedTop = p[1]; } }
        }
        catch { /* Position is cosmetic; fall back to the primary work area. */ }
        Left = Math.Clamp(savedLeft ?? work.Right - Width - 18, work.Left, Math.Max(work.Left, work.Right - Width));
        Top = Math.Clamp(savedTop ?? work.Bottom - Height - 18, work.Top, Math.Max(work.Top, work.Bottom - Height));
    }
    private void HeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<Button>(e.OriginalSource as DependencyObject) is not null) return;
        if (e.ClickCount == 2 && FindParent<TextBlock>(e.OriginalSource as DependencyObject) == BrandTitle) { ResetWindowPosition(); e.Handled = true; return; }
        try
        {
            DragMove();
            if (!App.PreviewMode && Directory.Exists(App.DataDirectory)) File.WriteAllBytes(Path.Combine(App.DataDirectory, "window.json"), Codec.Encode(new[] { Left, Top }));
        }
        catch (InvalidOperationException) { }
        catch (IOException) { SetStatus("Position could not be saved"); }
    }
    private void ResetWindowPosition()
    {
        var work = SystemParameters.WorkArea;
        Left = Math.Max(work.Left, work.Right - Width - 18); Top = Math.Max(work.Top, work.Bottom - Height - 18);
        savedLeft = Left; savedTop = Top;
        try { if (!App.PreviewMode) File.WriteAllBytes(Path.Combine(App.DataDirectory, "window.json"), Codec.Encode(new[] { Left, Top })); }
        catch (IOException) { SetStatus("Position could not be saved"); }
    }
    internal static T? FindParent<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj is not null) { if (obj is T result) return result; obj = obj is Visual ? VisualTreeHelper.GetParent(obj) : LogicalTreeHelper.GetParent(obj); }
        return null;
    }
    private void ToggleVisibility()
    {
        if (!vault.IsUnlocked && vault.Exists) { OpenPanel(); return; }
        if (IsVisible && IsActive) Hide(); else OpenPanel();
    }
    private void OpenPanel()
    {
        if (!IsVisible && vault.IsUnlocked && vault.Document!.Settings.LockEveryOpen && !session.IsPinned) ForceLock("Unlock to continue");
        if (vault.IsUnlocked && session.Expired(vault.Document!.Settings.SessionMinutes)) ForceLock("Session expired");
        ApplyTaskbarVisibility(); Show(); Activate(); FocusLockInput();
    }
    private void TogglePin(object sender, RoutedEventArgs e)
    {
        if (!vault.IsUnlocked && !App.PreviewMode) return;
        session.IsPinned = !session.IsPinned; Topmost = session.IsPinned || holdForPassword;
        ApplyTaskbarVisibility();
        PinButton.Background = session.IsPinned ? (Brush)FindResource("HoverSurface") : Brushes.Transparent;
        PinButton.ToolTip = session.IsPinned ? "Unpin · resume session timeout" : "Pin · keep open and pause session timeout";
        if (vault.IsUnlocked && session.Expired(vault.Document!.Settings.SessionMinutes)) ForceLock("Session expired");
        else UpdateStatus();
    }
    private void ToggleSearch(object sender, RoutedEventArgs e)
    {
        if (!vault.IsUnlocked || busy) return;
        NavigateSettings(() => { searchVisible = screen != "list" || !searchVisible; if (!searchVisible) search = ""; ShowList(); });
    }
    private void LockNow(object sender, RoutedEventArgs e) => ForceLock("Locked");
    private void HidePanel(object sender, RoutedEventArgs e) => Hide();
    private void ForceLock(string reason)
    {
        if (session.IsUnlocked) Audit(AuditEvent.Locked);
        epoch++; session.Lock(); ResetLoginCopy(); armedDelete = null;
        heartbeat.Stop();
        search = ""; searchVisible = false;
        sessionCancellation.Cancel(); sessionCancellation.Dispose(); sessionCancellation = new();
        EraseEditors(); Body.Content = null;
        clipboard.ClearOwned();
        if (!busy) vault.Lock();
        if (tray is not null) tray.Text = "SableLatch · locked";
        ShowUnlock(); SetStatus(reason); FocusLockInput();
        if (!busy) ReleaseUnlockMemory();
    }
    private void Tick()
    {
        if (!vault.IsUnlocked || busy) return;
        if (session.Expired(vault.Document!.Settings.SessionMinutes)) { ForceLock("Session expired"); return; }
        if (DateTimeOffset.UtcNow < nextMaintenance) return;
        nextMaintenance = DateTimeOffset.UtcNow.AddMinutes(1);
        try
        {
            if (vault.Document!.Settings.ExpireTrash && vault.Document.Items.Any(x => x.DeletedAt <= DateTimeOffset.UtcNow.AddDays(-vault.Document.Settings.TrashDays)))
            {
                vault.Mutate(d => ItemOperations.ExpireTrash(d, DateTimeOffset.UtcNow)); vault.CleanOrphanAttachments();
                if (screen == "trash") ShowTrash();
            }
            var s = vault.Document.Settings;
            if (s.BackupEnabled && vault.Document.Backup is not null && !string.IsNullOrWhiteSpace(s.BackupDirectory) && vault.Document.Revision > s.LastBackupRevision && (s.LastBackupAt is null || DateTimeOffset.UtcNow - s.LastBackupAt >= TimeSpan.FromHours(s.BackupHours)))
                _ = MakeBackup(false);
        }
        catch { SetStatus("Maintenance failed · check backup folder and storage"); }
    }
    private async Task<bool> RunVault(Func<Task> operation, Action? after = null, bool quietFailure = false)
    {
        if (busy) return false;
        if (!App.PreviewMode && !systemNotificationsReady) { ShowError("Windows lock notifications are unavailable. Restart SableLatch in your normal Windows session before unlocking."); return false; }
        var startEpoch = epoch; busy = true; Body.IsEnabled = false; SetStatus("Working…");
        try
        {
            await operation();
            if (epoch != startEpoch) { vault.Lock(); ShowUnlock(); return false; }
            after?.Invoke(); return true;
        }
        catch (Exception ex)
        {
            if (!quietFailure && screen != "unlock") Audit(AuditEvent.OperationFailed);
            if (epoch != startEpoch) { vault.Lock(); ShowUnlock(); return false; }
            if (!quietFailure) ShowError(ex is CryptographicException ? "Couldn't unlock. Check your credentials." : ex is ArgumentException or IOException or InvalidDataException or InvalidOperationException ? ex.Message : "Couldn't complete that action. Your saved vault is intact.");
            return false;
        }
        finally { busy = false; Body.IsEnabled = true; ReleaseUnlockMemory(); }
    }
    private static void ReleaseUnlockMemory()
    {
        // Argon2 intentionally uses 64 MiB during authentication; release its temporary
        // buffers at the operation boundary rather than keeping them resident while idle.
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }
    private bool Change(Action<VaultDocument> action)
    {
        if (busy || !vault.IsUnlocked) return false;
        try { vault.Mutate(action); Audit(AuditEvent.Changed); return true; }
        catch (Exception ex) { ShowError(ex is IOException or InvalidDataException ? ex.Message : "Could not save. Your previous saved vault is intact."); return false; }
    }
    private void SetStatus(string text) { Status.Text = text; Status.ToolTip = text; }
    private void UpdateStatus()
    {
        if (!vault.IsUnlocked) { SetStatus("LOCAL · ENCRYPTED"); return; }
        var count = vault.Document!.Items.Count(x => x.DeletedAt is null);
        SetStatus(session.IsPinned ? $"{count} ITEMS · PINNED · TIMER PAUSED" : $"{count} ITEMS · ENCRYPTED · {vault.Document.Settings.SessionMinutes} MIN SESSION");
    }
    private void ShowError(string message)
    {
        SetStatus(message);
        var field = Keyboard.FocusedElement as Control;
        if (field is not TextBox and not PasswordBox)
            field = Descendants<PasswordBox>(Body).FirstOrDefault() as Control ?? Descendants<TextBox>(Body).FirstOrDefault();
        if (field is not null) { RejectInput(field, message); return; }
        if (formError is not null) { formError.Text = message; formError.Visibility = Visibility.Visible; }
        else { ErrorText.Text = message; ErrorToast.Visibility = Visibility.Visible; }
    }
    private void EraseEditors()
    {
        CancelEditHover(); ClearDropMarker(); ClearFolderMarker(); folderChips = null; dragHandle = null;
        captureEditorDraft = null; toggleAdvancedEditor = null;
        clearingEditors = true;
        if (entryList is not null) { entryList.ItemsSource = null; entryList = null; }
        foreach (var clear in eraseEditors) clear();
        eraseEditors.Clear(); formError = null; clearingEditors = false;
    }
    private void SetBody(FrameworkElement element, string mode)
    {
        ApplyTaskbarVisibility();
        closeChoicePicker?.Invoke();
        ResetSettingsDraft();
        if (mode != "editor") { editorDrafts.Clear(); editorSharedLabel = ""; advancedDraft = null; editorTestedAction = null; }
        EraseEditors(); recordingShortcut = false; ErrorToast.Visibility = Visibility.Collapsed; Body.Content = element; screen = mode; formError = null;
        backAction = null;
        pageFooter = null;
        pageSubmit = null;
        SearchButton.IsEnabled = vault.IsUnlocked;
        SearchButton.Background = mode == "list" && searchVisible ? (Brush)FindResource("HoverSurface") : Brushes.Transparent;
        SettingsButton.Background = mode is "settings" or "about" or "audit" or "pin-setup" ? (Brush)FindResource("HoverSurface") : Brushes.Transparent;
        PinButton.Background = session.IsPinned ? (Brush)FindResource("HoverSurface") : Brushes.Transparent;
        SettingsButton.IsEnabled = vault.IsUnlocked;
        LockButton.IsEnabled = vault.IsUnlocked;
        PinButton.IsEnabled = vault.IsUnlocked || App.PreviewMode;
        var onboarding = mode == "onboarding-ready" || !vault.Exists && mode is "setup" or "recovery-key";
        foreach (var command in new[] { SearchButton, PinButton, LockButton, SettingsButton }) command.Visibility = onboarding ? Visibility.Collapsed : Visibility.Visible;
        ExtensionToolbar.Children.Clear();
        if (session.IsUnlocked && vault.IsUnlocked && extensions.Toolbar.Count > 0)
        {
            // Overflow menu keeps arbitrary contributions from crowding a compact header.
            var launcher = Button("⋯", () => { }); launcher.Width = 27;
            launcher.ToolTip = "Extension actions";
            var menu = new ContextMenu();
            foreach (var action in extensions.Toolbar)
            {
                var item = new MenuItem { Header = action.Label };
                item.Click += async (_, _) => { if (!vault.IsUnlocked || busy) return; try { await action.Execute(sessionCancellation.Token); } catch (OperationCanceledException) { } catch { ShowError("The extension action could not finish."); } };
                menu.Items.Add(item);
            }
            launcher.Click += (_, _) => { menu.PlacementTarget = launcher; menu.IsOpen = true; };
            ExtensionToolbar.Children.Add(launcher);
        }
    }
    private string taskbarMode = "pinned";
    private void ApplyTaskbarVisibility()
    {
        if (vault.IsUnlocked) taskbarMode = vault.Document!.Settings.TaskbarMode;
        var visible = taskbarMode == "always" || taskbarMode != "never" && session.IsPinned;
        // WPF owns taskbar registration; keep its state in sync with native Alt+Tab styles.
        if (ShowInTaskbar != visible) ShowInTaskbar = visible;
        Native.SetAltTabVisibility(handle, visible);
    }
    private void ExitApp()
    {
        if (busy) { SetStatus("Wait for the current operation before exiting"); return; }
        quitting = true; ForceLock("Locked"); Close(); Application.Current.Shutdown();
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!quitting) { e.Cancel = true; Hide(); return; }
        Audit(AuditEvent.Exited); heartbeat.Stop(); quickUnlock?.Dispose(); clipboard.Dispose(); vault.Dispose(); tray?.Dispose();
        Native.UnregisterHotKey(handle, 71); Native.WTSUnRegisterSessionNotification(handle);
        sessionCancellation.Cancel(); sessionCancellation.Dispose();
    }
    private async Task MakeBackup(bool manual)
    {
        if (!vault.IsUnlocked) return;
        var s = vault.Document!.Settings;
        await RunVault(() => Task.Run(() => Backups.Save(vault, s.BackupDirectory, s.BackupKeep)), () =>
        {
            vault.Mutate(d => { d.Settings.LastBackupAt = DateTimeOffset.UtcNow; d.Settings.LastBackupRevision = d.Revision + 1; });
            SetStatus(manual ? "Encrypted backup saved" : "Automatic backup saved");
            Audit(AuditEvent.BackupSaved);
        });
    }
}
