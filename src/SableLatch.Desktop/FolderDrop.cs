using System.Windows.Media.Animation;
using System.Windows.Documents;

namespace SableLatch.Desktop;

// Adorners never participate in the button's content measurement or sibling layout.
internal sealed class FolderConfirmationAdorner(UIElement target) : Adorner(target)
{
    protected override void OnRender(DrawingContext context)
    {
        var center = new Point(AdornedElement.RenderSize.Width / 2, AdornedElement.RenderSize.Height / 2);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(center.X - 5, center.Y), false, false);
            path.LineTo(new Point(center.X - 1, center.Y + 4), true, false);
            path.LineTo(new Point(center.X + 6, center.Y - 5), true, false);
        }
        context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(153, 195, 164)), 1.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, geometry);
    }
}

public partial class MainWindow
{
    private readonly HashSet<Button> folderConfirmations = [];
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
            e.Handled = true; e.Effects = DragDropEffects.None; ClearDropMarker(); ClearFolderMarker(); target.Background = normal;
            if (OwnFolderDrag(e.Data, out var moving))
            {
                if (target.Tag is Guid sibling && sibling != moving)
                {
                    var edge = FolderDropEdge(e.GetPosition(target).X, target.ActualWidth);
                    if (edge != 0) { ShowFolderMarker(target, edge > 0); e.Effects = DragDropEffects.Move; return; }
                }
                if (!FolderOperations.CanMove(vault.Document!, moving, destination) || vault.Document!.Folders.Single(f => f.Id == moving).ParentId == destination) return;
            }
            else if (!OwnDrag(e.Data, out var id) || vault.Document!.Items.Single(i => i.Id == id).FolderId == destination) return;
            e.Effects = DragDropEffects.Move; target.Background = (Brush)FindResource("HoverSurface");
        }
        target.DragEnter += Preview; target.DragOver += Preview;
        target.DragLeave += (_, _) => { target.Background = normal; ClearFolderMarker(); };
        target.Drop += (_, e) =>
        {
            e.Handled = true; e.Effects = DragDropEffects.None; target.Background = normal; ClearFolderMarker();
            if (OwnFolderDrag(e.Data, out var moving))
            {
                var edge = target.Tag is Guid sibling && sibling != moving ? FolderDropEdge(e.GetPosition(target).X, target.ActualWidth) : 0;
                if (edge != 0 && destination is Guid siblingId)
                {
                    if (PlaceFolder(moving, FolderSlot(siblingId, edge))) e.Effects = DragDropEffects.Move;
                }
                else if (MoveFolderInto(moving, destination))
                {
                    e.Effects = DragDropEffects.Move;
                    if (folderChips?.Children.OfType<Button>().FirstOrDefault(b => b.Tag is Guid id && id == destination) is { } newTarget) _ = FlashFolder(newTarget);
                }
                return;
            }
            if (OwnDrag(e.Data, out var id) && MoveIntoFolder(id, destination))
            {
                e.Effects = DragDropEffects.Move; _ = FlashFolder(target);
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
    private async Task FlashFolder(Button target)
    {
        target.UpdateLayout();
        var original = target.Content as UIElement ?? Descendants<ContentPresenter>(target).FirstOrDefault();
        var layer = AdornerLayer.GetAdornerLayer(target);
        if (original is null || layer is null || !folderConfirmations.Add(target)) return;
        var opacity = original.Opacity;
        var symbol = new FolderConfirmationAdorner(target) { IsHitTestVisible = false, Opacity = 0 };
        layer.Add(symbol);
        void Fade(UIElement element, double to, int duration)
        {
            if (SystemParameters.ClientAreaAnimation) element.BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(duration)));
            else element.Opacity = to;
        }
        try
        {
            Fade(original, 0, 130); await Task.Delay(130); Fade(symbol, 1, 130);
            await Task.Delay(470); Fade(symbol, 0, 130); await Task.Delay(130); Fade(original, opacity, 160); await Task.Delay(160);
        }
        finally
        {
            original.BeginAnimation(OpacityProperty, null); original.Opacity = opacity;
            layer.Remove(symbol); folderConfirmations.Remove(target);
        }
    }
}
