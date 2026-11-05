namespace SableLatch.Desktop;

internal static class FolderIcons
{
    public static readonly IReadOnlyDictionary<string, string> Choices = new Dictionary<string, string>
    {
        ["folder"] = "Folder", ["work"] = "Work", ["home"] = "Home", ["star"] = "Star", ["lock"] = "Private"
    };
    public static FrameworkElement Create(string id) => new System.Windows.Shapes.Path
    {
        Width = 17, Height = 17, Stretch = Stretch.Uniform, Stroke = new SolidColorBrush(Color.FromRgb(188, 188, 197)), StrokeThickness = 1.1,
        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
        Data = Geometry.Parse(id switch
        {
            "work" => "M 2,7 L 18,7 18,17 2,17 Z M 7,7 L 7,3 13,3 13,7 M 2,11 L 18,11 M 9,10 L 11,10 11,13 9,13 Z",
            "home" => "M 1,9 L 10,2 19,9 M 4,7 L 4,18 16,18 16,7 M 8,18 L 8,12 12,12 12,18",
            "star" => "M 10,1 L 13,7 19,8 14.5,12.5 16,19 10,16 4,19 5.5,12.5 1,8 7,7 Z",
            "lock" => "M 4,9 L 16,9 16,19 4,19 Z M 6,9 L 6,6 C 6,0 14,0 14,6 L 14,9 M 10,12 L 10,16",
            _ => "M 1,5 L 7,5 10,8 19,8 19,18 1,18 Z M 1,5 L 1,3 8,3 11,6 19,6 19,8"
        })
    };
    public static FrameworkElement Label(VaultFolder folder)
    {
        var row = new Grid(); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new());
        var icon = Create(folder.Icon); icon.Width = icon.Height = 14; row.Children.Add(icon);
        var name = new TextBlock { Text = folder.Name, FontSize = 11, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetColumn(name, 1); row.Children.Add(name); return row;
    }
}
