namespace AIQuotaBar.App.Settings;

using System.Drawing;
using System.Windows;
using System.Windows.Forms;

public static class PositionHelper
{
    public static (double Left, double Top) GetSafePosition(
        double? savedLeft,
        double? savedTop,
        double windowWidth = 340,
        double windowHeight = 160,
        Func<Rectangle[]>? getScreenBounds = null)
    {
        var screens = getScreenBounds?.Invoke() 
            ?? Screen.AllScreens.Select(s => s.WorkingArea).ToArray();

        if (savedLeft.HasValue && savedTop.HasValue && screens.Length > 0)
        {
            var testRect = new Rectangle(
                (int)savedLeft.Value,
                (int)savedTop.Value,
                (int)Math.Max(100, windowWidth),
                (int)Math.Max(50, windowHeight));

            var intersectsAnyScreen = screens.Any(screen => screen.IntersectsWith(testRect));
            if (intersectsAnyScreen)
            {
                return (savedLeft.Value, savedTop.Value);
            }
        }

        // Fallback: Default to Top-Right of Primary screen
        var primaryArea = screens.Length > 0 ? screens[0] : new Rectangle(0, 0, 1920, 1080);
        var defaultLeft = primaryArea.Right - windowWidth - 24;
        var defaultTop = primaryArea.Top + 24;

        return (Math.Max(primaryArea.Left, defaultLeft), Math.Max(primaryArea.Top, defaultTop));
    }
}
