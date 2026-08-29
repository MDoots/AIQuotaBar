namespace AIQuotaBar.App.Tests;

using System.Drawing;
using AIQuotaBar.App.Settings;
using Xunit;

public class PositionHelperTests
{
    private static Rectangle GetMockPrimaryScreen() => new Rectangle(0, 0, 1920, 1080);

    private static Rectangle[] GetMockSingleScreen()
    {
        return new[]
        {
            GetMockPrimaryScreen()
        };
    }

    private static Rectangle[] GetMockMultiScreensWithLeftMonitor()
    {
        return new[]
        {
            new Rectangle(0, 0, 1920, 1080),        // Primary (Right)
            new Rectangle(-1920, 0, 1920, 1080)     // Secondary (Left, negative X)
        };
    }

    [Fact]
    public void GetSafePosition_PreservesValidPosition_OnPrimaryScreen()
    {
        var (left, top) = PositionHelper.GetSafePosition(
            savedLeft: 200,
            savedTop: 300,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockSingleScreen,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        Assert.Equal(200, left);
        Assert.Equal(300, top);
    }

    [Fact]
    public void GetSafePosition_PreservesValidNegativeCoordinates_OnLeftSecondaryScreen()
    {
        // Saved position on a left monitor with negative coordinates
        var (left, top) = PositionHelper.GetSafePosition(
            savedLeft: -500,
            savedTop: 200,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockMultiScreensWithLeftMonitor,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        Assert.Equal(-500, left);
        Assert.Equal(200, top);
    }

    [Fact]
    public void GetSafePosition_FallsBackToPrimaryScreenTopRight_WhenCoordinatesAreCompletelyOffScreen()
    {
        // Position was on disconnected screen (e.g. -5000, 5000)
        var (left, top) = PositionHelper.GetSafePosition(
            savedLeft: -5000,
            savedTop: 5000,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockSingleScreen,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        // Expected primary top-right: 1920 - 330 - 24 = 1566, top = 24
        Assert.Equal(1566, left);
        Assert.Equal(24, top);
    }

    [Fact]
    public void GetSafePosition_FallsBackToPrimaryScreenTopRight_WhenSavedCoordinatesAreNull()
    {
        var (left, top) = PositionHelper.GetSafePosition(
            savedLeft: null,
            savedTop: null,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockSingleScreen,
            getPrimaryScreenBounds: GetMockPrimaryScreen);

        Assert.Equal(1566, left);
        Assert.Equal(24, top);
    }

    [Fact]
    public void GetCenteredPosition_CentersIn1920x1040WorkArea()
    {
        var primaryBounds = new Rectangle(0, 0, 1920, 1040);
        var (left, top) = PositionHelper.GetCenteredPosition(
            windowWidth: 300,
            windowHeight: 160,
            getPrimaryScreenBounds: () => primaryBounds);

        // (1920 - 300) / 2 = 810
        // (1040 - 160) / 2 = 440
        Assert.Equal(810, left);
        Assert.Equal(440, top);
    }

    [Fact]
    public void GetCenteredPosition_CentersIn1536x816WorkArea()
    {
        var primaryBounds = new Rectangle(0, 0, 1536, 816);
        var (left, top) = PositionHelper.GetCenteredPosition(
            windowWidth: 300,
            windowHeight: 160,
            getPrimaryScreenBounds: () => primaryBounds);

        // (1536 - 300) / 2 = 618
        // (816 - 160) / 2 = 328
        Assert.Equal(618, left);
        Assert.Equal(328, top);
    }

    [Fact]
    public void GetCenteredPosition_RespectsNonZeroWorkAreaOrigin()
    {
        var primaryBounds = new Rectangle(100, 50, 1920, 1040);
        var (left, top) = PositionHelper.GetCenteredPosition(
            windowWidth: 300,
            windowHeight: 160,
            getPrimaryScreenBounds: () => primaryBounds);

        // 100 + (1920 - 300) / 2 = 910
        // 50 + (1040 - 160) / 2 = 490
        Assert.Equal(910, left);
        Assert.Equal(490, top);
    }

    [Fact]
    public void GetCenteredPosition_MeasuredFinalDimensions_350x386_CentersCorrectly()
    {
        var primaryBounds = new Rectangle(0, 0, 1536, 816);
        var (left, top) = PositionHelper.GetCenteredPosition(
            windowWidth: 350,
            windowHeight: 386,
            getPrimaryScreenBounds: () => primaryBounds);

        // (1536 - 350) / 2 = 593
        // (816 - 386) / 2 = 215
        Assert.Equal(593, left);
        Assert.Equal(215, top);
    }

    [Fact]
    public void GetCenteredPosition_MeasuredFinalDimensions_320x308_CentersCorrectly()
    {
        var primaryBounds = new Rectangle(0, 0, 1536, 816);
        var (left, top) = PositionHelper.GetCenteredPosition(
            windowWidth: 320,
            windowHeight: 308,
            getPrimaryScreenBounds: () => primaryBounds);

        // (1536 - 320) / 2 = 608
        // (816 - 308) / 2 = 254
        Assert.Equal(608, left);
        Assert.Equal(254, top);
    }

    [Fact]
    public void GetCenteredPosition_MeasuredFinalDimensions_WithNonZeroOrigin()
    {
        var primaryBounds = new Rectangle(100, 50, 1536, 816);
        var (left, top) = PositionHelper.GetCenteredPosition(
            windowWidth: 350,
            windowHeight: 386,
            getPrimaryScreenBounds: () => primaryBounds);

        // 100 + (1536 - 350) / 2 = 693
        // 50 + (816 - 386) / 2 = 265
        Assert.Equal(693, left);
        Assert.Equal(265, top);
    }

    [Fact]
    public void CalculateCenteredPhysicalPosition_125PercentScale_1920x1020WorkArea_CentersWindowExactly()
    {
        // Work area physical: 1920 x 1020 (0, 0, 1920, 1020)
        // Window physical: 350 x 386
        var (targetX, targetY) = PositionHelper.CalculateCenteredPhysicalPosition(
            windowWidthPx: 350,
            windowHeightPx: 386,
            workAreaLeft: 0,
            workAreaTop: 0,
            workAreaRight: 1920,
            workAreaBottom: 1020);

        // Expected target coordinates: (1920 - 350) / 2 = 785, (1020 - 386) / 2 = 317
        Assert.Equal(785, targetX);
        Assert.Equal(317, targetY);

        // Verify window center matches work-area center exactly
        var windowCenterX = targetX + 350 / 2.0;
        var windowCenterY = targetY + 386 / 2.0;
        var workCenterX = (0 + 1920) / 2.0;
        var workCenterY = (0 + 1020) / 2.0;

        Assert.Equal(960.0, windowCenterX);
        Assert.Equal(510.0, windowCenterY);
        Assert.Equal(workCenterX, windowCenterX);
        Assert.Equal(workCenterY, windowCenterY);
    }

    [Fact]
    public void CalculateCenteredPhysicalPosition_100PercentScale_1920x1040WorkArea()
    {
        var (targetX, targetY) = PositionHelper.CalculateCenteredPhysicalPosition(
            windowWidthPx: 300,
            windowHeightPx: 300,
            workAreaLeft: 0,
            workAreaTop: 0,
            workAreaRight: 1920,
            workAreaBottom: 1040);

        // (1920 - 300) / 2 = 810
        // (1040 - 300) / 2 = 370
        Assert.Equal(810, targetX);
        Assert.Equal(370, targetY);
    }

    [Fact]
    public void CalculateCenteredPhysicalPosition_150PercentScale_2560x1390WorkArea()
    {
        var (targetX, targetY) = PositionHelper.CalculateCenteredPhysicalPosition(
            windowWidthPx: 450,
            windowHeightPx: 480,
            workAreaLeft: 0,
            workAreaTop: 0,
            workAreaRight: 2560,
            workAreaBottom: 1390);

        // (2560 - 450) / 2 = 1055
        // (1390 - 480) / 2 = 455
        Assert.Equal(1055, targetX);
        Assert.Equal(455, targetY);
    }

    [Fact]
    public void CalculateCenteredPhysicalPosition_RespectsNonZeroWorkAreaOrigin()
    {
        var (targetX, targetY) = PositionHelper.CalculateCenteredPhysicalPosition(
            windowWidthPx: 350,
            windowHeightPx: 386,
            workAreaLeft: 100,
            workAreaTop: 50,
            workAreaRight: 2020,
            workAreaBottom: 1070);

        // Width = 1920, Height = 1020
        // targetX = 100 + (1920 - 350) / 2 = 885
        // targetY = 50 + (1020 - 386) / 2 = 367
        Assert.Equal(885, targetX);
        Assert.Equal(367, targetY);
    }

    [Fact]
    public void CalculateCenteredPhysicalPosition_HandlesOddDimensionsCleanly()
    {
        var (targetX, targetY) = PositionHelper.CalculateCenteredPhysicalPosition(
            windowWidthPx: 351,
            windowHeightPx: 387,
            workAreaLeft: 0,
            workAreaTop: 0,
            workAreaRight: 1921,
            workAreaBottom: 1021);

        // (1921 - 351) / 2 = 785
        // (1021 - 387) / 2 = 317
        Assert.Equal(785, targetX);
        Assert.Equal(317, targetY);
    }

    [Fact]
    public void IsWindowMeaningfullyVisible_ReturnsFalse_ForInvalidInputs()
    {
        var screens = GetMockSingleScreen();
        Assert.False(PositionHelper.IsWindowMeaningfullyVisible(double.NaN, 0, 300, 160, screens));
        Assert.False(PositionHelper.IsWindowMeaningfullyVisible(0, double.PositiveInfinity, 300, 160, screens));
        Assert.False(PositionHelper.IsWindowMeaningfullyVisible(0, 0, -100, 160, screens));
        Assert.False(PositionHelper.IsWindowMeaningfullyVisible(0, 0, 300, 0, screens));
        Assert.False(PositionHelper.IsWindowMeaningfullyVisible(0, 0, 300, 160, Array.Empty<Rectangle>()));
    }

    [Fact]
    public void IsWindowMeaningfullyVisible_ReturnsTrue_WhenFullyVisible()
    {
        var screens = GetMockSingleScreen();
        Assert.True(PositionHelper.IsWindowMeaningfullyVisible(100, 100, 300, 160, screens));
    }

    [Fact]
    public void IsWindowMeaningfullyVisible_ReturnsFalse_WhenSliverOnly()
    {
        var screens = GetMockSingleScreen();
        // Placed at X=1900 on 1920-width screen: 20px visible < 60px default threshold
        Assert.False(PositionHelper.IsWindowMeaningfullyVisible(1900, 100, 300, 160, screens));
    }
}
