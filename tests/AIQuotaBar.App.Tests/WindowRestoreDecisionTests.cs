namespace AIQuotaBar.App.Tests;

using System.Drawing;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.Settings;
using Xunit;

public class WindowRestoreDecisionTests
{
    private static Rectangle GetMockPrimaryScreen() => new Rectangle(0, 0, 1920, 1080);

    private static Rectangle[] GetMockSingleScreen()
    {
        return new[]
        {
            GetMockPrimaryScreen()
        };
    }

    private static Rectangle[] GetMockDualScreenWithLeftMonitor()
    {
        return new[]
        {
            new Rectangle(0, 0, 1920, 1080),        // Primary (Right)
            new Rectangle(-1920, 0, 1920, 1080)     // Secondary (Left, negative X)
        };
    }

    private static Rectangle[] GetMockDualScreenWithTopMonitor()
    {
        return new[]
        {
            new Rectangle(0, 0, 1920, 1080),        // Primary (Bottom)
            new Rectangle(0, -1080, 1920, 1080)     // Secondary (Top, negative Y)
        };
    }

    [Fact]
    public void Restore_NormalVisibleFloatingPosition_IsPreservedIdempotently()
    {
        var (left, top) = PositionHelper.EnsureWindowVisible(
            currentLeft: 450,
            currentTop: 250,
            windowWidth: 350,
            windowHeight: 160,
            getScreenBounds: GetMockSingleScreen,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        Assert.Equal(450, left);
        Assert.Equal(250, top);

        // Repeated invocation is strictly idempotent
        var (left2, top2) = PositionHelper.EnsureWindowVisible(
            currentLeft: left,
            currentTop: top,
            windowWidth: 350,
            windowHeight: 160,
            getScreenBounds: GetMockSingleScreen,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        Assert.Equal(450, left2);
        Assert.Equal(250, top2);
    }

    [Fact]
    public void Restore_OffScreenRight_RecoversToPrimaryScreen()
    {
        // Placed way off the right edge (e.g. X = 3000 on a 1920 single screen)
        var (left, top) = PositionHelper.EnsureWindowVisible(
            currentLeft: 3000,
            currentTop: 200,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockSingleScreen,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        // Safe recovery: 1920 - 330 - 24 = 1566, top = 24
        Assert.Equal(1566, left);
        Assert.Equal(24, top);
    }

    [Fact]
    public void Restore_OffScreenLeft_RecoversToPrimaryScreen()
    {
        // Placed way off the left edge (e.g. X = -2000 on a single screen where X starts at 0)
        var (left, top) = PositionHelper.EnsureWindowVisible(
            currentLeft: -2000,
            currentTop: 200,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockSingleScreen,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        Assert.Equal(1566, left);
        Assert.Equal(24, top);
    }

    [Fact]
    public void Restore_OffScreenAbove_RecoversToPrimaryScreen()
    {
        var (left, top) = PositionHelper.EnsureWindowVisible(
            currentLeft: 400,
            currentTop: -1500,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockSingleScreen,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        Assert.Equal(1566, left);
        Assert.Equal(24, top);
    }

    [Fact]
    public void Restore_OffScreenBelow_RecoversToPrimaryScreen()
    {
        var (left, top) = PositionHelper.EnsureWindowVisible(
            currentLeft: 400,
            currentTop: 4000,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockSingleScreen,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        Assert.Equal(1566, left);
        Assert.Equal(24, top);
    }

    [Fact]
    public void Restore_DisconnectedSecondaryMonitorCoordinates_RecoversToPrimaryScreen()
    {
        // User had window on secondary screen at (-1200, 300), but secondary screen is now disconnected
        var (left, top) = PositionHelper.EnsureWindowVisible(
            currentLeft: -1200,
            currentTop: 300,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockSingleScreen, // Only primary screen connected
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        Assert.Equal(1566, left);
        Assert.Equal(24, top);
    }

    [Fact]
    public void Restore_LegitimatelyNegativeCoordinatesOnConnectedLeftMonitor_ArePreserved()
    {
        // Dual monitor setup with left monitor active
        var (left, top) = PositionHelper.EnsureWindowVisible(
            currentLeft: -1200,
            currentTop: 300,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockDualScreenWithLeftMonitor,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        Assert.Equal(-1200, left);
        Assert.Equal(300, top);
    }

    [Fact]
    public void Restore_LegitimatelyNegativeCoordinatesOnConnectedTopMonitor_ArePreserved()
    {
        // Dual monitor setup with top monitor active
        var (left, top) = PositionHelper.EnsureWindowVisible(
            currentLeft: 500,
            currentTop: -600,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockDualScreenWithTopMonitor,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        Assert.Equal(500, left);
        Assert.Equal(-600, top);
    }

    [Fact]
    public void Restore_SliverVisibleOnly_RecoversToSafePosition()
    {
        // Window placed at (1915, 200) on 1920 width screen -> only 5px width visible (below 60px minimum threshold)
        var (left, top) = PositionHelper.EnsureWindowVisible(
            currentLeft: 1915,
            currentTop: 200,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockSingleScreen,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        Assert.Equal(1566, left);
        Assert.Equal(24, top);
    }

    [Fact]
    public void Restore_MeaningfullyPartialVisible_PreservesPosition()
    {
        // Window placed at (1800, 200) on 1920 width screen -> 120px width visible (above 60px minimum threshold)
        var (left, top) = PositionHelper.EnsureWindowVisible(
            currentLeft: 1800,
            currentTop: 200,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockSingleScreen,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        Assert.Equal(1800, left);
        Assert.Equal(200, top);
    }

    [Fact]
    public void DockedMode_CalculateDockPosition_AlwaysRestoresWithinWorkAreaBounds()
    {
        // Dock Top test
        var (xTop, yTop) = DockingHelper.CalculateDockPosition(
            workLeft: 0,
            workTop: 0,
            workRight: 1920,
            workBottom: 1040,
            windowWidthPhysical: 300,
            windowHeightPhysical: 40,
            dockMode: WidgetDockMode.Top,
            horizontalAnchor: 0.5);

        Assert.Equal((1920 - 300) / 2, xTop);
        Assert.Equal(0, yTop);

        // Dock Bottom test
        var (xBottom, yBottom) = DockingHelper.CalculateDockPosition(
            workLeft: 0,
            workTop: 0,
            workRight: 1920,
            workBottom: 1040,
            windowWidthPhysical: 300,
            windowHeightPhysical: 40,
            dockMode: WidgetDockMode.Bottom,
            horizontalAnchor: 0.5);

        Assert.Equal((1920 - 300) / 2, xBottom);
        Assert.Equal(1040 - 40, yBottom);
    }
}
