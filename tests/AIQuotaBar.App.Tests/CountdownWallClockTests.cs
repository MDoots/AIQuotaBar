namespace AIQuotaBar.App.Tests;

using AIQuotaBar.Core.Utils;
using Xunit;

public class CountdownWallClockTests
{
    [Fact]
    public void FormatCountdown_CalculatesRemainingFromWallClockTimestamp()
    {
        var baselineTime = new DateTimeOffset(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);
        var resetsAt = baselineTime.AddHours(4).AddMinutes(30);

        // At baseline time: 4h 30m
        var initialCountdown = CountdownFormatter.FormatCountdown(resetsAt, baselineTime);
        Assert.Equal("resets in 4h 30m", initialCountdown);

        // Simulate 4 hours of PC sleep (time jump to baseline + 4 hours)
        var wakeTime = baselineTime.AddHours(4);
        var postResumeCountdown = CountdownFormatter.FormatCountdown(resetsAt, wakeTime);

        // Real wall-clock diff immediately shows 30m without requiring accumulated awake ticks
        Assert.Equal("resets in 30m", postResumeCountdown);

        // Simulate time jump past reset time
        var postResetTime = baselineTime.AddHours(5);
        var expiredCountdown = CountdownFormatter.FormatCountdown(resetsAt, postResetTime);
        Assert.Equal("resets soon", expiredCountdown);
    }
}
