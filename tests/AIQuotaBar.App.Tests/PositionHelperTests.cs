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
}
