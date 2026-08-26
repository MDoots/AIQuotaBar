namespace AIQuotaBar.App.Settings;

using System.Drawing;
using System.Windows.Forms;

public static class PositionHelper
{
    public static (double Left, double Top) GetSafePosition(
        double? savedLeft,
        double? savedTop,
        double windowWidth = 330,
        double windowHeight = 160,
        Func<Rectangle[]>? getScreenBounds = null,
        Func<Rectangle>? getPrimaryScreenBounds = null)
    {
        var screens = getScreenBounds?.Invoke() 
            ?? Screen.AllScreens.Select(s => s.WorkingArea).ToArray();

        // 1. If saved position exists, verify it intersects ANY active screen working area
        // (Note: Negative coordinates are completely valid for secondary monitors to the left/top of primary)
        if (savedLeft.HasValue && savedTop.HasValue && screens.Length > 0)
        {
            var testRect = new Rectangle(
                (int)Math.Floor(savedLeft.Value),
                (int)Math.Floor(savedTop.Value),
                (int)Math.Max(50, windowWidth),
                (int)Math.Max(30, windowHeight));

            var intersectsAnyScreen = screens.Any(screen => screen.IntersectsWith(testRect));
            if (intersectsAnyScreen)
            {
                return (savedLeft.Value, savedTop.Value);
            }
        }

        // 2. Fallback: Place at the top-right of the Windows Primary screen working area
        var primaryArea = getPrimaryScreenBounds?.Invoke()
            ?? (Screen.PrimaryScreen?.WorkingArea 
                ?? (screens.Length > 0 ? screens[0] : new Rectangle(0, 0, 1920, 1080)));

        var defaultLeft = primaryArea.Right - windowWidth - 24;
        var defaultTop = primaryArea.Top + 24;

        return (Math.Max(primaryArea.Left, defaultLeft), Math.Max(primaryArea.Top, defaultTop));
    }
}
