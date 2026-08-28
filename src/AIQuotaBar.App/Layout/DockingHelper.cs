namespace AIQuotaBar.App.Layout;

using System;

public static class DockingHelper
{
    public const double DefaultMinDockedWidthDip = 640.0;
    public const double SafeWorkAreaInsetDip = 20.0;

    /// <summary>
    /// Calculates the outer DIP width for a docked window based on the desired content width
    /// and the available work area DIP width.
    /// Clamps desired width between minDockedWidth (640 DIP default) and workAreaDipWidth - safeInset (safe work area cap).
    /// </summary>
    public static double CalculateDockedOuterWidth(
        double desiredContentWidthDip,
        double workAreaDipWidth,
        double minDockedWidthDip = DefaultMinDockedWidthDip,
        double safeInsetDip = SafeWorkAreaInsetDip)
    {
        if (double.IsNaN(workAreaDipWidth) || double.IsInfinity(workAreaDipWidth) || workAreaDipWidth <= 0)
        {
            return Math.Max(minDockedWidthDip, desiredContentWidthDip);
        }

        var maxAvailableWidth = Math.Max(0, workAreaDipWidth - safeInsetDip);

        if (maxAvailableWidth <= 0)
        {
            return workAreaDipWidth;
        }

        var targetWidth = Math.Max(minDockedWidthDip, desiredContentWidthDip);

        return Math.Min(targetWidth, maxAvailableWidth);
    }

    /// <summary>
    /// Calculates the physical (pixel) (X, Y) top-left origin for positioning a docked window
    /// anchored to the Top or Bottom of a monitor's physical work area RECT at the given horizontal anchor (0.0=left, 0.5=center, 1.0=right).
    /// </summary>
    public static (int X, int Y) CalculateDockPosition(
        int workLeft,
        int workTop,
        int workRight,
        int workBottom,
        int windowWidthPhysical,
        int windowHeightPhysical,
        WidgetDockMode dockMode,
        double horizontalAnchor = 0.5)
    {
        var workWidth = workRight - workLeft;
        var availableTravel = workWidth - windowWidthPhysical;

        int x;
        if (availableTravel <= 0)
        {
            x = workLeft;
        }
        else
        {
            var anchor = Math.Clamp(
                double.IsNaN(horizontalAnchor) || double.IsInfinity(horizontalAnchor) ? 0.5 : horizontalAnchor,
                0.0,
                1.0);
            x = workLeft + (int)Math.Round(anchor * availableTravel);
        }

        // Clamp X to ensure it stays fully within work area bounds if possible
        if (x < workLeft)
        {
            x = workLeft;
        }
        else if (x + windowWidthPhysical > workRight && workWidth >= windowWidthPhysical)
        {
            x = workRight - windowWidthPhysical;
        }

        // Vertically anchor to Top or Bottom
        int y;
        if (dockMode == WidgetDockMode.Top)
        {
            y = workTop;
        }
        else // Bottom or default fallback
        {
            y = workBottom - windowHeightPhysical;
            if (y < workTop)
            {
                y = workTop;
            }
        }

        return (x, y);
    }

    /// <summary>
    /// Calculates the normalized horizontal anchor (0.0=left, 0.5=center, 1.0=right) from a physical window X position.
    /// </summary>
    public static double CalculateAnchorFromPhysicalPosition(
        int windowXPhysical,
        int windowWidthPhysical,
        int workLeft,
        int workRight)
    {
        var workWidth = workRight - workLeft;
        var availableTravel = workWidth - windowWidthPhysical;

        if (availableTravel <= 0)
        {
            return 0.5;
        }

        var rawOffset = windowXPhysical - workLeft;
        var anchor = (double)rawOffset / availableTravel;

        if (double.IsNaN(anchor) || double.IsInfinity(anchor))
        {
            return 0.5;
        }

        return Math.Clamp(anchor, 0.0, 1.0);
    }

    /// <summary>
    /// Evaluates magnetic dock target on drag release given current mode, geometry, cursor, and monitor work area.
    /// </summary>
    public static WidgetDockMode GetDockTargetOnRelease(
        WidgetDockMode currentMode,
        int windowLeftPx,
        int windowTopPx,
        int windowRightPx,
        int windowBottomPx,
        int cursorXPx,
        int cursorYPx,
        int workLeftPx,
        int workTopPx,
        int workRightPx,
        int workBottomPx,
        double dpiScaleX = 1.0,
        double dpiScaleY = 1.0,
        double snapThresholdDip = 32.0,
        double undockThresholdDip = 48.0)
    {
        var snapPx = (int)Math.Round(snapThresholdDip * dpiScaleY);
        var undockPx = (int)Math.Round(undockThresholdDip * dpiScaleY);

        var inTopSnapZone = (windowTopPx <= workTopPx + snapPx) || (cursorYPx <= workTopPx + snapPx);
        var inBottomSnapZone = (windowBottomPx >= workBottomPx - snapPx) || (cursorYPx >= workBottomPx - snapPx);

        if (currentMode == WidgetDockMode.Floating)
        {
            if (inTopSnapZone)
            {
                return WidgetDockMode.Top;
            }
            if (inBottomSnapZone)
            {
                return WidgetDockMode.Bottom;
            }
            return WidgetDockMode.Floating;
        }

        if (currentMode == WidgetDockMode.Top)
        {
            // Direct transition to Bottom if dragged all the way to bottom edge
            if (inBottomSnapZone)
            {
                return WidgetDockMode.Bottom;
            }

            // Undock to Floating if pulled down past undock threshold
            if (windowTopPx > workTopPx + undockPx && cursorYPx > workTopPx + undockPx)
            {
                return WidgetDockMode.Floating;
            }

            return WidgetDockMode.Top;
        }

        if (currentMode == WidgetDockMode.Bottom)
        {
            // Direct transition to Top if dragged all the way to top edge
            if (inTopSnapZone)
            {
                return WidgetDockMode.Top;
            }

            // Undock to Floating if pulled up past undock threshold
            if (windowBottomPx < workBottomPx - undockPx && cursorYPx < workBottomPx - undockPx)
            {
                return WidgetDockMode.Floating;
            }

            return WidgetDockMode.Bottom;
        }

        return WidgetDockMode.Floating;
    }

    /// <summary>
    /// Resolves the initial temporary host coordinates (WPF DIPs) used solely to establish the HWND
    /// before native monitor resolution. Uses raw saved coordinates directly if valid and finite,
    /// or safe primary fallback coordinates otherwise.
    /// </summary>
    public static (double Left, double Top) ResolveInitialDockedHostPosition(
        double? savedLeft,
        double? savedTop,
        double defaultPrimaryLeft = 100.0,
        double defaultPrimaryTop = 100.0)
    {
        if (savedLeft.HasValue && !double.IsNaN(savedLeft.Value) && !double.IsInfinity(savedLeft.Value) &&
            savedTop.HasValue && !double.IsNaN(savedTop.Value) && !double.IsInfinity(savedTop.Value))
        {
            return (savedLeft.Value, savedTop.Value);
        }

        return (defaultPrimaryLeft, defaultPrimaryTop);
    }
}
