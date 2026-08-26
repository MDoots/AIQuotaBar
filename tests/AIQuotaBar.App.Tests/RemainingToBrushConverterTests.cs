namespace AIQuotaBar.App.Tests;

using System.Globalization;
using System.Windows.Media;
using AIQuotaBar.App.Converters;
using Xunit;

public class RemainingToBrushConverterTests
{
    private readonly RemainingToBrushConverter _converter = new();

    [Theory]
    [InlineData(100.0)]
    [InlineData(74.0)]
    [InlineData(47.0)]
    [InlineData(31.0)]
    [InlineData(30.1)]
    [InlineData(31)]
    public void Convert_ReturnsHealthyBrush_WhenRemainingGreaterThan30(object value)
    {
        var result = _converter.Convert(value, typeof(Brush), null, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(16, 185, 129), brush.Color); // #10B981
    }

    [Theory]
    [InlineData(30.0)]
    [InlineData(30)]
    [InlineData(20.0)]
    [InlineData(15.5)]
    [InlineData(10.0)]
    [InlineData(10)]
    public void Convert_ReturnsWarningBrush_WhenRemainingBetween10And30Inclusive(object value)
    {
        var result = _converter.Convert(value, typeof(Brush), null, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(245, 158, 11), brush.Color); // #F59E0B
    }

    [Theory]
    [InlineData(9.99)]
    [InlineData(9.0)]
    [InlineData(9)]
    [InlineData(5.0)]
    [InlineData(1.0)]
    [InlineData(0.0)]
    [InlineData(0)]
    [InlineData(-5.0)]
    public void Convert_ReturnsCriticalBrush_WhenRemainingLessThan10(object value)
    {
        var result = _converter.Convert(value, typeof(Brush), null, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(239, 68, 68), brush.Color); // #EF4444
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a number")]
    public void Convert_ReturnsNeutralBrush_WhenValueIsInvalidOrNull(object? value)
    {
        var result = _converter.Convert(value, typeof(Brush), null, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(107, 114, 128), brush.Color); // #6B7280
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack(null, typeof(double), null, CultureInfo.InvariantCulture));
    }
}
