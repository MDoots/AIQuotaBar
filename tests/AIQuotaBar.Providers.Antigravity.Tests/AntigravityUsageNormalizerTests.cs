namespace AIQuotaBar.Providers.Antigravity.Tests;

using System.Text.Json;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.Antigravity.Normalization;
using AIQuotaBar.Providers.Antigravity.Protocol;
using Xunit;

public class AntigravityUsageNormalizerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static AntigravityCliResponse LoadFixture(string filename)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", filename);
        var json = File.ReadAllText(fixturePath);
        return JsonSerializer.Deserialize<AntigravityCliResponse>(json, JsonOptions)!;
    }

    [Fact]
    public void Normalize_WithValidSuccessFixture_PopulatesExpectedWindowsAndProperties()
    {
        var response = LoadFixture("antigravity_usage_success.json");

        var snapshot = AntigravityUsageNormalizer.Normalize(response);

        Assert.Equal("antigravity", snapshot.ProviderId);
        Assert.Equal("Google Antigravity", snapshot.ProviderDisplayName);
        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Null(snapshot.StatusMessage);
        Assert.Null(snapshot.AccountPlan); // Plan must remain null unless officially supplied
        Assert.Equal(4, snapshot.Windows.Count);

        // Window 1: Gemini 5-Hour
        var gemini5h = snapshot.Windows[0];
        Assert.Equal("Gemini · 5-Hour", gemini5h.DisplayName);
        Assert.Equal(TimeSpan.FromHours(5), gemini5h.Duration);
        Assert.True(Math.Abs(72.705549 - gemini5h.RemainingPercent) < 0.001);
        Assert.True(Math.Abs(27.294451 - gemini5h.RawUsedPercent) < 0.001);
        Assert.Equal(QuotaWindowStatus.Active, gemini5h.Status);
        Assert.NotNull(gemini5h.ResetsAt);

        // Window 2: Gemini Weekly
        var geminiWeekly = snapshot.Windows[1];
        Assert.Equal("Gemini · Weekly", geminiWeekly.DisplayName);
        Assert.Equal(TimeSpan.FromDays(7), geminiWeekly.Duration);
        Assert.True(Math.Abs(95.450925 - geminiWeekly.RemainingPercent) < 0.001);
        Assert.Equal(QuotaWindowStatus.Active, geminiWeekly.Status);
        Assert.NotNull(geminiWeekly.ResetsAt);

        // Window 3: Claude & GPT 5-Hour
        var claude5h = snapshot.Windows[2];
        Assert.Equal("Claude & GPT · 5-Hour", claude5h.DisplayName);
        Assert.Equal(100.0, claude5h.RemainingPercent);
        Assert.Equal(0.0, claude5h.RawUsedPercent);
        Assert.Equal(QuotaWindowStatus.Active, claude5h.Status);

        // Window 4: Claude & GPT Weekly
        var claudeWeekly = snapshot.Windows[3];
        Assert.Equal("Claude & GPT · Weekly", claudeWeekly.DisplayName);
        Assert.Equal(100.0, claudeWeekly.RemainingPercent);
        Assert.Equal(0.0, claudeWeekly.RawUsedPercent);
        Assert.Equal(QuotaWindowStatus.Active, claudeWeekly.Status);
    }

    [Fact]
    public void Normalize_WithFractionalAndBoundaryValues_PreservesPrecisionAndClampsSafely()
    {
        var response = LoadFixture("antigravity_usage_fractional.json");

        var snapshot = AntigravityUsageNormalizer.Normalize(response);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal(4, snapshot.Windows.Count);

        // Gemini 5h: remaining_fraction = 0.727055
        var gemini5h = snapshot.Windows[0];
        Assert.Equal("Gemini · 5-Hour", gemini5h.DisplayName);
        Assert.True(Math.Abs(72.7055 - gemini5h.RemainingPercent) < 0.00001, $"Expected ~72.7055, got {gemini5h.RemainingPercent}");
        Assert.True(Math.Abs(27.2945 - gemini5h.RawUsedPercent) < 0.00001, $"Expected ~27.2945, got {gemini5h.RawUsedPercent}");

        // Gemini Weekly: remaining_fraction = -0.05 (clamped to 0.0 remaining -> 100% used)
        var geminiWeekly = snapshot.Windows[1];
        Assert.Equal("Gemini · Weekly", geminiWeekly.DisplayName);
        Assert.Equal(0.0, geminiWeekly.RemainingPercent);
        Assert.Equal(100.0, geminiWeekly.RawUsedPercent);
        Assert.Equal(QuotaWindowStatus.Exhausted, geminiWeekly.Status);

        // Claude 5h: remaining_fraction = 1.05 (clamped to 1.0 remaining -> 0% used)
        var claude5h = snapshot.Windows[2];
        Assert.Equal("Claude & GPT · 5-Hour", claude5h.DisplayName);
        Assert.Equal(100.0, claude5h.RemainingPercent);
        Assert.Equal(0.0, claude5h.RawUsedPercent);

        // Claude Weekly: remaining_fraction = 0.0 (exact 0 -> 100% used)
        var claudeWeekly = snapshot.Windows[3];
        Assert.Equal("Claude & GPT · Weekly", claudeWeekly.DisplayName);
        Assert.Equal(0.0, claudeWeekly.RemainingPercent);
        Assert.Equal(100.0, claudeWeekly.RawUsedPercent);
        Assert.Equal(QuotaWindowStatus.Exhausted, claudeWeekly.Status);
    }

    [Fact]
    public void Normalize_WithExhaustedFixture_SetsExhaustedStatus()
    {
        var response = LoadFixture("antigravity_usage_exhausted.json");

        var snapshot = AntigravityUsageNormalizer.Normalize(response);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Single(snapshot.Windows);
        Assert.Equal(QuotaWindowStatus.Exhausted, snapshot.Windows[0].Status);
        Assert.Equal(0.0, snapshot.Windows[0].RemainingPercent);
        Assert.Equal(100.0, snapshot.Windows[0].RawUsedPercent);
    }

    [Fact]
    public void Normalize_WithUnauthenticatedFixture_ReturnsUnauthenticated()
    {
        var response = LoadFixture("antigravity_usage_unauthenticated.json");

        var snapshot = AntigravityUsageNormalizer.Normalize(response);

        Assert.Equal(ProviderStatus.Unauthenticated, snapshot.Status);
        Assert.Equal("Antigravity CLI requires authentication", snapshot.StatusMessage);
        Assert.Empty(snapshot.Windows);
    }

    [Fact]
    public void Normalize_WithEmptyGroups_ReturnsUnavailable()
    {
        var response = LoadFixture("antigravity_usage_missing_buckets.json");

        var snapshot = AntigravityUsageNormalizer.Normalize(response);

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("No active quota windows returned by Antigravity", snapshot.StatusMessage);
        Assert.Empty(snapshot.Windows);
    }

    [Fact]
    public void Normalize_WithNull_ReturnsUnavailable()
    {
        var snapshot = AntigravityUsageNormalizer.Normalize(null);

        Assert.Equal(ProviderStatus.Unavailable, snapshot.Status);
        Assert.Equal("No response received from Antigravity CLI", snapshot.StatusMessage);
    }

    [Fact]
    public void Normalize_WithWeeklyOnlyGeminiQuota_DoesNotManufactureFiveHourWindow()
    {
        var response = new AntigravityCliResponse
        {
            Status = "SUCCESS",
            Command = new AntigravityCommand
            {
                Data = new AntigravityUsageData
                {
                    Groups = new List<AntigravityGroup>
                    {
                        new()
                        {
                            Name = "Gemini",
                            Buckets = new List<AntigravityBucket>
                            {
                                new()
                                {
                                    Id = "gemini-weekly",
                                    Name = "Weekly",
                                    Window = "weekly",
                                    RemainingFraction = 0.85
                                }
                            }
                        }
                    }
                }
            }
        };

        var snapshot = AntigravityUsageNormalizer.Normalize(response);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Null(snapshot.AccountPlan);
        Assert.Single(snapshot.Windows);
        Assert.Equal("Gemini · Weekly", snapshot.Windows[0].DisplayName);
        Assert.Equal(85.0, snapshot.Windows[0].RemainingPercent);
    }

    [Fact]
    public void Normalize_WithBothWeeklyGroupsOnly_EmitsBothWeeklyAndZeroFiveHourWindows()
    {
        var response = new AntigravityCliResponse
        {
            Status = "SUCCESS",
            Command = new AntigravityCommand
            {
                Data = new AntigravityUsageData
                {
                    Groups = new List<AntigravityGroup>
                    {
                        new()
                        {
                            Name = "Gemini",
                            Buckets = new List<AntigravityBucket>
                            {
                                new()
                                {
                                    Id = "gemini-weekly",
                                    Name = "Weekly",
                                    Window = "weekly",
                                    RemainingFraction = 0.60
                                }
                            }
                        },
                        new()
                        {
                            Name = "Claude & GPT",
                            Buckets = new List<AntigravityBucket>
                            {
                                new()
                                {
                                    Id = "claude_and_gpt-weekly",
                                    Name = "Weekly",
                                    Window = "weekly",
                                    RemainingFraction = 0.90
                                }
                            }
                        }
                    }
                }
            }
        };

        var snapshot = AntigravityUsageNormalizer.Normalize(response);

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal(2, snapshot.Windows.Count);
        Assert.Equal("Gemini · Weekly", snapshot.Windows[0].DisplayName);
        Assert.Equal("Claude & GPT · Weekly", snapshot.Windows[1].DisplayName);
        Assert.DoesNotContain(snapshot.Windows, w => w.DisplayName.Contains("5-Hour"));
    }
}
