namespace AIQuotaBar.Core.Tests;

using AIQuotaBar.Core.Utils;
using Xunit;

public class DurationFormatterTests
{
    [Theory]
    [InlineData(300L, "5-Hour")]
    [InlineData(10080L, "Weekly")]
    [InlineData(20160L, "2-Week")]
    [InlineData(1440L, "Daily")]
    [InlineData(2880L, "2-Day")]
    [InlineData(60L, "1-Hour")]
    [InlineData(120L, "2-Hour")]
    [InlineData(45L, "45-Minute")]
    [InlineData(null, "Quota Window")]
    [InlineData(0L, "Quota Window")]
    [InlineData(-10L, "Quota Window")]
    public void FormatWindowName_FormatsCorrectly(long? durationMinutes, string expected)
    {
        var result = DurationFormatter.FormatWindowName(durationMinutes);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatWindowName_UsesCustomFallbackLabel_WhenDurationNull()
    {
        var result = DurationFormatter.FormatWindowName(null, "Custom Default");
        Assert.Equal("Custom Default", result);
    }

    [Fact]
    public void ToTimeSpan_ReturnsExpectedSpan()
    {
        Assert.Null(DurationFormatter.ToTimeSpan(null));
        Assert.Null(DurationFormatter.ToTimeSpan(0));
        Assert.Null(DurationFormatter.ToTimeSpan(-5));
        Assert.Equal(TimeSpan.FromHours(5), DurationFormatter.ToTimeSpan(300));
        Assert.Equal(TimeSpan.FromDays(7), DurationFormatter.ToTimeSpan(10080));
    }
}
