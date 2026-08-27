namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Layout;
using Xunit;

public class ResponsiveLayoutHelperTests
{
    [Theory]
    [InlineData(0, WidgetLayoutMode.Micro)]
    [InlineData(100, WidgetLayoutMode.Micro)]
    [InlineData(179.0, WidgetLayoutMode.Micro)]
    [InlineData(179.9, WidgetLayoutMode.Micro)]
    [InlineData(180.0, WidgetLayoutMode.Micro)]
    [InlineData(200.0, WidgetLayoutMode.Micro)]
    [InlineData(219.0, WidgetLayoutMode.Micro)]
    [InlineData(219.9, WidgetLayoutMode.Micro)]
    [InlineData(220.0, WidgetLayoutMode.Minimal)]
    [InlineData(250.0, WidgetLayoutMode.Minimal)]
    [InlineData(269.0, WidgetLayoutMode.Minimal)]
    [InlineData(269.9, WidgetLayoutMode.Minimal)]
    [InlineData(270.0, WidgetLayoutMode.Compact)]
    [InlineData(300.0, WidgetLayoutMode.Compact)]
    [InlineData(329.0, WidgetLayoutMode.Compact)]
    [InlineData(329.9, WidgetLayoutMode.Compact)]
    [InlineData(330.0, WidgetLayoutMode.Full)]
    [InlineData(420.0, WidgetLayoutMode.Full)]
    [InlineData(600.0, WidgetLayoutMode.Full)]
    [InlineData(1200.0, WidgetLayoutMode.Full)]
    public void GetLayoutMode_ReturnsCorrectMode_ForGivenWidth(double width, WidgetLayoutMode expected)
    {
        var mode = ResponsiveLayoutHelper.GetLayoutMode(width);
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void GetLayoutMode_ReturnsFullFallback_ForNaNOrInfinity()
    {
        Assert.Equal(WidgetLayoutMode.Full, ResponsiveLayoutHelper.GetLayoutMode(double.NaN));
        Assert.Equal(WidgetLayoutMode.Full, ResponsiveLayoutHelper.GetLayoutMode(double.PositiveInfinity));
        Assert.Equal(WidgetLayoutMode.Full, ResponsiveLayoutHelper.GetLayoutMode(double.NegativeInfinity));
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
