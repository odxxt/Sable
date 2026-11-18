using SableLatch.Core;

internal static class PlacementChecks
{
    public static void Run(Action<bool, string> check)
    {
        var primary = new ScreenBounds(0, 0, 1920, 1040); var second = new ScreenBounds(1920, 0, 3840, 1040); var left = new ScreenBounds(-1920, -240, 0, 800);
        check(WindowPlacement.BottomRight(primary, 342, 411, 18) == (1560, 611), "Primary monitor bottom-right respects work area and margin");
        check(WindowPlacement.BottomRight(second, 342, 411, 18) == (3480, 611), "Secondary monitor docking keeps its desktop offset");
        check(WindowPlacement.BottomRight(left, 342, 411, 18) == (-360, 371), "Docking supports monitors with negative desktop coordinates");
        check(WindowPlacement.Clamp(second, 2100, 100, 342, 411) == (2100, 100), "A saved secondary-monitor location is preserved");
        check(WindowPlacement.Clamp(primary, 4000, 2000, 342, 411) == (1578, 629), "Lost monitor positions are constrained to a visible work area");
        check(WindowPlacement.BottomRight(new(0, 0, 200, 200), 342, 411, 18) == (0, 0), "Small work areas never produce an invalid clamp range");
    }
}
