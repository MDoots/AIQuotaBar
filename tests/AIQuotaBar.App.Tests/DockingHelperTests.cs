namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Layout;
using Xunit;

public class DockingHelperTests
{
    [Fact]
    public void CalculateDockPosition_TopMode_CentresHorizontallyAndAnchorsToTop()
    {
        // 1920x1080 monitor with bottom taskbar (rcWork: 0, 0, 1920, 1040)
        // Window width = 960, height = 48
        var (x, y) = DockingHelper.CalculateDockPosition(
            workLeft: 0,
            workTop: 0,
            workRight: 1920,
            workBottom: 1040,
            windowWidthPhysical: 960,
            windowHeightPhysical: 48,
            dockMode: WidgetDockMode.Top);

        // Expected X: (1920 - 960) / 2 = 480
        // Expected Y: 0 (rcWork.Top)
        Assert.Equal(480, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void CalculateDockPosition_BottomMode_CentresHorizontallyAndAnchorsToBottomUsingActualHeight()
    {
        // 1920x1080 monitor with bottom taskbar (rcWork: 0, 0, 1920, 1040)
        // Window width = 960, height = 52
        var (x, y) = DockingHelper.CalculateDockPosition(
            workLeft: 0,
            workTop: 0,
            workRight: 1920,
            workBottom: 1040,
            windowWidthPhysical: 960,
            windowHeightPhysical: 52,
            dockMode: WidgetDockMode.Bottom);

        // Expected X: (1920 - 960) / 2 = 480
        // Expected Y: 1040 - 52 = 988
        Assert.Equal(480, x);
        Assert.Equal(988, y);
    }

    [Fact]
    public void CalculateDockPosition_TopTaskbar_RespectsTopTaskbarOffset()
    {
        // Taskbar at top (rcWork: 0, 40, 1920, 1080)
        var (xTop, yTop) = DockingHelper.CalculateDockPosition(
            workLeft: 0,
            workTop: 40,
            workRight: 1920,
            workBottom: 1080,
            windowWidthPhysical: 960,
            windowHeightPhysical: 50,
            dockMode: WidgetDockMode.Top);

        Assert.Equal(480, xTop);
        Assert.Equal(40, yTop);

        var (xBottom, yBottom) = DockingHelper.CalculateDockPosition(
            workLeft: 0,
            workTop: 40,
            workRight: 1920,
            workBottom: 1080,
            windowWidthPhysical: 960,
            windowHeightPhysical: 50,
            dockMode: WidgetDockMode.Bottom);

        Assert.Equal(480, xBottom);
        Assert.Equal(1030, yBottom);
    }

    [Fact]
    public void CalculateDockPosition_LeftTaskbar_RespectsLeftTaskbarOffset()
    {
        // Taskbar at left (rcWork: 60, 0, 1920, 1080)
        var (x, y) = DockingHelper.CalculateDockPosition(
            workLeft: 60,
            workTop: 0,
            workRight: 1920,
            workBottom: 1080,
            windowWidthPhysical: 960,
            windowHeightPhysical: 50,
            dockMode: WidgetDockMode.Top);

        // Work area width = 1920 - 60 = 1860
        // Centred offset = (1860 - 960) / 2 = 450
        // X = 60 + 450 = 510
        Assert.Equal(510, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void CalculateDockPosition_RightTaskbar_RespectsRightTaskbarOffset()
    {
        // Taskbar at right (rcWork: 0, 0, 1860, 1080)
        var (x, y) = DockingHelper.CalculateDockPosition(
            workLeft: 0,
            workTop: 0,
            workRight: 1860,
            workBottom: 1080,
            windowWidthPhysical: 960,
            windowHeightPhysical: 50,
            dockMode: WidgetDockMode.Top);

        // Work area width = 1860
        // Centred offset = (1860 - 960) / 2 = 450
        // X = 0 + 450 = 450
        Assert.Equal(450, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void CalculateDockPosition_NegativeCoordinatesSecondaryMonitor_PositionsCorrectly()
    {
        // Secondary monitor positioned to the left of primary: (-1920, 0) to (0, 1080)
        // rcWork with taskbar at bottom: (-1920, 0, 0, 1040)
        var (xTop, yTop) = DockingHelper.CalculateDockPosition(
            workLeft: -1920,
            workTop: 0,
            workRight: 0,
            workBottom: 1040,
            windowWidthPhysical: 960,
            windowHeightPhysical: 50,
            dockMode: WidgetDockMode.Top);

        // Work width = 0 - (-1920) = 1920
        // X = -1920 + (1920 - 960) / 2 = -1920 + 480 = -1440
        Assert.Equal(-1440, xTop);
        Assert.Equal(0, yTop);

        var (xBottom, yBottom) = DockingHelper.CalculateDockPosition(
            workLeft: -1920,
            workTop: 0,
            workRight: 0,
            workBottom: 1040,
            windowWidthPhysical: 960,
            windowHeightPhysical: 50,
            dockMode: WidgetDockMode.Bottom);

        Assert.Equal(-1440, xBottom);
        Assert.Equal(990, yBottom);
    }

    [Fact]
    public void CalculateDockPosition_SecondaryMonitorRight_PositionsCorrectly()
    {
        // Secondary monitor to the right of primary: (1920, 0) to (3840, 1080)
        var (xTop, yTop) = DockingHelper.CalculateDockPosition(
            workLeft: 1920,
            workTop: 0,
            workRight: 3840,
            workBottom: 1080,
            windowWidthPhysical: 960,
            windowHeightPhysical: 50,
            dockMode: WidgetDockMode.Top);

        // Work width = 3840 - 1920 = 1920
        // X = 1920 + (1920 - 960) / 2 = 1920 + 480 = 2400
        Assert.Equal(2400, xTop);
        Assert.Equal(0, yTop);
    }

    [Fact]
    public void CalculateDockPosition_NarrowWorkArea_ClampsPositionToWorkArea()
    {
        // Unusually narrow monitor work area: (0, 0, 500, 800)
        // Window width = 600 (wider than work area)
        var (x, y) = DockingHelper.CalculateDockPosition(
            workLeft: 0,
            workTop: 0,
            workRight: 500,
            workBottom: 800,
            windowWidthPhysical: 600,
            windowHeightPhysical: 50,
            dockMode: WidgetDockMode.Top);

        // Window cannot centre; clamped to workLeft = 0
        Assert.Equal(0, x);
        Assert.Equal(0, y);
    }

    [Theory]
    [InlineData(800.0, 1920.0, 640.0, 20.0, 800.0)]     // Content 800px fits easily -> 800
    [InlineData(1150.0, 1920.0, 640.0, 20.0, 1150.0)]   // Content 1150px exceeds old 960 cap but fits -> 1150
    [InlineData(2200.0, 1920.0, 640.0, 20.0, 1900.0)]   // Content 2200px exceeds work area (1920-20=1900) -> capped at 1900
    [InlineData(400.0, 1920.0, 640.0, 20.0, 640.0)]     // Small content (400px) clamped to min dock width (640)
    [InlineData(800.0, 500.0, 640.0, 20.0, 480.0)]      // Narrow work area (500-20=480) -> capped to 480
    [InlineData(100.0, 150.0, 640.0, 20.0, 130.0)]      // Extremely narrow work area (150-20=130) -> capped to 130
    [InlineData(500.0, 15.0, 640.0, 20.0, 15.0)]        // Work area smaller than safe inset -> returns work area width
    public void CalculateDockedOuterWidth_ContentDriven_CalculatesCorrectly(
        double desiredContentWidth,
        double workAreaDipWidth,
        double minDockedWidth,
        double safeInset,
        double expected)
    {
        var width = DockingHelper.CalculateDockedOuterWidth(desiredContentWidth, workAreaDipWidth, minDockedWidth, safeInset);
        Assert.Equal(expected, width);
    }

    [Fact]
    public void CalculateDockedOuterWidth_NoFixed960CapRemains()
    {
        // 1150 DIPs on a 1920 work area must NOT be clamped to 960
        var width = DockingHelper.CalculateDockedOuterWidth(1150.0, 1920.0);
        Assert.Equal(1150.0, width);
        Assert.True(width > 960.0);
    }

    [Fact]
    public void CalculateDockedOuterWidth_ContentShrink_CausesDockWidthShrink()
    {
        // 4 visible quota rows / providers requiring 1200 DIPs
        var expandedWidth = DockingHelper.CalculateDockedOuterWidth(1200.0, 1920.0);

        // User hides rows so content now requires only 750 DIPs
        var shrunkWidth = DockingHelper.CalculateDockedOuterWidth(750.0, 1920.0);

        Assert.Equal(1200.0, expandedWidth);
        Assert.Equal(750.0, shrunkWidth);
        Assert.True(shrunkWidth < expandedWidth);
    }

    [Theory]
    [InlineData(0.0, 0)]      // Left-most: X = 0
    [InlineData(0.5, 480)]    // Centre: X = (1920 - 960) / 2 = 480
    [InlineData(1.0, 960)]    // Right-most: X = 1920 - 960 = 960
    [InlineData(-0.5, 0)]     // Clamped to 0.0 -> X = 0
    [InlineData(1.5, 960)]    // Clamped to 1.0 -> X = 960
    [InlineData(double.NaN, 480)]              // Defaults to 0.5 -> X = 480
    [InlineData(double.PositiveInfinity, 480)] // Defaults to 0.5 -> X = 480
    public void CalculateDockPosition_HorizontalAnchor_CalculatesCorrectX(double anchor, int expectedX)
    {
        var (x, y) = DockingHelper.CalculateDockPosition(
            workLeft: 0,
            workTop: 0,
            workRight: 1920,
            workBottom: 1040,
            windowWidthPhysical: 960,
            windowHeightPhysical: 48,
            dockMode: WidgetDockMode.Top,
            horizontalAnchor: anchor);

        Assert.Equal(expectedX, x);
        Assert.Equal(0, y);
    }

    [Theory]
    [InlineData(0, 960, 0, 1920, 0.0)]        // Left edge: anchor = 0.0
    [InlineData(480, 960, 0, 1920, 0.5)]      // Centred: anchor = 0.5
    [InlineData(960, 960, 0, 1920, 1.0)]      // Right edge: anchor = 1.0
    [InlineData(-100, 960, 0, 1920, 0.0)]     // Left out of bounds: clamped to 0.0
    [InlineData(1200, 960, 0, 1920, 1.0)]     // Right out of bounds: clamped to 1.0
    [InlineData(0, 1920, 0, 1920, 0.5)]       // Window width equal to work width: returns 0.5
    [InlineData(-1440, 960, -1920, 0, 0.5)]   // Secondary monitor negative coords centred: anchor = 0.5
    public void CalculateAnchorFromPhysicalPosition_CalculatesNormalizedAnchor(
        int windowX,
        int windowWidth,
        int workLeft,
        int workRight,
        double expectedAnchor)
    {
        var anchor = DockingHelper.CalculateAnchorFromPhysicalPosition(windowX, windowWidth, workLeft, workRight);
        Assert.Equal(expectedAnchor, anchor, 2);
    }

    [Fact]
    public void GetDockTargetOnRelease_FloatingToTopSnapZone_ReturnsTop()
    {
        var target = DockingHelper.GetDockTargetOnRelease(
            currentMode: WidgetDockMode.Floating,
            windowLeftPx: 400,
            windowTopPx: 20, // Inside 32 DIP snap zone
            windowRightPx: 1200,
            windowBottomPx: 120,
            cursorXPx: 600,
            cursorYPx: 25,
            workLeftPx: 0,
            workTopPx: 0,
            workRightPx: 1920,
            workBottomPx: 1040);

        Assert.Equal(WidgetDockMode.Top, target);
    }

    [Fact]
    public void GetDockTargetOnRelease_FloatingToBottomSnapZone_ReturnsBottom()
    {
        var target = DockingHelper.GetDockTargetOnRelease(
            currentMode: WidgetDockMode.Floating,
            windowLeftPx: 400,
            windowTopPx: 940,
            windowRightPx: 1200,
            windowBottomPx: 1030, // Inside 32 DIP bottom snap zone (1040 - 32 = 1008)
            cursorXPx: 600,
            cursorYPx: 1025,
            workLeftPx: 0,
            workTopPx: 0,
            workRightPx: 1920,
            workBottomPx: 1040);

        Assert.Equal(WidgetDockMode.Bottom, target);
    }

    [Fact]
    public void GetDockTargetOnRelease_FloatingInMiddle_ReturnsFloating()
    {
        var target = DockingHelper.GetDockTargetOnRelease(
            currentMode: WidgetDockMode.Floating,
            windowLeftPx: 400,
            windowTopPx: 400, // Middle of screen
            windowRightPx: 1200,
            windowBottomPx: 500,
            cursorXPx: 600,
            cursorYPx: 420,
            workLeftPx: 0,
            workTopPx: 0,
            workRightPx: 1920,
            workBottomPx: 1040);

        Assert.Equal(WidgetDockMode.Floating, target);
    }

    [Fact]
    public void GetDockTargetOnRelease_TopDockSmallPull_RemainsTop()
    {
        var target = DockingHelper.GetDockTargetOnRelease(
            currentMode: WidgetDockMode.Top,
            windowLeftPx: 400,
            windowTopPx: 20, // Less than 48 DIP undock threshold
            windowRightPx: 1200,
            windowBottomPx: 68,
            cursorXPx: 600,
            cursorYPx: 30,
            workLeftPx: 0,
            workTopPx: 0,
            workRightPx: 1920,
            workBottomPx: 1040);

        Assert.Equal(WidgetDockMode.Top, target);
    }

    [Fact]
    public void GetDockTargetOnRelease_TopDockPulledPastUndockThreshold_UndocksToFloating()
    {
        var target = DockingHelper.GetDockTargetOnRelease(
            currentMode: WidgetDockMode.Top,
            windowLeftPx: 400,
            windowTopPx: 100, // Pulled down past 48 DIP undock threshold
            windowRightPx: 1200,
            windowBottomPx: 148,
            cursorXPx: 600,
            cursorYPx: 110,
            workLeftPx: 0,
            workTopPx: 0,
            workRightPx: 1920,
            workBottomPx: 1040);

        Assert.Equal(WidgetDockMode.Floating, target);
    }

    [Fact]
    public void GetDockTargetOnRelease_TopDockDraggedToBottomEdge_DirectTransitionToBottom()
    {
        var target = DockingHelper.GetDockTargetOnRelease(
            currentMode: WidgetDockMode.Top,
            windowLeftPx: 400,
            windowTopPx: 980,
            windowRightPx: 1200,
            windowBottomPx: 1030, // Bottom edge snap zone
            cursorXPx: 600,
            cursorYPx: 1020,
            workLeftPx: 0,
            workTopPx: 0,
            workRightPx: 1920,
            workBottomPx: 1040);

        Assert.Equal(WidgetDockMode.Bottom, target);
    }

    [Fact]
    public void GetDockTargetOnRelease_BottomDockSmallPull_RemainsBottom()
    {
        var target = DockingHelper.GetDockTargetOnRelease(
            currentMode: WidgetDockMode.Bottom,
            windowLeftPx: 400,
            windowTopPx: 970,
            windowRightPx: 1200,
            windowBottomPx: 1018, // Within 48 DIP undock threshold from 1040
            cursorXPx: 600,
            cursorYPx: 1010,
            workLeftPx: 0,
            workTopPx: 0,
            workRightPx: 1920,
            workBottomPx: 1040);

        Assert.Equal(WidgetDockMode.Bottom, target);
    }

    [Fact]
    public void GetDockTargetOnRelease_BottomDockPulledPastUndockThreshold_UndocksToFloating()
    {
        var target = DockingHelper.GetDockTargetOnRelease(
            currentMode: WidgetDockMode.Bottom,
            windowLeftPx: 400,
            windowTopPx: 800,
            windowRightPx: 1200,
            windowBottomPx: 848, // Pulled up past 48 DIP undock threshold (1040 - 48 = 992)
            cursorXPx: 600,
            cursorYPx: 820,
            workLeftPx: 0,
            workTopPx: 0,
            workRightPx: 1920,
            workBottomPx: 1040);

        Assert.Equal(WidgetDockMode.Floating, target);
    }

    [Fact]
    public void GetDockTargetOnRelease_BottomDockDraggedToTopEdge_DirectTransitionToTop()
    {
        var target = DockingHelper.GetDockTargetOnRelease(
            currentMode: WidgetDockMode.Bottom,
            windowLeftPx: 400,
            windowTopPx: 10, // Top snap zone
            windowRightPx: 1200,
            windowBottomPx: 58,
            cursorXPx: 600,
            cursorYPx: 20,
            workLeftPx: 0,
            workTopPx: 0,
            workRightPx: 1920,
            workBottomPx: 1040);

        Assert.Equal(WidgetDockMode.Top, target);
    }
}
