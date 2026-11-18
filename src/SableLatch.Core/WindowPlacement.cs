namespace SableLatch.Core;

public readonly record struct ScreenBounds(int Left, int Top, int Right, int Bottom);
public static class WindowPlacement
{
    public static (int X, int Y) BottomRight(ScreenBounds area, int width, int height, int margin) =>
        (Math.Max(area.Left, area.Right - width - margin), Math.Max(area.Top, area.Bottom - height - margin));
    public static (int X, int Y) Clamp(ScreenBounds area, int x, int y, int width, int height) =>
        (Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - width)), Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - height)));
}
