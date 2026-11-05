#if DEBUG
using System.Text;
using System.Windows.Automation;
namespace SableLatch.Desktop;

public partial class MainWindow
{
    private async Task CheckSableFeatures(Action<bool, string> check, Func<string, FrameworkElement?, Task> render)
    {
        Show(); folder = null; search = ""; session.IsPinned = true; Topmost = true;
        check(Title == "Sable" && BrandTitle.Text == "Sable", "App window and visible brand are Sable");
        ShowEditor(selectedType: "sable.shortcut"); UpdateLayout();
        check(!Descendants<MaskedSecretBox>(Body).Any() && !Descendants<PasswordBox>(Body).Any(), "Shortcut editor needs no secret value");
        Descendants<TextBox>(Body).Single(t => AutomationProperties.GetName(t) == "Label").Text = "Ping device";
        Descendants<ComboBox>(Body).Single().SelectedItem = "Ping"; UpdateLayout();
        Descendants<TextBox>(Body).Single(t => AutomationProperties.GetName(t) == "Action target or script").Text = "192.0.2.1";
        LogicalDescendants(Body).OfType<CheckBox>().Single(c => c.Content is TextBlock t && t.Text == "Suggest for matching clipboard").IsChecked = true;
        int launches = 0; string command = "";
        previewActionLaunch = start => { launches++; command = Encoding.Unicode.GetString(Convert.FromBase64String(start.ArgumentList.Last())); };
        var test = Descendants<Button>(Body).Single(b => b.Content as string == "Test action");
        test.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        check(launches == 1 && command.Contains("'192.0.2.1'") && command.Contains("ping.exe"), "Diagnostic test prepares an explicit validated command");
        await render("90-shortcut-editor", null);
        pageSubmit!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); UpdateLayout();
        var saved = vault.Document!.Items.Single(i => i.Label == "Ping device");
        check(saved.ContentType == "sable.shortcut" && saved.Field("text") == "" && saved.Advanced!.Action!.SuggestFromClipboard, "Save persists a standalone shortcut with matching preference");
        clipboard.ClearOwned(); var row = entryList!.Items.Cast<RowModel>().Single(r => r.Item.Id == saved.Id);
        RowClick(entryList, new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent, new Button { Tag = "copy", DataContext = row }));
        check(launches == 2 && ((PreviewClipboard)clipboard).LastText == "", "Clicking a shortcut runs it without copying anything");
        previewSuggestionText = "198.51.100.12"; RefreshSuggestions(); UpdateLayout();
        check(suggestionHost!.Children.Count == 1, "Only an opted-in matching shortcut appears above the list");
        check(Descendants<TextBlock>(suggestionHost).Any(t => t.Text == "198.51.100.12" && t.FontSize == 13) && Descendants<System.Windows.Shapes.Path>(suggestionHost).Any(), "Suggestion separates the readable address from its action label and run affordance");
        await render("91-suggestions", null);
        ((Button)suggestionHost.Children[0]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        check(launches == 3 && command.Contains("'198.51.100.12'") && saved.Advanced!.Action!.Target == "192.0.2.1", "Suggestion click substitutes a validated host without modifying the saved destination");
        previewSuggestionText = "example.com; whoami"; RefreshSuggestions(); check(suggestionHost.Children.Count == 0 && launches == 3, "Clipboard operators neither suggest nor execute");
        previewSuggestionText = "198.51.100.12"; vault.Mutate(d => d.Settings.ClipboardSuggestions = false); RefreshSuggestions(); check(suggestionHost.Children.Count == 0, "Global clipboard suggestion switch disables matching");
        vault.Mutate(d => d.Settings.ClipboardSuggestions = true); RefreshSuggestions(); Hide(); check(suggestionHost.Children.Count == 0, "Hiding Sable discards clipboard suggestion UI"); Show();
        previewSuggestionText = null; vault.Mutate(d => d.Items.Single(i => i.Id == saved.Id).Order = -1); ShowList(); UpdateLayout();
        var toggle = Descendants<Button>(Body).Single(b => AutomationProperties.GetName(b) == "Toggle entry layout"); toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); UpdateLayout();
        check(vault.Document.Settings.Layout == "grid", "View button saves the two-column layout preference");
        var tiles = Descendants<ListBoxItem>(entryList!).Take(2).ToArray();
        check(tiles.Length == 2 && Math.Abs(tiles[0].TranslatePoint(new(), entryList).Y - tiles[1].TranslatePoint(new(), entryList).Y) < 1 && tiles[1].TranslatePoint(new(), entryList).X > tiles[0].TranslatePoint(new(), entryList).X, "Grid places exactly two entries side by side");
        check(tiles.All(t => t.ActualHeight == 68), "Compact grid tiles use an equal 68-pixel slot");
        check(!Descendants<Button>(entryList!).Any(b => b.Tag as string == "edit") && Descendants<Button>(tiles[0]).Any(b => b.Tag as string == "entry-action" && b.IsVisible), "Grid removes pencil controls and keeps the shortcut run action on the right");
        var tileRow = (RowModel)tiles[0].DataContext;
        check(BuildEntryContext(tileRow).Items.OfType<MenuItem>().Any(m => m.Header as string == "Edit"), "Editing remains available from the grid context menu");
        var tileBounds = tiles[0].RenderSize; tileRow.Copied = true; tileRow.Refresh(); UpdateLayout();
        check(tiles[0].RenderSize == tileBounds && Descendants<TextBlock>(tiles[0]).Any(t => t.Tag as string == "copy-feedback" && t.IsVisible), "Copy confirmation replaces the action in place without resizing a tile"); tileRow.Copied = false; tileRow.Refresh();
        check(Descendants<Button>(tiles[0]).Where(b => b.Tag as string is "copy" or "entry-action").All(EntryPress.GetEnabled), "Tile activation and run buttons have lightweight press feedback");
        await render("92-grid", null);
        UpdateDropPoint(tiles[1].TranslatePoint(new Point(1, 40), entryList!)); check(insertionIndex == 1 && insertionMarker?.X is not null, "Grid dragging uses the tile edge instead of a row-wide insertion line"); ClearDropMarker();
        var temporary = new List<Guid>(); vault.Mutate(d => { for (var i = 0; i < 1000; i++) { var item = new VaultItem { Label = "Grid fixture " + i, Order = d.Items.Count }; d.Items.Add(item); temporary.Add(item.Id); } });
        ShowList(); UpdateLayout();
        check(Descendants<ListBoxItem>(entryList!).Count() < 24, "Grid virtualizes a thousand entries instead of creating every tile");
        var panel = Descendants<TwoColumnPanel>(entryList!).Single(); panel.SetVerticalOffset(double.PositiveInfinity); UpdateLayout();
        check(Descendants<ListBoxItem>(entryList!).Any(c => ((RowModel)c.DataContext).Item.Id == temporary[^1]) && Descendants<ListBoxItem>(entryList!).Count() < 24, "Virtualized grid reaches the last entry with bounded controls");
        vault.Mutate(d => d.Items.RemoveAll(i => temporary.Contains(i.Id))); ShowList(); UpdateLayout();
        Descendants<Button>(Body).Single(b => AutomationProperties.GetName(b) == "Toggle entry layout").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); UpdateLayout();
        check(vault.Document.Settings.Layout == "rows" && entryList!.Items.Cast<RowModel>().Any(r => r.Item.Id == saved.Id), "Switching back to rows retains shortcuts and entries");
        check(!Descendants<Button>(entryList!).Any(b => b.Tag as string == "edit"), "Rows also use context-menu editing without pencil controls");
        var rail = folderRail!; var chips = folderChips!.Children.OfType<Button>().ToArray();
        check(chips.Length > 4 && rail.ScrollableWidth > 0 && chips.Select(c => c.TranslatePoint(new(), folderChips).Y).Distinct().Count() == 1, "Extra folders overflow horizontally without wrapping into more rows");
        check(chips.All(c => Math.Abs(c.ActualWidth + c.Margin.Right - rail.ActualWidth / 4) < .5) && rail.ActualHeight == 36, "Folder strip has exactly four compact slots and a fixed height");
        check(FolderRail.ShouldBrowse(true, 100, new Vector(30, 1)) && !FolderRail.ShouldBrowse(true, 400, new Vector(30, 1)) && !FolderRail.ShouldBrowse(true, 100, new Vector(1, 30)), "Quick horizontal movement browses; a hold or vertical drag preserves folder movement");
        var ids = vault.Document.Folders.OrderBy(f => f.Order).Select(f => f.Id).ToArray();
        rail.PreviewPressed = _ => true; rail.PreviewPosition = _ => 80;
        rail.StartPan(240); rail.PanTo(80); UpdateLayout();
        check(rail.IsPanning && rail.HorizontalOffset > 0 && ids.SequenceEqual(vault.Document.Folders.OrderBy(f => f.Order).Select(f => f.Id)), $"Dragging the folder rail browses without changing folder order or opening one (panning={rail.IsPanning}, offset={rail.HorizontalOffset:0.0}, captured={rail.IsMouseCaptured})");
        rail.EndPan(true); rail.PreviewPressed = null; rail.PreviewPosition = null; await Task.Delay(200); UpdateLayout();
        check(!rail.IsPanning && !rail.IsMouseCaptured && Math.Abs(rail.HorizontalOffset / rail.SlotWidth - Math.Round(rail.HorizontalOffset / rail.SlotWidth)) < .01, "Folder browsing releases capture and settles smoothly on a folder boundary");
        rail.ScrollTo(rail.ScrollableWidth); await Task.Delay(200); UpdateLayout();
        var last = chips[^1].TranslatePoint(new(), rail);
        check(last.X >= 0 && last.X + chips[^1].ActualWidth <= rail.ActualWidth + 1, "All folders can be reached, including the final folder");
        var offset = rail.HorizontalOffset; ShowList(); await Task.Delay(40); UpdateLayout();
        check(Math.Abs(folderRail!.HorizontalOffset - offset) < 1, "Refreshing entries retains the folder browsing position");
        await render("93-folder-strip", null);
        previewActionLaunch = null;
    }
}
#endif
