namespace SableLatch.Desktop;

internal static class FolderIcons
{
    public static readonly IReadOnlyDictionary<string, string> Choices = new Dictionary<string, string>
    {
        ["folder"] = "Folder", ["work"] = "Work", ["home"] = "Home", ["star"] = "Star", ["lock"] = "Private",
        ["servers"] = "Servers", ["network"] = "Network", ["cloud"] = "Cloud", ["code"] = "Code", ["database"] = "Database",
        ["terminal"] = "Terminal", ["documents"] = "Documents", ["downloads"] = "Downloads", ["media"] = "Media", ["mail"] = "Mail",
        ["finance"] = "Finance", ["tools"] = "Tools", ["devices"] = "Devices", ["web"] = "Web", ["archive"] = "Archive"
    };
    public static FrameworkElement Create(string id) => new System.Windows.Shapes.Path
    {
        Width = 17, Height = 17, Stretch = Stretch.Uniform, Stroke = new SolidColorBrush(Color.FromRgb(188, 188, 197)), StrokeThickness = 1.1,
        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
        Data = Geometry.Parse(id switch
        {
            "work" => "M 2,7 L 18,7 18,17 2,17 Z M 7,7 L 7,3 13,3 13,7 M 2,11 L 18,11 M 9,10 L 11,10 11,13 9,13 Z",
            "home" => "M 1,9 L 10,2 19,9 M 4,7 L 4,18 16,18 16,7 M 8,18 L 8,12 12,12 12,18",
            "star" => "M 10,1 L 13,7 19,8 14.5,12.5 16,19 10,16 4,19 5.5,12.5 1,8 7,7 Z",
            "lock" => "M 4,9 L 16,9 16,19 4,19 Z M 6,9 L 6,6 C 6,0 14,0 14,6 L 14,9 M 10,12 L 10,16",
            "servers" => "M 2,2 L 18,2 18,8 2,8 Z M 2,12 L 18,12 18,18 2,18 Z M 5,5 L 5.1,5 M 5,15 L 5.1,15 M 9,5 L 15,5 M 9,15 L 15,15",
            "network" => "M 7,1 L 13,1 13,7 7,7 Z M 1,13 L 7,13 7,19 1,19 Z M 13,13 L 19,13 19,19 13,19 Z M 10,7 L 10,10 M 4,13 L 4,10 16,10 16,13",
            "cloud" => "M 5,16 C 0,16 0,8 5,8 C 5,0 16,0 16,8 C 21,8 21,16 16,16 Z",
            "code" => "M 6,4 L 1,10 6,16 M 14,4 L 19,10 14,16 M 12,2 L 8,18",
            "database" => "M 2,5 C 2,0 18,0 18,5 C 18,10 2,10 2,5 Z M 2,5 L 2,15 C 2,20 18,20 18,15 L 18,5 M 2,10 C 2,15 18,15 18,10",
            "terminal" => "M 1,3 L 19,3 19,17 1,17 Z M 5,7 L 8,10 5,13 M 11,13 L 15,13",
            "documents" => "M 5,1 L 13,1 18,6 18,17 5,17 Z M 13,1 L 13,6 18,6 M 5,4 L 2,4 2,20 15,20 15,17 M 8,10 L 14,10 M 8,13 L 14,13",
            "downloads" => "M 10,1 L 10,13 M 5,8 L 10,13 15,8 M 2,13 L 2,19 18,19 18,13",
            "media" => "M 1,2 L 19,2 19,18 1,18 Z M 1,14 L 7,8 12,14 15,11 19,15 M 13,6 A 1,1 0 1 0 15,6 A 1,1 0 1 0 13,6",
            "mail" => "M 1,3 L 19,3 19,17 1,17 Z M 1,4 L 10,11 19,4",
            "finance" => "M 1,3 L 19,3 19,17 1,17 Z M 1,7 L 19,7 M 4,13 L 9,13",
            "tools" => "M 12,2 A 5,5 0 0 0 7,9 L 1,15 5,19 11,13 A 5,5 0 0 0 18,8 L 14,10 10,6 Z",
            "devices" => "M 1,2 L 15,2 15,13 1,13 Z M 8,13 L 8,18 M 4,18 L 12,18 M 16,9 L 20,9 20,19 16,19 Z",
            "web" => "M 1,10 A 9,9 0 1 0 19,10 A 9,9 0 1 0 1,10 M 1,10 L 19,10 M 10,1 C 4,6 4,14 10,19 C 16,14 16,6 10,1",
            "archive" => "M 2,1 L 18,1 18,5 2,5 Z M 3,5 L 3,19 17,19 17,5 M 7,10 L 13,10",
            _ => "M 1,5 L 7,5 10,8 19,8 19,18 1,18 Z M 1,5 L 1,3 8,3 11,6 19,6 19,8"
        })
    };
    public static FrameworkElement Label(VaultFolder folder)
    {
        var row = new Grid(); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new());
        var icon = Create(folder.Icon); icon.Width = icon.Height = 14; row.Children.Add(icon);
        var name = new TextBlock { Text = folder.Name, MaxWidth = 110, FontSize = 11, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetColumn(name, 1); row.Children.Add(name); return row;
    }
}
