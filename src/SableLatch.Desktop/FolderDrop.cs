using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

public partial class MainWindow
{
    private bool OwnDrag(IDataObject data, out Guid id)
    {
        id = default;
        return vault.IsUnlocked && !busy && activeDragItem is Guid expected && data.GetDataPresent("SableLatch/item-id", false)
            && Guid.TryParse(data.GetData("SableLatch/item-id", false) as string, out id) && expected == id
            && vault.Document!.Items.Any(i => i.Id == expected && i.DeletedAt is null);
    }
    private Dictionary<Guid, double> RowPositions() => entryList is null ? [] : Descendants<ListBoxItem>(entryList)
        .Where(c => c.DataContext is RowModel).ToDictionary(c => ((RowModel)c.DataContext).Item.Id, c => c.TranslatePoint(new Point(), entryList).Y);
    private void AnimateRows(Dictionary<Guid, double> before)
    {
        if (entryList is null || !SystemParameters.ClientAreaAnimation) return;
        entryList.UpdateLayout();
        foreach (var row in Descendants<ListBoxItem>(entryList))
        {
            if (row.DataContext is not RowModel model || !before.TryGetValue(model.Item.Id, out var oldY)) continue;
            row.RenderTransform = Transform.Identity;
            var offset = oldY - row.TranslatePoint(new Point(), entryList).Y;
            if (Math.Abs(offset) < .5) continue;
            var transform = new TranslateTransform(); row.RenderTransform = transform;
            transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(180))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        }
    }
    private void AttachFolderDrop(Button target, Guid? destination)
    {
        target.AllowDrop = true;
        var normal = target.Background;
        void Preview(object sender, DragEventArgs e)
        {
            e.Handled = true; e.Effects = DragDropEffects.None; ClearDropMarker();
            if (!OwnDrag(e.Data, out var id) || vault.Document!.Items.Single(i => i.Id == id).FolderId == destination) return;
            e.Effects = DragDropEffects.Move; target.Background = (Brush)FindResource("HoverSurface");
        }
        target.DragEnter += Preview; target.DragOver += Preview;
        target.DragLeave += (_, _) => target.Background = normal;
        target.Drop += (_, e) =>
        {
            e.Handled = true; target.Background = normal;
            if (OwnDrag(e.Data, out var id) && MoveIntoFolder(id, destination))
            {
                e.Effects = DragDropEffects.Move; FlashFolder(target);
            }
        };
    }
    private bool MoveIntoFolder(Guid id, Guid? destination)
    {
        if (!vault.IsUnlocked || busy || (destination is Guid folderId && !vault.Document!.Folders.Any(f => f.Id == folderId))) return false;
        var item = vault.Document!.Items.SingleOrDefault(i => i.Id == id && i.DeletedAt is null);
        if (item is null || item.FolderId == destination) return false;
        var before = RowPositions();
        if (!Change(d =>
        {
            var moving = d.Items.Single(i => i.Id == id);
            moving.FolderId = destination;
            moving.Order = d.Items.Where(i => i.Id != id && i.FolderId == destination && i.DeletedAt is null).Select(i => i.Order).DefaultIfEmpty(-1).Max() + 1;
        })) return false;
        ResetLoginCopy(); PopulateRows(); AnimateRows(before); return true;
    }
    private async void FlashFolder(Button target)
    {
        if (target.Content is StackPanel { Tag: "folder-confirmation" }) return;
        var original = target.Content;
        var symbol = new TextBlock { Text = "✓", Foreground = new SolidColorBrush(Color.FromRgb(153, 195, 164)), VerticalAlignment = VerticalAlignment.Center };
        var host = new StackPanel { Orientation = Orientation.Horizontal, Tag = "folder-confirmation" };
        target.Content = null; host.Children.Add(symbol);
        var presenter = new ContentPresenter { Content = original, Margin = new Thickness(6, 0, 0, 0) }; host.Children.Add(presenter); target.Content = host;
        if (SystemParameters.ClientAreaAnimation) symbol.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
        await Task.Delay(750);
        if (ReferenceEquals(target.Content, host)) { target.Content = null; host.Children.Clear(); presenter.Content = null; target.Content = original; }
    }
}
