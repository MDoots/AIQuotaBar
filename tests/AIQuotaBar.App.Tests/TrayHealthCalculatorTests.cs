namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Health;
using AIQuotaBar.App.Settings;
using AIQuotaBar.App.Tray;
using AIQuotaBar.App.ViewModels;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using Xunit;

public class TrayHealthCalculatorTests
{
    private sealed class MockUsageProvider : IUsageProvider
    {
        private readonly Func<CancellationToken, Task<ProviderSnapshot>> _handler;

        public string Id { get; }
        public string DisplayName { get; }

        public MockUsageProvider(
            string id,
            string displayName,
            Func<CancellationToken, Task<ProviderSnapshot>> handler)
        {
            Id = id;
            DisplayName = displayName;
            _handler = handler;
        }

        public Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
        {
            return _handler(cancellationToken);
        }
    }

    [Fact]
    public void Calculate_ReturnsHealthy_WhenLowestRemainingAbove30()
    {
        var snapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("5h", "5-Hour", 20, TimeSpan.FromHours(5), null), // 80% remaining
            new QuotaWindow("weekly", "Weekly", 16, TimeSpan.FromDays(7), null) // 84% remaining
        });

        var provider = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(snapshot));
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromMinutes(1));
        section.ApplySnapshot(snapshot);

        var state = TrayHealthCalculator.Calculate(new[] { section });

        Assert.Equal(QuotaHealthLevel.Healthy, state.HealthLevel);
        Assert.Equal(80.0, state.LowestRemainingPercent);
        Assert.True(state.HasVisibleQuotaData);
        Assert.True(state.HasVisibleProviders);
        Assert.Equal("AIQuotaBar — 80% · Codex 5-Hour", state.TooltipText);
        Assert.Equal("Lowest quota: 80% — Codex 5-Hour", state.StatusMenuText);
    }

    [Theory]
    [InlineData(30.0)]
    [InlineData(20.0)]
    [InlineData(10.0)]
    public void Calculate_ReturnsWarning_WhenLowestBetween10And30Inclusive(double remainingPercent)
    {
        var usedPercent = 100.0 - remainingPercent;
        var snapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("weekly", "Weekly", usedPercent, TimeSpan.FromDays(7), null)
        });

        var provider = new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(snapshot));
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromMinutes(1));
        section.ApplySnapshot(snapshot);

        var state = TrayHealthCalculator.Calculate(new[] { section });

        Assert.Equal(QuotaHealthLevel.Warning, state.HealthLevel);
        Assert.Equal(remainingPercent, state.LowestRemainingPercent);
        Assert.Equal($"AIQuotaBar — {(int)remainingPercent}% · Codex Weekly", state.TooltipText);
        Assert.Equal($"Lowest quota: {(int)remainingPercent}% — Codex Weekly", state.StatusMenuText);
    }

    [Theory]
    [InlineData(9.9)]
    [InlineData(7.0)]
    [InlineData(1.0)]
    [InlineData(0.0)]
    public void Calculate_ReturnsCritical_WhenLowestBelow10(double remainingPercent)
    {
        var usedPercent = 100.0 - remainingPercent;
        var snapshot = new ProviderSnapshot("antigravity", "Google Antigravity", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("gemini_weekly", "Gemini · Weekly", usedPercent, TimeSpan.FromDays(7), null)
        });

        var provider = new MockUsageProvider("antigravity", "Google Antigravity", _ => Task.FromResult(snapshot));
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromMinutes(1));
        section.ApplySnapshot(snapshot);

        var state = TrayHealthCalculator.Calculate(new[] { section });

        Assert.Equal(QuotaHealthLevel.Critical, state.HealthLevel);
        Assert.NotNull(state.LowestRemainingPercent);
        Assert.Equal(remainingPercent, state.LowestRemainingPercent.Value, 3);
        var rounded = (int)Math.Round(remainingPercent, MidpointRounding.AwayFromZero);
        Assert.Equal($"AIQuotaBar — {rounded}% · Gemini Weekly", state.TooltipText);
        Assert.Equal($"Lowest quota: {rounded}% — Gemini Weekly", state.StatusMenuText);
    }

    [Fact]
    public void Calculate_LowestVisibleRowWins_AcrossMultipleProviders()
    {
        var codexSnapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("primary", "5-Hour", 15, null, null) // 85% remaining
        });
        var agySnapshot = new ProviderSnapshot("antigravity", "Google Antigravity", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("gemini_weekly", "Gemini · Weekly", 93, null, null), // 7% remaining
            new QuotaWindow("claude_weekly", "Claude & GPT · Weekly", 50, null, null) // 50% remaining
        });

        var codexSection = new ProviderSectionViewModel(new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(codexSnapshot)), TimeSpan.FromMinutes(1));
        var agySection = new ProviderSectionViewModel(new MockUsageProvider("antigravity", "Google Antigravity", _ => Task.FromResult(agySnapshot)), TimeSpan.FromMinutes(1));
        codexSection.ApplySnapshot(codexSnapshot);
        agySection.ApplySnapshot(agySnapshot);

        var state = TrayHealthCalculator.Calculate(new[] { codexSection, agySection });

        Assert.Equal(QuotaHealthLevel.Critical, state.HealthLevel);
        Assert.Equal(7.0, state.LowestRemainingPercent);
        Assert.Equal("Google Antigravity", state.ProviderName);
        Assert.Equal("Gemini · Weekly", state.WindowName);
        Assert.Equal("AIQuotaBar — 7% · Gemini Weekly", state.TooltipText);
        Assert.Equal("Lowest quota: 7% — Gemini Weekly", state.StatusMenuText);
    }

    [Fact]
    public void Calculate_HiddenProvider_IsIgnoredByTrayHealth()
    {
        var codexSnapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("primary", "5-Hour", 20, null, null) // 80% remaining
        });
        var agySnapshot = new ProviderSnapshot("antigravity", "Google Antigravity", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("gemini_weekly", "Gemini · Weekly", 95, null, null) // 5% remaining (Critical)
        });

        var codexSection = new ProviderSectionViewModel(new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(codexSnapshot)), TimeSpan.FromMinutes(1));
        var agySection = new ProviderSectionViewModel(new MockUsageProvider("antigravity", "Google Antigravity", _ => Task.FromResult(agySnapshot)), TimeSpan.FromMinutes(1));
        codexSection.ApplySnapshot(codexSnapshot);
        agySection.ApplySnapshot(agySnapshot);

        var settings = new AppSettings();
        settings.SetProviderVisible("antigravity", false); // Hide critical provider

        codexSection.ApplyVisibilityFilter(settings);
        agySection.ApplyVisibilityFilter(settings);

        var state = TrayHealthCalculator.Calculate(new[] { codexSection, agySection });

        // Tray health reflects only the visible Codex provider (80% -> Healthy)
        Assert.Equal(QuotaHealthLevel.Healthy, state.HealthLevel);
        Assert.Equal(80.0, state.LowestRemainingPercent);
        Assert.Equal("AIQuotaBar — 80% · Codex 5-Hour", state.TooltipText);
    }

    [Fact]
    public void Calculate_HiddenRow_IsIgnoredByTrayHealth()
    {
        var codexSnapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("5h", "5-Hour", 10, null, null), // 90% remaining (Healthy)
            new QuotaWindow("weekly", "Weekly", 93, null, null) // 7% remaining (Critical)
        });

        var codexSection = new ProviderSectionViewModel(new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(codexSnapshot)), TimeSpan.FromMinutes(1));
        codexSection.ApplySnapshot(codexSnapshot);

        var settings = new AppSettings();
        settings.SetQuotaWindowVisible("codex", "weekly", false); // Hide 7% row

        codexSection.ApplyVisibilityFilter(settings);

        var state = TrayHealthCalculator.Calculate(new[] { codexSection });

        // Health should be Healthy (90%) from 5-Hour row
        Assert.Equal(QuotaHealthLevel.Healthy, state.HealthLevel);
        Assert.Equal(90.0, state.LowestRemainingPercent);
        Assert.Equal("AIQuotaBar — 90% · Codex 5-Hour", state.TooltipText);
        Assert.Equal("Lowest quota: 90% — Codex 5-Hour", state.StatusMenuText);
    }

    [Fact]
    public void Calculate_NoVisibleRows_ReturnsNeutralState()
    {
        var codexSnapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("5h", "5-Hour", 30, null, null)
        });

        var codexSection = new ProviderSectionViewModel(new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(codexSnapshot)), TimeSpan.FromMinutes(1));
        codexSection.ApplySnapshot(codexSnapshot);

        var settings = new AppSettings();
        settings.SetProviderVisible("codex", false);
        codexSection.ApplyVisibilityFilter(settings);

        var state = TrayHealthCalculator.Calculate(new[] { codexSection });

        Assert.Equal(QuotaHealthLevel.Neutral, state.HealthLevel);
        Assert.Null(state.LowestRemainingPercent);
        Assert.False(state.HasVisibleQuotaData);
        Assert.False(state.HasVisibleProviders);
        Assert.Equal("AIQuotaBar — No quota rows selected", state.TooltipText);
        Assert.Equal("No quota rows selected", state.StatusMenuText);
    }

    [Fact]
    public void Calculate_WaitingForData_WhenVisibleProvidersHaveNoLoadedRows()
    {
        var codexSection = new ProviderSectionViewModel(new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available))), TimeSpan.FromMinutes(1));
        // No snapshot applied yet

        var state = TrayHealthCalculator.Calculate(new[] { codexSection });

        Assert.Equal(QuotaHealthLevel.Neutral, state.HealthLevel);
        Assert.Null(state.LowestRemainingPercent);
        Assert.False(state.HasVisibleQuotaData);
        Assert.True(state.HasVisibleProviders);
        Assert.Equal("AIQuotaBar — Waiting for quota data", state.TooltipText);
        Assert.Equal("Waiting for quota data", state.StatusMenuText);
    }

    [Fact]
    public void Calculate_InvalidPercentages_AreIgnoredSafely()
    {
        var snapshot = new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Available, windows: new[]
        {
            new QuotaWindow("nan_row", "NaN Row", double.NaN, null, null),
            new QuotaWindow("valid_row", "Valid Row", 25, null, null) // 75% remaining
        });

        var codexSection = new ProviderSectionViewModel(new MockUsageProvider("codex", "OpenAI Codex", _ => Task.FromResult(snapshot)), TimeSpan.FromMinutes(1));
        codexSection.ApplySnapshot(snapshot);

        var state = TrayHealthCalculator.Calculate(new[] { codexSection });

        Assert.Equal(QuotaHealthLevel.Healthy, state.HealthLevel);
        Assert.Equal(75.0, state.LowestRemainingPercent);
    }

    [Fact]
    public void SafeTruncate_TruncatesLongString_ToMax63Chars()
    {
        var veryLong = "AIQuotaBar — 5% · Some Extremely Long Provider Name That Exceeds Normal Length Limits For Safe Tooltip Display On Windows";
        var truncated = TrayHealthCalculator.SafeTruncate(veryLong, 63);

        Assert.True(truncated.Length <= 63);
        Assert.EndsWith("...", truncated);
    }

    [Fact]
    public void SafeTruncate_HandlesShortAndNullStrings()
    {
        Assert.Equal("AIQuotaBar", TrayHealthCalculator.SafeTruncate(null));
        Assert.Equal("AIQuotaBar", TrayHealthCalculator.SafeTruncate(""));
        Assert.Equal("Short Text", TrayHealthCalculator.SafeTruncate("Short Text"));
    }
}
