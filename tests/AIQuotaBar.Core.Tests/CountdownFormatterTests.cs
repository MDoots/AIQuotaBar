namespace AIQuotaBar.Core.Tests;

using AIQuotaBar.Core.Utils;
using Xunit;

public class CountdownFormatterTests
{
    [Fact]
    public void FormatCountdown_ReturnsNull_WhenResetsAtNull()
    {
        var result = CountdownFormatter.FormatCountdown(null);
        Assert.Null(result);
    }

    [Fact]
    public void FormatCountdown_ReturnsResetsSoon_WhenInPast()
    {
        var now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var past = now.AddMinutes(-5);

        var result = CountdownFormatter.FormatCountdown(past, now);
        Assert.Equal("resets soon", result);
    }

    [Fact]
    public void FormatCountdown_ReturnsDaysAndHours_WhenMultipleDays()
    {
        var now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var reset = now.AddDays(5).AddHours(14);

        var result = CountdownFormatter.FormatCountdown(reset, now);
        Assert.Equal("resets in 5d 14h", result);
    }

    [Fact]
    public void FormatCountdown_ReturnsHoursAndMinutes_WhenUnderTwoDays()
    {
        var now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var reset = now.AddHours(2).AddMinutes(14);

        var result = CountdownFormatter.FormatCountdown(reset, now);
        Assert.Equal("resets in 2h 14m", result);
    }

    [Fact]
    public void FormatCountdown_ReturnsMinutes_WhenUnderOneHour()
    {
        var now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var reset = now.AddMinutes(45);

        var result = CountdownFormatter.FormatCountdown(reset, now);
        Assert.Equal("resets in 45m", result);
    }

    [Fact]
    public void FormatCountdown_ReturnsUnderMinute_WhenUnderOneMinute()
    {
        var now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var reset = now.AddSeconds(30);

        var result = CountdownFormatter.FormatCountdown(reset, now);
        Assert.Equal("resets in <1m", result);
    }
}
