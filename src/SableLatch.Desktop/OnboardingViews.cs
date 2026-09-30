using System.Text.Json;
using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private static void WelcomeMotion(FrameworkElement element)
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        var shift = new TranslateTransform(); element.RenderTransform = shift;
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { FillBehavior = FillBehavior.Stop });
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(9, 0, TimeSpan.FromMilliseconds(260))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        element.Unloaded += (_, _) => { element.BeginAnimation(OpacityProperty, null); shift.BeginAnimation(TranslateTransform.YProperty, null); };
    }

    private StackPanel SetupPage(int step, string title, string hint, Action? back = null, string mode = "setup")
    {
        var page = new Grid();
        page.RowDefinitions.Add(new() { Height = GridLength.Auto });
        page.RowDefinitions.Add(new() { Height = GridLength.Auto });
        page.RowDefinitions.Add(new()); page.RowDefinitions.Add(new() { Height = GridLength.Auto });
        SetBody(page, mode); backAction = back;
        var progress = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 15) };
        var names = new[] { "Protect", "Recover", "Unlock", "First secret" };
        for (var i = 0; i < names.Length; i++)
        {
            var part = new StackPanel { Margin = new Thickness(0, 0, i == 3 ? 0 : 5, 0), ToolTip = $"Step {i + 1} of 4 · {names[i]}" };
            part.Children.Add(new Border { Height = 3, CornerRadius = new CornerRadius(1.5), Background = (Brush)FindResource(i <= step ? "Accent" : "ControlSurface"), Opacity = i == step ? 1 : .45 });
            part.Children.Add(new TextBlock { Text = names[i], FontSize = 9, Opacity = i == step ? .9 : .4, Margin = new Thickness(0, 6, 0, 0) }); progress.Children.Add(part);
        }
        page.Children.Add(progress);
        var heading = new StackPanel(); var caption = Heading(title); caption.FontSize = 23; caption.Margin = new Thickness(0, 0, 0, 5); heading.Children.Add(caption);
        var description = Text(hint, 11, true); description.Margin = new Thickness(0, 0, 0, 13); heading.Children.Add(description); Grid.SetRow(heading, 1); page.Children.Add(heading);
        var stack = new StackPanel { Margin = new Thickness(1, 0, 5, 0) };
        var scroll = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 2); page.Children.Add(scroll);
        var footer = new Grid { Margin = new Thickness(0, 10, 0, 0) }; footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); footer.ColumnDefinitions.Add(new());
        if (back is not null)
        {
            var previous = Button("Back", GoBack); previous.Height = 38; previous.Margin = new Thickness(0, 0, 8, 0); previous.ToolTip = "Go back without losing what you entered"; footer.Children.Add(previous);
        }
        pageFooter = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch }; Grid.SetColumn(pageFooter, 1); footer.Children.Add(pageFooter);
        Grid.SetRow(footer, 3); page.Children.Add(footer);
        WelcomeMotion(page);
        return stack;
    }

    private void ShowSetupFirstSecret(SetupDraft draft)
    {
        TextBox? label = null; MaskedSecretBox? value = null;
        void Remember() { draft.FirstLabel = label!.Text; draft.FirstValue = value!.Password; }
        var stack = SetupPage(3, "Make it yours", "Save something you reach for. Only its label shows.", () => { Remember(); ShowSetupPin(draft); });
        label = Input(stack, "Label", draft.FirstLabel); label.ToolTip = "A name you recognize, like Home Wi-Fi. This is shown in your list.";
        stack.Children.Add(Text("Secret value", 11, true));
        var host = new Grid(); stack.Children.Add(host);
        value = new MaskedSecretBox { Password = draft.FirstValue, MinHeight = 44, MaxHeight = 74, Padding = new Thickness(10, 8, 38, 8), ToolTip = "A password, note or any text. Shift+Enter adds another line." };
        System.Windows.Automation.AutomationProperties.SetName(value, "First secret value"); host.Children.Add(value); eraseEditors.Add(value.Clear);
        var reveal = new TextBox { Visibility = Visibility.Collapsed, AcceptsReturn = true, MinHeight = 44, MaxHeight = 74, TextWrapping = TextWrapping.Wrap, IsUndoEnabled = false, Padding = new Thickness(10, 8, 38, 8), MaxLength = 100_000 };
        host.Children.Add(reveal); eraseEditors.Add(reveal.Clear);
        var graphic = new Grid { Width = 16, Height = 16, IsHitTestVisible = false };
        graphic.Children.Add(new TextBlock { Text = "\uE890", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 });
        var slash = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 2,14 L 14,2"), Stroke = Brushes.White, StrokeThickness = 1.3, Visibility = Visibility.Collapsed }; graphic.Children.Add(slash);
        var eye = Button("", () =>
        {
            var show = reveal.Visibility != Visibility.Visible;
            if (show) { reveal.Text = value.Password; reveal.Visibility = Visibility.Visible; value.Visibility = Visibility.Collapsed; reveal.Focus(); }
            else { value.Password = reveal.Text; reveal.Visibility = Visibility.Collapsed; reveal.Clear(); value.Visibility = Visibility.Visible; value.Focus(); }
            slash.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        });
        reveal.TextChanged += (_, _) => { if (reveal.IsVisible) value.Password = reveal.Text; };
        eye.Content = graphic; eye.Style = (Style)FindResource("RowIcon"); eye.HorizontalAlignment = HorizontalAlignment.Right; eye.VerticalAlignment = VerticalAlignment.Top; eye.Margin = new Thickness(0, 4, 4, 0); eye.ToolTip = "Reveal or hide the value"; System.Windows.Automation.AutomationProperties.SetName(eye, "Reveal first secret"); host.Children.Add(eye);
        var skip = AsyncButton("I'll add one later", () => FinishSetup(draft, false), false); skip.Opacity = .6; skip.FontSize = 11; skip.ToolTip = "Create an empty vault. Add a secret anytime with the + button."; stack.Children.Add(skip);
        PageAction(AsyncButton("Save & finish  →", async () =>
        {
            Remember();
            if (string.IsNullOrWhiteSpace(draft.FirstLabel) || draft.FirstLabel.Trim().Length > 300) { RejectInput(label, "Give your secret a label, up to 300 characters."); return; }
            if (draft.FirstValue.Length == 0) { RejectInput(value, "Add the value you want to keep."); return; }
            await FinishSetup(draft, true);
        }));
        Dispatcher.BeginInvoke(() => label.Focus());
    }

    private async Task FinishSetup(SetupDraft draft, bool saveFirst)
    {
        await RunVault(() => Task.Run(() =>
        {
            using var imported = draft.ImportFile is null ? null : Transfers.Open(ReadArchive(draft.ImportFile), draft.ImportPassword!);
            vault.Create(draft.Password, draft.Recovery);
            var config = draft.Pin.Length > 0 ? PinConfiguration.Create(draft.Pin) : null;
            try
            {
                vault.Mutate(d =>
                {
                    d.Settings.Startup = draft.Startup; d.Settings.PinPromptDismissed = true; d.QuickUnlock = config is null ? null : Codec.Clone(config);
                    if (saveFirst)
                    {
                        var item = new VaultItem { Label = draft.FirstLabel.Trim(), Payload = JsonSerializer.SerializeToElement(new { text = draft.FirstValue }) };
                        EntryMetadata.MarkSaved(item, null, DateTimeOffset.UtcNow); d.Items.Add(item);
                    }
                });
            }
            finally { if (config is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(config.Verifier); }
            if (imported is not null) Transfers.Import(vault, imported);
        }), () =>
        {
            draft.Password = draft.Recovery = draft.Pin = draft.FirstValue = ""; draft.ImportPassword = null;
            OnUnlocked(); ShowSetupReady(saveFirst);
        });
    }

    private void ShowSetupReady(bool saved)
    {
        var page = new Grid(); page.RowDefinitions.Add(new()); page.RowDefinitions.Add(new() { Height = GridLength.Auto }); SetBody(page, "onboarding-ready");
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 10) }; page.Children.Add(stack);
        stack.Children.Add(new TextBlock { Text = "✓", FontSize = 42, Foreground = new SolidColorBrush(Color.FromRgb(153, 195, 164)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 14) });
        var title = Heading("You're ready"); title.TextAlignment = TextAlignment.Center; stack.Children.Add(title);
        var caption = Text(saved ? "Your first secret is tucked away.\nClick its row whenever you need to copy it." : "A quiet place for your everyday essentials.\nUse + to add your first secret or login.", 12, true); caption.TextAlignment = TextAlignment.Center; stack.Children.Add(caption);
        var shortcut = Text(vault.Document!.Settings.Shortcut, 15); shortcut.TextAlignment = TextAlignment.Center; shortcut.Margin = new Thickness(0, 15, 0, 5); shortcut.ToolTip = "Open SableLatch from anywhere. Change this in Settings."; stack.Children.Add(shortcut);
        var tip = Text("Always close, in your tray", 10, true); tip.TextAlignment = TextAlignment.Center; stack.Children.Add(tip);
        var done = Button("Open my vault  →", ShowList, true); Grid.SetRow(done, 1); page.Children.Add(done); pageSubmit = done; WelcomeMotion(page);
    }
}
