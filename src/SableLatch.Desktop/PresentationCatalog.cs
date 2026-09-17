using System.Text.Json;

namespace SableLatch.Desktop;

public sealed class ThemePreset
{
    public string Name { get; set; } = "Graphite";
    public string Surface { get; set; } = "#F2111113";
    public string Control { get; set; } = "#FF232326";
    public string Hover { get; set; } = "#FF303035";
    public string Accent { get; set; } = "#FFF2F2F4";
}
public static class ThemeCatalog
{
    public static IReadOnlyList<ThemePreset> All()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Themes"); var themes = new List<ThemePreset>();
        if (Directory.Exists(directory)) foreach (var path in Directory.EnumerateFiles(directory, "*.json").Take(30))
        {
            try
            {
                if (new FileInfo(path).Length > 8192) continue;
                var t = Codec.Decode<ThemePreset>(File.ReadAllBytes(path));
                if (t.Name.Length is < 1 or > 40 || new[] { t.Surface, t.Control, t.Hover, t.Accent }.Any(c => c.Length != 9 || c[0] != '#' || !c[1..].All(Uri.IsHexDigit))) continue;
                if (themes.All(existing => existing.Name != t.Name)) themes.Add(t);
            }
            catch { /* Invalid data-only themes are ignored, never executed. */ }
        }
        if (themes.All(t => t.Name != "Graphite")) themes.Add(new());
        return themes.OrderBy(t => t.Name == "Graphite" ? 0 : 1).ThenBy(t => t.Name).ToList();
    }
}
public sealed class LanguagePack
{
    public string Id { get; set; } = "en";
    public string Name { get; set; } = "English";
    public string Direction { get; set; } = "ltr";
    public Dictionary<string, string> Strings { get; set; } = [];
}
public static class Localizer
{
    public static LanguagePack Current { get; private set; } = new();
    public static void LoadEnglish()
    {
        try { Current = Codec.Decode<LanguagePack>(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Languages", "en.json"))); } catch { Current = new(); }
    }
    public static string Translate(string english) => Current.Strings.GetValueOrDefault(english) ?? english;
    public static FlowDirection Flow => Current.Direction == "rtl" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}
