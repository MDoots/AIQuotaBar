namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Layout;
using Xunit;

public class ResponsiveLayoutHelperTests
{
    [Theory]
    [InlineData(0, WidgetLayoutMode.Micro)]
    [InlineData(100, WidgetLayoutMode.Micro)]
    [InlineData(179.9, WidgetLayoutMode.Micro)]
    [InlineData(180.0, WidgetLayoutMode.Micro)]
    [InlineData(200.0, WidgetLayoutMode.Micro)]
    [InlineData(239.0, WidgetLayoutMode.Micro)]
    [InlineData(239.9, WidgetLayoutMode.Micro)]
    [InlineData(240.0, WidgetLayoutMode.Minimal)]
    [InlineData(270.0, WidgetLayoutMode.Minimal)]
    [InlineData(299.0, WidgetLayoutMode.Minimal)]
    [InlineData(299.9, WidgetLayoutMode.Minimal)]
    [InlineData(300.0, WidgetLayoutMode.Compact)]
    [InlineData(350.0, WidgetLayoutMode.Compact)]
    [InlineData(419.0, WidgetLayoutMode.Compact)]
    [InlineData(419.9, WidgetLayoutMode.Compact)]
    [InlineData(420.0, WidgetLayoutMode.Full)]
    [InlineData(600.0, WidgetLayoutMode.Full)]
    [InlineData(1200.0, WidgetLayoutMode.Full)]
    public void GetLayoutMode_ReturnsCorrectMode_ForGivenWidth(double width, WidgetLayoutMode expected)
    {
        var mode = ResponsiveLayoutHelper.GetLayoutMode(width);
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void GetLayoutMode_ReturnsCompactFallback_ForNaNOrInfinity()
    {
        Assert.Equal(WidgetLayoutMode.Compact, ResponsiveLayoutHelper.GetLayoutMode(double.NaN));
        Assert.Equal(WidgetLayoutMode.Compact, ResponsiveLayoutHelper.GetLayoutMode(double.PositiveInfinity));
        Assert.Equal(WidgetLayoutMode.Compact, ResponsiveLayoutHelper.GetLayoutMode(double.NegativeInfinity));
    }

    [Theory]
    [InlineData(null, ResponsiveLayoutHelper.DefaultWidgetWidth)]
    [InlineData(double.NaN, ResponsiveLayoutHelper.DefaultWidgetWidth)]
    [InlineData(double.PositiveInfinity, ResponsiveLayoutHelper.DefaultWidgetWidth)]
    [InlineData(50.0, ResponsiveLayoutHelper.MinWidgetWidth)]
    [InlineData(180.0, 180.0)]
    [InlineData(330.0, 330.0)]
    [InlineData(1200.0, 1200.0)]
    [InlineData(5000.0, ResponsiveLayoutHelper.MaxWidgetWidth)]
    public void ClampWidth_EnforcesBoundsCorrectly(double? input, double expected)
    {
        var clamped = ResponsiveLayoutHelper.ClampWidth(input);
        Assert.Equal(expected, clamped);
    }
}
