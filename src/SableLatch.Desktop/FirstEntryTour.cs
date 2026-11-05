using System.Windows.Controls.Primitives;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private ToolTip? firstEntryTip;
    private bool FirstEntryTour => vault.IsUnlocked && vault.Document!.Settings.FirstEntryTourPending;
    private void CloseTourHint()
    {
        if (firstEntryTip is not null) { firstEntryTip.IsOpen = false; firstEntryTip.Content = null; firstEntryTip.PlacementTarget = null; firstEntryTip = null; }
    }
    private void TourHint(FrameworkElement target, string title, string description, PlacementMode placement = PlacementMode.Bottom)
    {
        if (!FirstEntryTour || !target.IsLoaded || !target.IsVisible) return;
        if (firstEntryTip?.PlacementTarget == target) return;
        CloseTourHint();
        var content = new StackPanel { MaxWidth = 210 };
        content.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
        content.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, FontSize = 11, Opacity = .7, Margin = new Thickness(0, 5, 0, 0) });
        firstEntryTip = new ToolTip { Content = content, PlacementTarget = target, Placement = placement, StaysOpen = true, IsHitTestVisible = false, Focusable = false };
        firstEntryTip.IsOpen = true;
    }
    private Button TourSkip()
    {
        var skip = Button("Skip", () => { if (Change(d => d.Settings.FirstEntryTourPending = false)) { CloseTourHint(); ShowList(); } });
        skip.Background = Brushes.Transparent; skip.Opacity = .55; skip.FontSize = 11; skip.HorizontalAlignment = HorizontalAlignment.Center;
        skip.ToolTip = "Finish without the guide. You can add entries whenever you like."; return skip;
    }
    private void GuideFirstEntry(Button create, ContextMenu menu, MenuItem newEntry, Grid viewport)
    {
        if (!FirstEntryTour) return;
        create.Background = (Brush)FindResource("HoverSurface");
        var invitation = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        invitation.Children.Add(Text("Create your first secret", 17));
        invitation.Children.Add(Text("Start with the + button above.", 12, true)); invitation.Children.Add(TourSkip()); viewport.Children.Add(invitation);
        void PromptCreate() => Dispatcher.BeginInvoke(() => { if (screen == "list" && !menu.IsOpen) TourHint(create, "1 · Click +", "This is where you add entries and folders.", PlacementMode.Left); });
        create.Loaded += (_, _) => PromptCreate();
        create.IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) PromptCreate(); };
        menu.Opened += (_, _) =>
        {
            CloseTourHint(); newEntry.Background = (Brush)FindResource("HoverSurface"); newEntry.FontWeight = FontWeights.SemiBold;
            Dispatcher.BeginInvoke(() => { if (menu.IsOpen) TourHint(newEntry, "2 · New entry", "Choose this to create your secret.", PlacementMode.Left); });
        };
        menu.Closed += (_, _) => { if (firstEntryTip?.PlacementTarget == newEntry || firstEntryTip?.PlacementTarget == create) CloseTourHint(); if (screen == "list") TourHint(create, "1 · Click +", "Choose New entry to continue.", PlacementMode.Left); };
    }
    private void GuideSecretFields(TextBox label, MaskedSecretBox value, Button save)
    {
        if (!FirstEntryTour) return;
        void Update()
        {
            if (!FirstEntryTour || screen != "editor" || !label.IsLoaded) return;
            if (string.IsNullOrWhiteSpace(label.Text)) TourHint(label, "3 · Name your secret", "Choose a label you'll recognize. Only this label appears in the list.");
            else if (value.Password.Length == 0) TourHint(value, "4 · Add the value", "A password, note or any text. The eye lets you check it.");
            else { save.Background = (Brush)FindResource("HoverSurface"); TourHint(save, "5 · Save your secret", "Then click its row whenever you want to copy the value.", PlacementMode.Top); }
        }
        label.Loaded += (_, _) => Dispatcher.BeginInvoke(Update);
        label.IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) Dispatcher.BeginInvoke(Update); };
        label.TextChanged += (_, _) => Update(); value.TextChanged += (_, _) => Update();
    }
}
