namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.ViewModels;
using AIQuotaBar.Core.Models;
using Xunit;

public class QuotaWindowViewModelTests
{
    [Fact]
    public void Properties_DisplayPercentageWithoutRemainingWording()
    {
        var window = new QuotaWindow("primary", "5-Hour", 53.0, TimeSpan.FromHours(5), null);
        var vm = new QuotaWindowViewModel(window);

        Assert.Equal("47%", vm.RemainingText);
        Assert.Equal("47%", vm.PercentText);
        Assert.Equal("53% used", vm.UsedText);
        Assert.Equal(47, vm.DisplayRemainingPercent);
        Assert.Equal(53, vm.DisplayUsedPercent);
    }

    [Fact]
    public void TooltipText_IncludesFormattedFractionalPercentage_WhenFractionalPrecisionExists()
    {
        var window = new QuotaWindow("gemini_5h", "Gemini · 5-Hour", 100.0 - 65.349, TimeSpan.FromHours(5), null);
        var vm = new QuotaWindowViewModel(window);

        Assert.Equal("65%", vm.RemainingText);
        Assert.Equal("65.3", vm.FormattedRemainingPercent);
        Assert.Contains("Gemini · 5-Hour: 65.3% quota remaining", vm.TooltipText);
    }

    [Fact]
    public void TooltipText_FormatsWholeNumber_WhenNoFractionalPrecision()
    {
        var window = new QuotaWindow("3p_weekly", "Claude & GPT · Weekly", 0.0, TimeSpan.FromDays(7), null);
        var vm = new QuotaWindowViewModel(window);

        Assert.Equal("100%", vm.RemainingText);
        Assert.Equal("100", vm.FormattedRemainingPercent);
        Assert.Contains("Claude & GPT · Weekly: 100% quota remaining", vm.TooltipText);
    }

    [Fact]
    public void AccessibilityText_IncludesMeaningfulText()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(4).AddMinutes(12);
        var window = new QuotaWindow("primary", "5-Hour", 53.0, TimeSpan.FromHours(5), reset);
        var vm = new QuotaWindowViewModel(window);

        Assert.Contains("5-Hour, 47 percent remaining", vm.AccessibilityText);
        Assert.Contains("resets in", vm.AccessibilityText);
    }

    [Fact]
    public void LayoutMode_Changes_UpdatesDisplayNameAndShowCountdown()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2);
        var window = new QuotaWindow("gemini_5h", "Gemini · 5-Hour", 30.0, TimeSpan.FromHours(5), reset);
        var vm = new QuotaWindowViewModel(window);

        // Default layout mode is Compact
        Assert.Equal(AIQuotaBar.App.Layout.WidgetLayoutMode.Compact, vm.LayoutMode);
        Assert.Equal("Gemini · 5h", vm.DisplayName);
        Assert.True(vm.ShowCountdown);

        // Switch to Full
        vm.LayoutMode = AIQuotaBar.App.Layout.WidgetLayoutMode.Full;
        Assert.Equal("Gemini · 5-Hour", vm.DisplayName);
        Assert.True(vm.ShowCountdown);

        // Switch to Minimal
        vm.LayoutMode = AIQuotaBar.App.Layout.WidgetLayoutMode.Minimal;
        Assert.Equal("Gemini · 5h", vm.DisplayName);
        Assert.False(vm.ShowCountdown);

        // Switch to Micro
        vm.LayoutMode = AIQuotaBar.App.Layout.WidgetLayoutMode.Micro;
        Assert.Equal("G · 5h", vm.DisplayName);
        Assert.False(vm.ShowCountdown);
    }
}
