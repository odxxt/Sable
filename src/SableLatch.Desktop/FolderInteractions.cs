using System.Windows.Documents;
using System.Windows.Media.Animation;

namespace SableLatch.Desktop;

internal sealed class FolderInsertionMarker(UIElement host) : Adorner(host)
{
    public Point Position { get; set; }
    public double LineHeight { get; set; }
    protected override void OnRender(DrawingContext context)
    {
        var pen = new Pen((Brush)Application.Current.FindResource("Accent"), 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        context.DrawLine(pen, Position, new Point(Position.X, Position.Y + LineHeight));
    }
}

public partial class MainWindow
{
    private WrapPanel? folderChips;
    private Guid? activeDragFolder;
    private FolderInsertionMarker? folderMarker;
    private AdornerLayer? folderMarkerLayer;
#if DEBUG
    // Preview harness supplies pointer state and replaces only the native modal drag loop.
    private Func<MouseEventArgs, Point>? previewFolderPointer;
    private Func<MouseEventArgs, bool>? previewFolderPressed;
    private Action<Button, IDataObject>? previewFolderDrag;
#endif

    private bool OwnFolderDrag(IDataObject data, out Guid id)
    {
        id = default;
        return vault.IsUnlocked && !busy && activeDragFolder is Guid expected && data.GetDataPresent("SableLatch/folder-id", false)
            && Guid.TryParse(data.GetData("SableLatch/folder-id", false) as string, out id) && id == expected
            && vault.Document!.Folders.Any(f => f.Id == expected);
    }

    private void AttachFolderDrag(Button chip, Guid id)
    {
        Point start = default; var pending = false;
        Point Position(MouseEventArgs e)
        {
#if DEBUG
            if (previewFolderPointer is not null) return previewFolderPointer(e);
#endif
            return e.GetPosition(this);
        }
        bool Pressed(MouseEventArgs e)
        {
#if DEBUG
            if (previewFolderPressed is not null) return previewFolderPressed(e);
#endif
            return e.LeftButton == MouseButtonState.Pressed;
        }
        chip.PreviewMouseLeftButtonDown += (_, e) => { pending = vault.IsUnlocked && !busy && screen == "list"; start = Position(e); };
        chip.PreviewMouseLeftButtonUp += (_, _) => pending = false;
        chip.LostMouseCapture += (_, _) => pending = false;
        // ButtonBase handles MouseMove while pressed. A normal event subscription never
        // receives that movement; listen even when the button has already handled it.
        chip.AddHandler(Mouse.MouseMoveEvent, new MouseEventHandler((_, e) =>
        {
            if (!pending || !Pressed(e)) { pending = false; return; }
            var point = Position(e);
            if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            pending = false; if (!vault.IsUnlocked || busy || screen != "list") return;
            activeDragFolder = id; modalDepth++; Mouse.Capture(null); e.Handled = true;
            QueryContinueDragEventHandler cancel = (_, args) => { if (!vault.IsUnlocked || screen != "list") args.Action = DragAction.Cancel; };
            chip.QueryContinueDrag += cancel;
            try
            {
                var data = new DataObject("SableLatch/folder-id", id.ToString());
#if DEBUG
                if (previewFolderDrag is not null) previewFolderDrag(chip, data);
                else
#endif
                DragDrop.DoDragDrop(chip, data, DragDropEffects.Move);
            }
            finally { chip.QueryContinueDrag -= cancel; activeDragFolder = null; modalDepth--; ClearFolderMarker(); }
        }), handledEventsToo: true);
        chip.PreviewKeyDown += (_, e) =>
        {
            if (!vault.IsUnlocked || busy || Keyboard.Modifiers != ModifierKeys.Alt || e.Key is not (Key.Left or Key.Right)) return;
            var siblings = vault.Document!.Folders.Where(f => f.ParentId == folder).OrderBy(f => f.Order).ToList();
            var from = siblings.FindIndex(f => f.Id == id);
            PlaceFolder(id, e.Key == Key.Left ? from - 1 : from + 2); e.Handled = true;
            folderChips?.Children.OfType<Button>().FirstOrDefault(b => b.Tag is Guid value && value == id)?.Focus();
        };
    }

    private Dictionary<Guid, Point> FolderPositions()
    {
        if (folderChips is null) return [];
        foreach (var chip in folderChips.Children.OfType<Button>()) chip.RenderTransform = Transform.Identity;
        return folderChips.Children.OfType<Button>().Where(b => b.Tag is Guid).ToDictionary(b => (Guid)b.Tag, b => b.TranslatePoint(new Point(), folderChips));
    }
    private void AnimateFolders(Dictionary<Guid, Point> positions)
    {
        if (folderChips is null || !SystemParameters.ClientAreaAnimation) return;
        UpdateLayout();
        foreach (var chip in folderChips.Children.OfType<Button>())
        {
            if (chip.Tag is not Guid id || !positions.TryGetValue(id, out var before)) continue;
            var after = chip.TranslatePoint(new Point(), folderChips);
            var transform = new TranslateTransform(); chip.RenderTransform = transform;
            DoubleAnimation Slide(double value) => new(value, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            transform.BeginAnimation(TranslateTransform.XProperty, Slide(before.X - after.X));
            transform.BeginAnimation(TranslateTransform.YProperty, Slide(before.Y - after.Y));
        }
    }
    private bool MoveFolderInto(Guid id, Guid? destination)
    {
        if (!vault.IsUnlocked || busy || !FolderOperations.CanMove(vault.Document!, id, destination)) return false;
        if (vault.Document!.Folders.Single(f => f.Id == id).ParentId == destination) return false;
        var positions = FolderPositions();
        if (!Change(d => FolderOperations.Move(d, id, destination))) return false;
        ShowList(); AnimateFolders(positions); return true;
    }
    private bool PlaceFolder(Guid id, int insertion)
    {
        if (!vault.IsUnlocked || busy) return false;
        var siblings = vault.Document!.Folders.Where(f => f.ParentId == folder).OrderBy(f => f.Order).ToList();
        var from = siblings.FindIndex(f => f.Id == id); if (from < 0) return false;
        var index = Math.Clamp(insertion > from ? insertion - 1 : insertion, 0, siblings.Count - 1);
        if (from == index) return false;
        var positions = FolderPositions();
        if (!Change(d => FolderOperations.Move(d, id, folder, index))) return false;
        ShowList(); AnimateFolders(positions); return true;
    }
    // Outer quarters reorder siblings; the center is an explicit nesting target.
    private static int FolderDropEdge(double x, double width) => x < width * .25 ? -1 : x > width * .75 ? 1 : 0;
    private int FolderSlot(Guid target, int edge)
    {
        var siblings = vault.Document!.Folders.Where(f => f.ParentId == folder).OrderBy(f => f.Order).ToList();
        return siblings.FindIndex(f => f.Id == target) + (edge > 0 ? 1 : 0);
    }
    private void ShowFolderMarker(Button target, bool after)
    {
        if (folderChips is null) return;
        folderMarkerLayer ??= AdornerLayer.GetAdornerLayer(folderChips);
        if (folderMarker is null && folderMarkerLayer is not null) { folderMarker = new(folderChips) { IsHitTestVisible = false }; folderMarkerLayer.Add(folderMarker); }
        if (folderMarker is null) return;
        var point = target.TranslatePoint(new Point(after ? target.ActualWidth : 0, 4), folderChips);
        folderMarker.Position = point; folderMarker.LineHeight = Math.Max(4, target.ActualHeight - 8); folderMarker.InvalidateVisual();
    }
    private void ClearFolderMarker()
    {
        if (folderMarker is not null) folderMarkerLayer?.Remove(folderMarker);
        folderMarker = null; folderMarkerLayer = null;
    }
    private void AttachFolderPanelDrop(WrapPanel panel)
    {
        panel.AllowDrop = true; panel.Background = Brushes.Transparent;
        panel.DragOver += (_, e) =>
        {
            e.Handled = true; e.Effects = DragDropEffects.None;
            if (!OwnFolderDrag(e.Data, out var moving) || panel.Children.OfType<Button>().LastOrDefault() is not { } last) return;
            ShowFolderMarker(last, true); e.Effects = DragDropEffects.Move;
        };
        panel.DragLeave += (_, _) => ClearFolderMarker();
        panel.Drop += (_, e) =>
        {
            e.Handled = true; e.Effects = DragDropEffects.None; ClearFolderMarker();
            if (OwnFolderDrag(e.Data, out var id) && PlaceFolder(id, panel.Children.Count)) e.Effects = DragDropEffects.Move;
        };
    }
    private Dictionary<string, Guid?> FolderDestinations(Guid? moving)
    {
        var result = new Dictionary<string, Guid?> { ["All items (root)"] = null };
        foreach (var candidate in vault.Document!.Folders.OrderBy(f => FolderPath(f.Id), StringComparer.CurrentCultureIgnoreCase))
            if (moving is null || FolderOperations.CanMove(vault.Document, moving.Value, candidate.Id)) result[FolderPath(candidate.Id) + " · " + candidate.Id.ToString()[..4]] = candidate.Id;
        return result;
    }
    private void ShowMoveFolder(VaultFolder moving)
    {
        var stack = Form("Move folder", moving.Name); var destinations = FolderDestinations(moving.Id);
        var pick = Select(stack, "Destination folder", destinations.Keys, destinations.First(p => p.Value == moving.ParentId).Key);
        pick.Loaded += (_, _) => { pick.Focus(); pick.IsDropDownOpen = true; };
        PageAction(Button("Move here", () =>
        {
            var destination = destinations[(string)pick.SelectedItem];
            if (destination == moving.ParentId) ShowList(); else MoveFolderInto(moving.Id, destination);
        }, true));
    }
}
