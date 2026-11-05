using System.Windows.Controls.Primitives;

namespace SableLatch.Desktop;

// Fixed two-column tiles; realize only visible rows plus one row on each side.
public sealed class TwoColumnPanel : VirtualizingPanel, IScrollInfo
{
    internal const double TileHeight = 68;
    private double offset, width, height, extent;
    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; } = true;
    public double ExtentWidth => width;
    public double ExtentHeight => extent;
    public double ViewportWidth => width;
    public double ViewportHeight => height;
    public double HorizontalOffset => 0;
    public double VerticalOffset => offset;
    public ScrollViewer ScrollOwner { get; set; } = null!;
    protected override Size MeasureOverride(Size available)
    {
        var owner = ItemsControl.GetItemsOwner(this); if (owner is null) return new();
        width = double.IsFinite(available.Width) ? available.Width : 310;
        height = double.IsFinite(available.Height) ? available.Height : 270;
        extent = Math.Ceiling(owner.Items.Count / 2d) * TileHeight;
        offset = Math.Clamp(offset, 0, Math.Max(0, extent - height));
        var first = Math.Max(0, (int)(offset / TileHeight) - 1) * 2;
        var last = Math.Min(owner.Items.Count - 1, ((int)((offset + height) / TileHeight) + 2) * 2 - 1);
        _ = InternalChildren; // Initializes the generator association for a newly mounted items panel.
        var generator = ItemContainerGenerator;
        for (int i = InternalChildren.Count - 1; i >= 0; i--)
        {
            var index = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            if (index < first || index > last) { generator.Remove(new GeneratorPosition(i, 0), 1); RemoveInternalChildRange(i, 1); }
        }
        if (last >= first)
        {
            var start = generator.GeneratorPositionFromIndex(first);
            var childIndex = start.Offset == 0 ? start.Index : start.Index + 1;
            using (generator.StartAt(start, GeneratorDirection.Forward, true))
                for (var index = first; index <= last; index++, childIndex++)
                {
                    var child = (UIElement)generator.GenerateNext(out var fresh);
                    if (fresh) { InsertInternalChild(childIndex, child); generator.PrepareItemContainer(child); }
                    child.Measure(new Size(width / 2, TileHeight));
                }
        }
        ScrollOwner?.InvalidateScrollInfo(); return new(width, height);
    }
    protected override Size ArrangeOverride(Size final)
    {
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var index = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            InternalChildren[i].Arrange(new Rect((index % 2) * final.Width / 2, (index / 2) * TileHeight - offset, final.Width / 2, TileHeight));
        }
        return final;
    }
    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args) { ItemContainerGenerator.RemoveAll(); RemoveInternalChildRange(0, InternalChildren.Count); InvalidateMeasure(); }
    protected override void BringIndexIntoView(int index) => SetVerticalOffset(index / 2 * TileHeight);
    public void SetVerticalOffset(double value) { if (!double.IsFinite(value)) value = value > 0 ? extent : 0; offset = Math.Clamp(value, 0, Math.Max(0, extent - height)); InvalidateMeasure(); ScrollOwner?.InvalidateScrollInfo(); }
    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        var child = visual as DependencyObject;
        while (child is not null && VisualTreeHelper.GetParent(child) != this) child = VisualTreeHelper.GetParent(child);
        if (child is UIElement element)
        {
            var index = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(InternalChildren.IndexOf(element), 0));
            var y = index / 2 * TileHeight;
            if (y < offset) SetVerticalOffset(y); else if (y + TileHeight > offset + height) SetVerticalOffset(y + TileHeight - height);
        }
        return rectangle;
    }
    public void LineUp() => SetVerticalOffset(offset - 24);
    public void LineDown() => SetVerticalOffset(offset + 24);
    public void MouseWheelUp() => SetVerticalOffset(offset - 48);
    public void MouseWheelDown() => SetVerticalOffset(offset + 48);
    public void PageUp() => SetVerticalOffset(offset - height);
    public void PageDown() => SetVerticalOffset(offset + height);
    public void LineLeft() { } public void LineRight() { } public void MouseWheelLeft() { } public void MouseWheelRight() { }
    public void PageLeft() { } public void PageRight() { } public void SetHorizontalOffset(double value) { }
}
