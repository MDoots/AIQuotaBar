namespace AIQuotaBar.Core.Tests;

using AIQuotaBar.Core.Models;
using Xunit;

public class QuotaWindowTests
{
    [Fact]
    public void QuotaWindow_CalculatesClampedAndRemainingCorrectly()
    {
        var window = new QuotaWindow("primary", "5-Hour", 28, TimeSpan.FromHours(5), null);

        Assert.Equal("primary", window.Id);
        Assert.Equal("5-Hour", window.DisplayName);
        Assert.Equal(28, window.RawUsedPercent);
        Assert.Equal(28, window.ClampedUsedPercent);
        Assert.Equal(72, window.RemainingPercent);
        Assert.Equal(QuotaWindowStatus.Active, window.Status);
    }

    [Fact]
    public void QuotaWindow_ClampsOver100Correctly()
    {
        var window = new QuotaWindow("primary", "5-Hour", 115, TimeSpan.FromHours(5), null, QuotaWindowStatus.Exhausted);

        Assert.Equal(115, window.RawUsedPercent);
        Assert.Equal(100, window.ClampedUsedPercent);
        Assert.Equal(0, window.RemainingPercent);
        Assert.Equal(QuotaWindowStatus.Exhausted, window.Status);
    }

    [Fact]
    public void QuotaWindow_ClampsNegativeCorrectly()
    {
        var window = new QuotaWindow("primary", "5-Hour", -10, TimeSpan.FromHours(5), null);

        Assert.Equal(-10, window.RawUsedPercent);
        Assert.Equal(0, window.ClampedUsedPercent);
        Assert.Equal(100, window.RemainingPercent);
    }

    [Fact]
    public void QuotaWindow_PreservesFractionalPrecision()
    {
        var window = new QuotaWindow("primary", "5-Hour", 27.294451, TimeSpan.FromHours(5), null);

        Assert.Equal(27.294451, window.RawUsedPercent);
        Assert.Equal(27.294451, window.ClampedUsedPercent);
        Assert.True(Math.Abs(72.705549 - window.RemainingPercent) < 0.000001);
    }
}
