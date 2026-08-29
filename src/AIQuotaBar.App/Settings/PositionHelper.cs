namespace AIQuotaBar.App.Settings;

using System.Drawing;
using System.Windows.Forms;

public static class PositionHelper
{
    public const double DefaultMinVisibleWidth = 60.0;
    public const double DefaultMinVisibleHeight = 40.0;

    /// <summary>
    /// Determines whether a meaningful, usable portion of the window intersects at least one active screen working area.
    /// Supports negative coordinates (valid for secondary monitors placed to the left or top of primary).
    /// </summary>
    public static bool IsWindowMeaningfullyVisible(
        double left,
        double top,
        double width,
        double height,
        IEnumerable<Rectangle> workingAreas,
        double minVisibleWidth = DefaultMinVisibleWidth,
        double minVisibleHeight = DefaultMinVisibleHeight)
    {
        if (double.IsNaN(left) || double.IsInfinity(left) ||
            double.IsNaN(top) || double.IsInfinity(top) ||
            double.IsNaN(width) || double.IsInfinity(width) || width <= 0 ||
            double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
        {
            return false;
        }

        var windowRect = new Rectangle(
            (int)Math.Floor(left),
            (int)Math.Floor(top),
            (int)Math.Ceiling(width),
            (int)Math.Ceiling(height));

        foreach (var workArea in workingAreas)
        {
            var intersection = Rectangle.Intersect(workArea, windowRect);
            if (!intersection.IsEmpty &&
                intersection.Width >= minVisibleWidth &&
                intersection.Height >= minVisibleHeight)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Verifies that the window is meaningfully visible. If it is already visible on any current screen,
    /// preserves its current coordinates. If off-screen (or on a disconnected monitor), recovers it to a safe on-screen location.
    /// </summary>
    public static (double Left, double Top) EnsureWindowVisible(
        double currentLeft,
        double currentTop,
        double windowWidth = 330,
        double windowHeight = 160,
        Func<Rectangle[]>? getScreenBounds = null,
        Func<Rectangle>? getPrimaryScreenBounds = null)
    {
        var screens = getScreenBounds?.Invoke()
            ?? Screen.AllScreens.Select(s => s.WorkingArea).ToArray();

        if (screens.Length > 0 && IsWindowMeaningfullyVisible(currentLeft, currentTop, windowWidth, windowHeight, screens))
        {
            return (currentLeft, currentTop);
        }

        return GetSafePosition(null, null, windowWidth, windowHeight, getScreenBounds, getPrimaryScreenBounds);
    }

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
            if (IsWindowMeaningfullyVisible(savedLeft.Value, savedTop.Value, windowWidth, windowHeight, screens))
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

    public static (double Left, double Top) GetCenteredPosition(
        double windowWidth = 300,
        double windowHeight = 160,
        Func<Rectangle>? getPrimaryScreenBounds = null)
    {
        var primaryArea = getPrimaryScreenBounds?.Invoke()
            ?? (Screen.PrimaryScreen?.WorkingArea
                ?? new Rectangle(0, 0, 1920, 1080));

        var centeredLeft = primaryArea.Left + (primaryArea.Width - windowWidth) / 2.0;
        var centeredTop = primaryArea.Top + (primaryArea.Height - windowHeight) / 2.0;

        return (Math.Max(primaryArea.Left, centeredLeft), Math.Max(primaryArea.Top, centeredTop));
    }

    public static (int TargetX, int TargetY) CalculateCenteredPhysicalPosition(
        int windowWidthPx,
        int windowHeightPx,
        int workAreaLeft,
        int workAreaTop,
        int workAreaRight,
        int workAreaBottom)
    {
        int workAreaWidth = workAreaRight - workAreaLeft;
        int workAreaHeight = workAreaBottom - workAreaTop;

        int targetX = workAreaLeft + (workAreaWidth - windowWidthPx) / 2;
        int targetY = workAreaTop + (workAreaHeight - windowHeightPx) / 2;

        return (targetX, targetY);
    }
}
