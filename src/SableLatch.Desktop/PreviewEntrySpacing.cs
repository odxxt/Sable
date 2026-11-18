#if DEBUG
using System.Text.Json;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private async Task PreviewEntrySpacing(Action<bool, string> check, Func<string, Task> render)
    {
        var dimensions = new Size(Width, Height);
        void Toggle() { SwitchEntryLayout(); UpdateLayout(); }
        void Spacing(string mode)
        {
            var model = entryList!.Items.Cast<RowModel>().Single(r => r.Item.Label == "Server token");
            var container = (ListBoxItem)entryList.ItemContainerGenerator.ContainerFromItem(model);
            var card = Descendants<Border>(container).Single(b => b.Name == (mode == "rows" ? "Row" : "Tile"));
            var main = Descendants<Button>(container).Single(b => b.Tag as string == "copy");
            var action = Descendants<Button>(container).Single(b => b.Tag as string == "entry-action");
            var icon = Descendants<EntryIdentity>(main).Single(); var run = Descendants<EntryIdentity>(action).Single();
            var label = Descendants<TextBlock>(main).Single(t => t.Text == model.Title);
            var left = icon.TranslatePoint(new(), card).X; var gap = label.TranslatePoint(new(), card).X - left - icon.ActualWidth;
            var right = card.ActualWidth - run.TranslatePoint(new Point(run.ActualWidth, 0), card).X;
            check(card.ActualHeight == 56 && container.ActualHeight == 60, mode + " keeps a 56px entry inside the shared 60px rhythm");
            check(Math.Abs(left - 12) < .1 && Math.Abs(gap - 12) < .1, $"{mode} matches the approved left inset and icon-to-text clearance (inset {left:0.##}, gap {gap:0.##}, icon {icon.ActualWidth:0.##}/{icon.Width:0.##}, run {run.ActualWidth:0.##}/{run.Width:0.##})");
            check(icon.ActualWidth == 18 && run.ActualWidth == 16 && right >= 13.9 && action.ActualWidth == 28 && action.ActualHeight == 32, mode + " preserves a comfortable action target around the smaller inset Run icon");
        }
        Spacing("grid");
        await render("10-approved-grid-padding");
        Toggle(); Spacing("rows"); var rows = entryList!;
        check(CurrentEntryLayout == "rows" && new Size(Width, Height) == dimensions, "Switching to rows preserves the fixed window dimensions");
        if (SystemParameters.ClientAreaAnimation) check(rows.HasAnimatedProperties, "Explicit view switching starts one short opacity transition");
        await Task.Delay(200); check(rows.Opacity == 1 && !rows.HasAnimatedProperties, "The view transition settles opaque and releases its clock");
        await render("11-approved-row-padding");
        Toggle(); var first = entryList!; Toggle();
        check(!first.HasAnimatedProperties && first.Opacity == 1, "A rapid layout change cancels the previous view's fade");
        Toggle(); await Task.Delay(200); Spacing("grid");
        var group = vault.Document!.Folders.Single(); folder = group.Id; ShowList(); UpdateLayout();
        check(CurrentEntryLayout == "rows", "Folder layout remains independent of the root layout");
        Toggle(); await Task.Delay(200); folder = null; ShowList(); UpdateLayout();
        check(CurrentEntryLayout == "grid", "Changing a folder view does not change the root preference");
        var saved = vault.Document.Items.Select(i => i.Id).ToHashSet();
        vault.Mutate(d =>
        {
            for (var i = 0; i < 16; i++) d.Items.Add(new() { Label = "Spacing example " + i, Order = i + 10, Payload = JsonSerializer.SerializeToElement(new { text = "fictional-spacing-value" }) });
        });
        ShowList(); UpdateLayout();
        var viewer = Descendants<ScrollViewer>(entryList!).Single(); viewer.ScrollToEnd();
        await Task.Delay(100); UpdateLayout();
        var last = entryList!.Items.Cast<RowModel>().Last(); var lastContainer = entryList.ItemContainerGenerator.ContainerFromItem(last) as ListBoxItem;
        check(lastContainer is not null && lastContainer.TranslatePoint(new(), viewer).Y >= 0 && lastContainer.TranslatePoint(new Point(0, lastContainer.ActualHeight), viewer).Y <= viewer.ActualHeight + 1, "The shortened virtualized grid scrolls all the way to its last row");
        check(Descendants<TwoColumnPanel>(entryList).Single().ExtentHeight == Math.Ceiling(entryList.Items.Count / 2d) * 60, "Grid scroll extent follows the new shared entry pitch");
        vault.Mutate(d => { d.Items.RemoveAll(i => !saved.Contains(i.Id)); d.Folders.Single().Layout = "rows"; });
        ShowList(); UpdateLayout();
    }
}
#endif
