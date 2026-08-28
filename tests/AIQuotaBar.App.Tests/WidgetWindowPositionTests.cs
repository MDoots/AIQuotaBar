namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Views;
using Xunit;

public class WidgetWindowPositionTests
{
    [Theory]
    [InlineData(100, 200, 150, 250, 180, 270, 130, 220)]
    [InlineData(500, 300, 520, 310, 480, 290, 460, 280)]
    [InlineData(0, 0, 100, 100, 100, 100, 0, 0)]
    [InlineData(-1920, 100, -1500, 200, -1400, 250, -1820, 150)] // Multi-monitor negative screen coordinate
    public void CalculateNewPosition_ComputesCorrectWindowCoordinates(
        int initialLeft, int initialTop,
        int initialCursorX, int initialCursorY,
        int currentCursorX, int currentCursorY,
        int expectedLeft, int expectedTop)
    {
        var (newLeft, newTop) = WidgetWindow.CalculateNewPosition(
            initialLeft, initialTop,
            initialCursorX, initialCursorY,
            currentCursorX, currentCursorY);

        Assert.Equal(expectedLeft, newLeft);
        Assert.Equal(expectedTop, newTop);
    }
}
