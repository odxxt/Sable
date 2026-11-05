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
        var progress = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 15) };
        var names = new[] { "Password", "Recovery", "PIN" };
        for (var i = 0; i < names.Length; i++)
        {
            var part = new StackPanel { Margin = new Thickness(0, 0, i == 2 ? 0 : 5, 0), ToolTip = $"Step {i + 1} of 3 · {names[i]}" };
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

    private async Task FinishSetup(SetupDraft draft)
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
                    d.Settings.FirstEntryTourPending = true;
                });
            }
            finally { if (config is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(config.Verifier); }
            if (imported is not null) Transfers.Import(vault, imported);
        }), () =>
        {
            draft.Password = draft.Recovery = draft.Pin = ""; draft.ImportPassword = null;
            OnUnlocked();
        });
    }

    private void ShowSetupReady()
    {
        var page = new Grid(); page.RowDefinitions.Add(new()); page.RowDefinitions.Add(new() { Height = GridLength.Auto }); SetBody(page, "onboarding-ready");
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 10) }; page.Children.Add(stack);
        stack.Children.Add(new TextBlock { Text = "✓", FontSize = 42, Foreground = new SolidColorBrush(Color.FromRgb(153, 195, 164)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 14) });
        var title = Heading("Welcome to Sable"); title.TextAlignment = TextAlignment.Center; stack.Children.Add(title);
        var caption = Text("Setup is complete.\nLet's create your first secret.", 13, true); caption.TextAlignment = TextAlignment.Center; stack.Children.Add(caption);
        var shortcut = Text(vault.Document!.Settings.Shortcut, 15); shortcut.TextAlignment = TextAlignment.Center; shortcut.Margin = new Thickness(0, 15, 0, 5); shortcut.ToolTip = "Open Sable from anywhere. Change this in Settings."; stack.Children.Add(shortcut);
        var tip = Text("Always close, in your tray", 10, true); tip.TextAlignment = TextAlignment.Center; stack.Children.Add(tip);
        var skip = TourSkip(); stack.Children.Add(skip);
        var done = Button("Create my first secret  →", ShowList, true); Grid.SetRow(done, 1); page.Children.Add(done); pageSubmit = done; WelcomeMotion(page);
    }
}
