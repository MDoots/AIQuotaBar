namespace AIQuotaBar.App.Tests;

using System.Drawing;
using AIQuotaBar.App.Settings;
using Xunit;

public class PositionHelperTests
{
    private static Rectangle[] GetMockScreens()
    {
        return new[]
        {
            new Rectangle(0, 0, 1920, 1080) // Primary 1080p
        };
    }

    [Fact]
    public void GetSafePosition_ReturnsSavedPosition_WhenInsideScreen()
    {
        var (left, top) = PositionHelper.GetSafePosition(
            savedLeft: 200,
            savedTop: 300,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockScreens);

        Assert.Equal(200, left);
        Assert.Equal(300, top);
    }

    [Fact]
    public void GetSafePosition_RecoversToPrimaryTopRight_WhenCoordinatesAreOffScreen()
    {
        // Saved position way beyond the 1920x1080 screen (e.g. 5000, 5000)
        var (left, top) = PositionHelper.GetSafePosition(
            savedLeft: 5000,
            savedTop: 5000,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockScreens);

        // Expected top-right default on primary (1920 - 330 - 24 = 1566, top = 24)
        Assert.Equal(1566, left);
        Assert.Equal(24, top);
    }

    [Fact]
    public void GetSafePosition_RecoversToPrimaryTopRight_WhenCoordinatesAreNegativeAndNotVisible()
    {
        var (left, top) = PositionHelper.GetSafePosition(
            savedLeft: -2000,
            savedTop: -2000,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockScreens);

        Assert.Equal(1566, left);
        Assert.Equal(24, top);
    }

    [Fact]
    public void GetSafePosition_ReturnsDefaultPosition_WhenSavedCoordinatesAreNull()
    {
        var (left, top) = PositionHelper.GetSafePosition(
            savedLeft: null,
            savedTop: null,
            windowWidth: 330,
            windowHeight: 160,
            getScreenBounds: GetMockScreens);

        Assert.Equal(1566, left);
        Assert.Equal(24, top);
    }
}
