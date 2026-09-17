using System.Security.Cryptography;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private bool pinAttemptRunning;
    private static PasswordBox? FindLockInput(DependencyObject root)
    {
        if (root is PasswordBox input) return input;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindLockInput(VisualTreeHelper.GetChild(root, i)) is { } child) return child;
        return null;
    }
    private void RefreshQuickUnlock()
    {
        quickUnlock?.Dispose(); quickUnlock = vault.CreateQuickUnlockSession();
    }
    private (StackPanel Stack, PasswordBox Input, TextBlock Attempts) LockForm(string mode, bool pinMode)
    {
        var host = new Grid { Background = Brushes.Transparent }; SetBody(host, mode);
        var stack = new StackPanel { Width = 240, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        host.Children.Add(stack);
        stack.Children.Add(new TextBlock { Text = "\uE72E", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 27, Opacity = .6, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,16) });
        stack.Children.Add(new TextBlock { Text = "Locked", FontSize = 22, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = pinMode ? "Enter your PIN" : "Enter your full password", FontSize = 11, Opacity = .5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,8,0,8) });
        var input = new PasswordBox { Tag = "bare-secret", Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = pinMode ? 27 : 20, Height = 49, MaxLength = pinMode ? 12 : 1024, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4) };
        System.Windows.Automation.AutomationProperties.SetName(input, pinMode ? "PIN" : "Password");
        var typing = new Grid { Height = 49 };
        typing.Children.Add(input);
        var placeholder = new TextBlock { Text = pinMode ? "• • • •" : "• • • • • • • •", FontSize = 22, Opacity = .2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        typing.Children.Add(placeholder);
        input.PasswordChanged += (_, _) => placeholder.Visibility = input.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        stack.Children.Add(typing); eraseEditors.Add(input.Clear);
        host.PreviewMouseDown += (_, e) =>
        {
            if (FindParent<Button>(e.OriginalSource as DependencyObject) is null) input.Focus();
        };
        var attempts = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(230, 131, 141)), Height = 20, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(attempts);
        Dispatcher.BeginInvoke(() => input.Focus());
        return (stack, input, attempts);
    }
    private void ShowPinUnlock()
    {
        var (stack, pin, attempts) = LockForm("pin", true);
        void UpdateAttempts()
        {
            var remaining = quickUnlock?.AttemptsRemaining ?? 0;
            attempts.Text = remaining is >= 1 and <= 3 ? $"{remaining} {(remaining == 1 ? "attempt" : "attempts")} left" : "";
        }
        UpdateAttempts();
        async Task Unlock()
        {
            if (busy || pinAttemptRunning) { pin.Clear(); return; }
            if (DateTimeOffset.UtcNow < nextUnlockAttempt) { pin.Clear(); return; }
            var ticket = quickUnlock; if (ticket is null || !ticket.Available) { ShowUnlock(); return; }
            var value = pin.Password; pin.Clear(); var startEpoch = epoch;
            pinAttemptRunning = true; pin.IsEnabled = false;
            try
            {
            var ok = await RunVault(() => Task.Run(() => vault.UnlockWithPin(value, ticket)), () => OnUnlocked(false), quietFailure: true);
            if (!ok && startEpoch == epoch)
            {
                Audit(AuditEvent.PinRejected);
                failedUnlocks++; nextUnlockAttempt = DateTimeOffset.UtcNow.AddMilliseconds(650);
                if (!ticket.Available)
                {
                    Audit(AuditEvent.PinExhausted); quickUnlock = null; nextUnlockAttempt = default; failedUnlocks = 0; ShowUnlock();
                }
                else { UpdateAttempts(); RejectInput(pin, ""); await Task.Delay(650); }
            }
            }
            finally
            {
                pin.Clear(); pin.IsEnabled = true; pinAttemptRunning = false;
                if (startEpoch == epoch && screen == "pin") pin.Focus();
            }
        }
        var unlock = AsyncButton("Unlock", Unlock); unlock.HorizontalContentAlignment = HorizontalAlignment.Center; stack.Children.Add(unlock);
        pageSubmit = unlock;
        var fallback = Button("Use full password", () => { quickUnlock?.Dispose(); quickUnlock = null; ShowUnlock(); });
        fallback.HorizontalContentAlignment = HorizontalAlignment.Center; fallback.Opacity = .55; stack.Children.Add(fallback);
    }
    private void ShowPinSetup(bool requirePassword)
    {
        var stack = Form("Quick PIN", "", "pin-setup", requirePassword, parent: ShowSecuritySettings);
        stack.Children.Add(Text("Reopen this running app with a PIN.", 12, true));
        var password = requirePassword ? Password(stack, "Current app password") : null;
        var pin = Password(stack, "PIN · 4–12 digits"); pin.MaxLength = 12;
        var confirm = Password(stack, "Confirm PIN"); confirm.MaxLength = 12;
        SettingsSave(stack, AsyncButton("Save PIN", async () =>
        {
            var chosen = pin.Password; var full = password?.Password;
            try { PinConfiguration.RequirePin(chosen); }
            catch (ArgumentException ex) { RejectInput(pin, ex.Message); return; }
            if (chosen != confirm.Password) { RejectInput(confirm, "PINs do not match."); return; }
            await RunVault(() => Task.Run(() =>
            {
                if (requirePassword) vault.VerifyPassword(full!);
                var config = PinConfiguration.Create(chosen);
                try { vault.Mutate(d => d.QuickUnlock = Codec.Clone(config)); }
                finally { CryptographicOperations.ZeroMemory(config.Verifier); }
            }), () => { Audit(AuditEvent.PinChanged); RefreshQuickUnlock(); ShowList(); });
        }));
        if (requirePassword && vault.Document!.QuickUnlock is not null)
            stack.Children.Add(AsyncButton("Disable PIN", async () =>
            {
                var full = password!.Password;
                await RunVault(() => Task.Run(() => { vault.VerifyPassword(full); vault.Mutate(d => { d.QuickUnlock = null; d.Settings.PinPromptDismissed = true; }); }), () => { Audit(AuditEvent.PinChanged); RefreshQuickUnlock(); ShowList(); });
            }, false));
        else if (!requirePassword) stack.Children.Add(Button("Use full password instead", () => { if (Change(d => d.Settings.PinPromptDismissed = true)) ShowList(); }));
    }
    private static void RejectInput(Control field, string message)
    {
        ValidationFeedback.Failed(field, message);
        field.ToolTip = message;
        System.Windows.Automation.AutomationProperties.SetHelpText(field, message);
        var transform = new TranslateTransform(); field.RenderTransform = transform;
        var shake = new DoubleAnimationUsingKeyFrames();
        var offsets = new double[] { 0, -5, 5, -4, 4, -2, 2, 0 };
        for (var i = 0; i < offsets.Length; i++) shake.KeyFrames.Add(new LinearDoubleKeyFrame(offsets[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i * 45))));
        transform.BeginAnimation(TranslateTransform.XProperty, shake);
        if (field.Tag as string == "bare-secret")
        {
            var ink = new SolidColorBrush(Color.FromRgb(240, 114, 130)); field.Foreground = ink;
            ink.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(ink.Color, Colors.White, TimeSpan.FromSeconds(1)));
            field.Focus(); return;
        }
        var brush = new SolidColorBrush(Color.FromRgb(106, 40, 48)); field.Background = brush;
        var normal = ((SolidColorBrush)Application.Current.FindResource("ControlSurface")).Color;
        var fade = new ColorAnimation(brush.Color, normal, TimeSpan.FromSeconds(1.5));
        fade.Completed += (_, _) => field.SetResourceReference(Control.BackgroundProperty, "ControlSurface");
        brush.BeginAnimation(SolidColorBrush.ColorProperty, fade);
        field.Focus();
    }
    private Func<string> ShortcutRecorder(StackPanel stack, string initial)
    {
        var selected = initial;
        stack.Children.Add(Text("Global shortcut", 11, true));
        Button? record = null;
        record = Button(selected, () => { recordingShortcut = true; record!.Content = "Press keys…"; record.Focus(); }, true);
        record.ToolTip = "Record a shortcut";
        record.PreviewKeyDown += (_, e) =>
        {
            if (!recordingShortcut) return;
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape) { recordingShortcut = false; record.Content = selected; return; }
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
            var modifiers = Keyboard.Modifiers;
            var parts = new List<string>();
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (parts.Count == 0 || modifiers.HasFlag(ModifierKeys.Windows)) { RejectInput(record, "Include Ctrl, Alt or Shift."); return; }
            parts.Add(key.ToString()); var candidate = string.Join('+', parts);
            try { Native.ParseShortcut(candidate); selected = candidate; record.Content = selected; recordingShortcut = false; DraftEdited(); }
            catch (ArgumentException ex) { RejectInput(record, ex.Message); }
        };
        record.LostKeyboardFocus += (_, _) => { recordingShortcut = false; record.Content = selected; };
        stack.Children.Add(record);
        stack.Children.Add(Button("Restore default", () => { recordingShortcut = false; selected = "Ctrl+Alt+Space"; record.Content = selected; DraftEdited(); }));
        return () => selected;
    }
}
