using System.Windows.Documents;
using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

internal sealed class InsertionMarker(UIElement host) : Adorner(host)
{
    public double Y { get; set; }
    protected override void OnRender(DrawingContext context)
    {
        var brush = (Brush)Application.Current.FindResource("Accent");
        context.DrawLine(new Pen(brush, 2), new Point(7, Y), new Point(Math.Max(7, ActualWidth - 7), Y));
        context.DrawEllipse(brush, null, new Point(7, Y), 2.5, 2.5);
    }
}

public partial class MainWindow
{
    private Guid? pendingLogin, activeDragItem;
    private long lastDragScroll;
    private Button? hoverEdit, dragHandle;
    private CancellationTokenSource? hoverCancellation;
    private Point dragStart;
    private InsertionMarker? insertionMarker;
    private AdornerLayer? insertionLayer;
    private int insertionIndex = -1;

    private void ResetLoginCopy()
    {
        holdForPassword = false; pendingLogin = null; Topmost = session.IsPinned;
        if (entryList is not null) foreach (var row in entryList.Items.Cast<RowModel>()) { row.PendingPassword = false; row.Refresh(); }
    }
    private void ArmDelete(RowModel row)
    {
        CancelEditHover(); armedDelete = row.Item.Id;
        if (entryList is not null) foreach (var model in entryList.Items.Cast<RowModel>()) { model.DeleteArmed = model.Item.Id == row.Item.Id; model.Refresh(); }
    }
    private void CancelEditHover()
    {
        hoverCancellation?.Cancel(); hoverCancellation?.Dispose(); hoverCancellation = null;
        if (hoverEdit?.DataContext is RowModel row) { row.DeleteReady = false; row.Refresh(); }
        hoverEdit = null;
    }
    private async void TrackRowPointer(object sender, MouseEventArgs e)
    {
        var button = FindParent<Button>(e.OriginalSource as DependencyObject);
        if (dragHandle is not null && e.LeftButton == MouseButtonState.Pressed && entryList is not null)
        {
            var point = e.GetPosition(entryList);
            if (Math.Abs(point.X - dragStart.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(point.Y - dragStart.Y) >= SystemParameters.MinimumVerticalDragDistance)
            {
                var handle = dragHandle; dragHandle = null; CancelEditHover();
                if (handle.DataContext is RowModel moving)
                {
                    activeDragItem = moving.Item.Id; modalDepth++; Mouse.Capture(null);
                    QueryContinueDragEventHandler cancel = (_, args) => { if (!vault.IsUnlocked || screen != "list") args.Action = DragAction.Cancel; };
                    handle.QueryContinueDrag += cancel;
                    try { DragDrop.DoDragDrop(handle, new DataObject("SableLatch/item-id", moving.Item.Id.ToString()), DragDropEffects.Move); }
                    finally { handle.QueryContinueDrag -= cancel; activeDragItem = null; modalDepth--; ClearDropMarker(); }
                }
                return;
            }
        }
        if (e.LeftButton != MouseButtonState.Pressed) dragHandle = null;
        if (button?.Tag as string != "edit") { CancelEditHover(); return; }
        if (ReferenceEquals(button, hoverEdit)) return;
        CancelEditHover(); hoverEdit = button;
        hoverCancellation = new(); var token = hoverCancellation.Token;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            if (token.IsCancellationRequested || !button.IsMouseOver || button.DataContext is not RowModel row || !vault.IsUnlocked) return;
            row.DeleteReady = true; row.Refresh();
            button.BeginAnimation(OpacityProperty, new DoubleAnimation(0, .8, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop });
        }
        catch (OperationCanceledException) { }
    }
    private void BeginRowDrag(object sender, MouseButtonEventArgs e)
    {
        var button = FindParent<Button>(e.OriginalSource as DependencyObject);
        if (armedDelete is not null && button?.Tag as string != "confirm-delete")
        {
            armedDelete = null;
            if (entryList is not null) foreach (var row in entryList.Items.Cast<RowModel>()) { row.DeleteArmed = false; row.Refresh(); }
        }
        if (button?.Tag as string != "copy" || !vault.IsUnlocked || busy || entryList is null) return;
        dragHandle = button; dragStart = e.GetPosition(entryList); button.Focus();
    }
    private void PreviewRowDrop(object sender, DragEventArgs e)
    {
        e.Handled = true; e.Effects = DragDropEffects.None;
        if (!vault.IsUnlocked || busy || entryList is null || !string.IsNullOrWhiteSpace(search) || !OwnDrag(e.Data, out _)) { ClearDropMarker(); return; }
        var point = e.GetPosition(entryList);
        UpdateDropPoint(point); e.Effects = DragDropEffects.Move;
    }
    private void UpdateDropPoint(Point point)
    {
        if (entryList is null) return;
        double y = 2; var index = 0;
        var realized = Descendants<ListBoxItem>(entryList).OrderBy(x => x.TranslatePoint(new Point(), entryList).Y).ToList();
        foreach (var li in realized)
        {
            var top = li.TranslatePoint(new Point(), entryList).Y;
            var actualIndex = entryList.ItemContainerGenerator.IndexFromContainer(li);
            if (point.Y < top + li.ActualHeight / 2) { index = actualIndex; y = top; break; }
            index = actualIndex + 1; y = top + li.ActualHeight;
        }
        insertionIndex = index;
        insertionLayer ??= AdornerLayer.GetAdornerLayer(entryList);
        if (insertionMarker is null && insertionLayer is not null) { insertionMarker = new(entryList) { IsHitTestVisible = false }; insertionLayer.Add(insertionMarker); }
        if (insertionMarker is not null) { insertionMarker.Y = Math.Clamp(y, 2, Math.Max(2, entryList.ActualHeight - 2)); insertionMarker.InvalidateVisual(); }
        var scroll = Descendants<ScrollViewer>(entryList).FirstOrDefault();
        if (Environment.TickCount64 - lastDragScroll >= 70 && (point.Y < 18 || point.Y > entryList.ActualHeight - 18))
        {
            lastDragScroll = Environment.TickCount64;
            if (scroll is not null) SmoothScrolling.Wheel(scroll, point.Y < 18 ? 40 : -40);
        }
    }
    private void ClearDropMarker()
    {
        if (insertionMarker is not null) insertionLayer?.Remove(insertionMarker);
        insertionMarker = null; insertionLayer = null; insertionIndex = -1;
    }
    private void DropRow(object sender, DragEventArgs e)
    {
        PreviewRowDrop(sender, e);
        var to = insertionIndex; ClearDropMarker();
        if (to < 0 || !vault.IsUnlocked || busy || !OwnDrag(e.Data, out var source)) return;
        PlaceRow(source, to);
    }
    private void PlaceRow(Guid source, int to)
    {
        var siblings = vault.Document!.Items.Where(i => i.FolderId == folder && i.DeletedAt is null).OrderBy(i => i.Order).ToList();
        var from = siblings.FindIndex(i => i.Id == source);
        if (from >= 0) MoveEntry(source, (to > from ? to - 1 : to) - from);
    }
}
