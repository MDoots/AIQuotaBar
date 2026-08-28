namespace AIQuotaBar.App.Tests;

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AIQuotaBar.App.Controls;
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

    [Fact]
    public void IsInteractiveElement_ProgressBar_ReturnsFalse()
    {
        RunOnSta(() =>
        {
            var progressBar = new ProgressBar();
            Assert.False(WidgetWindow.IsInteractiveElement(progressBar));
        });
    }

    [Fact]
    public void IsInteractiveElement_InteractiveControls_ReturnsTrue()
    {
        RunOnSta(() =>
        {
            Assert.True(WidgetWindow.IsInteractiveElement(new Button()));
            Assert.True(WidgetWindow.IsInteractiveElement(new TextBox()));
            Assert.True(WidgetWindow.IsInteractiveElement(new ScrollBar()));
            Assert.True(WidgetWindow.IsInteractiveElement(new Thumb()));
            Assert.True(WidgetWindow.IsInteractiveElement(new Slider()));
        });
    }

    [Fact]
    public void IsInteractiveElement_DisplayElements_ReturnsFalse()
    {
        RunOnSta(() =>
        {
            Assert.False(WidgetWindow.IsInteractiveElement(null));
            Assert.False(WidgetWindow.IsInteractiveElement(new TextBlock()));
            Assert.False(WidgetWindow.IsInteractiveElement(new Border()));
            Assert.False(WidgetWindow.IsInteractiveElement(new Grid()));
            Assert.False(WidgetWindow.IsInteractiveElement(new AdaptiveLabelPresenter()));
        });
    }

    [Fact]
    public void AdaptiveLabelPresenter_HasClipToBoundsSetToTrue()
    {
        RunOnSta(() =>
        {
            var presenter = new AdaptiveLabelPresenter();
            Assert.True(presenter.ClipToBounds);
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
