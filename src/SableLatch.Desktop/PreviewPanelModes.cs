#if DEBUG
namespace SableLatch.Desktop;

public partial class MainWindow
{
    private async Task PreviewPanelModes(Action<bool, string> check, Func<string, Task> render)
    {
        void Cycle() { TogglePin(PinButton, new RoutedEventArgs()); UpdateLayout(); }
        void Appearance() { UpdatePanelTopmost(); ApplyTaskbarVisibility(); UpdatePinButton(); UpdateLayout(); }
        vault.Mutate(d => { d.Settings.TaskbarMode = "pinned"; d.Settings.CloseAfterCopy = true; d.Settings.LockEveryOpen = false; });
        session.Mode = PanelMode.Tray; ShowList(); Show(); Appearance();
        var buttonSize = PinButton.RenderSize; var lockPosition = LockButton.TranslatePoint(new(), Header); var searchPosition = SearchButton.TranslatePoint(new(), Header);
        check(!Topmost && !ShowInTaskbar && !session.KeepsPanelOpen, "Tray mode has no taskbar entry and is not topmost");
        await render("10-tray-mode");
        Cycle();
        check(session.Mode == PanelMode.AlwaysOnTop && Topmost && ShowInTaskbar && session.KeepsPanelOpen, "First pin click enables always-on-top and taskbar visibility");
        HandlePanelDeactivated(); check(IsVisible, "Always-on-top mode stays visible on deactivation");
        await render("11-topmost-mode");
        Cycle();
        check(session.Mode == PanelMode.Window && !Topmost && ShowInTaskbar && session.KeepsPanelOpen, "Second pin click creates a normal taskbar window without topmost");
        HandlePanelDeactivated(); check(IsVisible, "Normal window remains open when another application takes focus");
        check(((Grid)PinButton.Content).RenderSize == new Size(16, 16) && PinButton.ToolTip.ToString()!.Contains("timer active"), "Normal window has a fixed-size icon and explains its active timer");
        await render("12-normal-window");
        Cycle();
        check(session.Mode == PanelMode.Tray && !Topmost && !ShowInTaskbar, "Third pin click returns to tray mode");
        check(PinButton.RenderSize == buttonSize && LockButton.TranslatePoint(new(), Header) == lockPosition && SearchButton.TranslatePoint(new(), Header) == searchPosition, "Cycling modes never moves or resizes neighboring toolbar controls");
        HandlePanelDeactivated(); check(!IsVisible, "Tray mode still hides on deactivation");
        OpenPanel(); Cycle(); Cycle();
        var login = vault.Document!.Items.First(i => i.ContentType == "sable.login"); ShowList(); UpdateLayout();
        var row = entryList!.Items.Cast<RowModel>().Single(r => r.Item.Id == login.Id);
        async Task Copy()
        {
            RowClick(entryList!, new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, new Button { Tag = "copy", DataContext = row }));
            await Task.Delay(240);
        }
        await Copy(); check(holdForPassword && IsVisible && !Topmost && ShowInTaskbar, "Normal-window username copying stays open without forcing topmost");
        HandlePanelDeactivated(); check(IsVisible && !Topmost, "Switching applications during normal-window login remains possible");
        await Copy(); check(!holdForPassword && IsVisible && !Topmost && ShowInTaskbar, "Normal-window password copying remains open despite tray close-after-copy preference");
        RowClick(entryList!, new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, new Button { Tag = "entry-action", DataContext = row })); await Task.Delay(240);
        check(holdForPassword && IsVisible && !Topmost && ((PreviewClipboard)clipboard).LastText == login.Field("username"), "Run and copy also respects normal-window stacking");
        await Copy();
        foreach (var visibility in new[] { "always", "never", "pinned" })
        {
            vault.Mutate(d => d.Settings.TaskbarMode = visibility); Appearance();
            check(ShowInTaskbar == (visibility != "never") && !Topmost, "Normal-window mode honors explicit taskbar preference: " + visibility);
        }
        vault.Mutate(d => d.Settings.TaskbarMode = "pinned"); Appearance();
        ShowEditor(login); UpdateLayout();
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var advanced = Descendants<Button>(Body).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Advanced");
        var editorScroll = FindParent<ScrollViewer>(advanced)!;
        editorScroll.ScrollToVerticalOffset(advanced.TranslatePoint(new(), editorScroll).Y + editorScroll.VerticalOffset); await render("13-advanced-spacing");
        var details = Descendants<Button>(Body).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Color & description");
        details.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); UpdateLayout();
        var scroll = FindParent<ScrollViewer>(details)!; scroll.ScrollToBottom(); await render("14-details-spacing");
        ShowEntryStarters();
    }
}
#endif
