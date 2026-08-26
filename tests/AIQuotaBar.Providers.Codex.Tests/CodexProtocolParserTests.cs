namespace AIQuotaBar.Providers.Codex.Tests;

using System.Text.Json;
using AIQuotaBar.Providers.Codex.Protocol;
using Xunit;

public class CodexProtocolParserTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Parse_FullPlusFixture_DeserializesCorrectly()
    {
        var json = File.ReadAllText(Path.Combine("Fixtures", "codex_plus_full.json"));
        var result = JsonSerializer.Deserialize<CodexRateLimitsResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.NotNull(result.RateLimits);
        Assert.Equal("codex", result.RateLimits.LimitId);
        Assert.Equal("plus", result.RateLimits.PlanType);

        Assert.NotNull(result.RateLimits.Primary);
        Assert.Equal(28, result.RateLimits.Primary.UsedPercent);
        Assert.Equal(300, result.RateLimits.Primary.WindowDurationMins);
        Assert.Equal(1787755078, result.RateLimits.Primary.ResetsAt);

        Assert.NotNull(result.RateLimits.Secondary);
        Assert.Equal(46, result.RateLimits.Secondary.UsedPercent);
        Assert.Equal(10080, result.RateLimits.Secondary.WindowDurationMins);
        Assert.Equal(1788276926, result.RateLimits.Secondary.ResetsAt);
    }

    [Fact]
    public void Parse_WeeklyOnlyFixture_DeserializesCorrectly()
    {
        var json = File.ReadAllText(Path.Combine("Fixtures", "codex_weekly_only.json"));
        var result = JsonSerializer.Deserialize<CodexRateLimitsResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.NotNull(result.RateLimits);
        Assert.Null(result.RateLimits.Primary);
        Assert.NotNull(result.RateLimits.Secondary);
        Assert.Equal(65, result.RateLimits.Secondary.UsedPercent);
    }

    [Fact]
    public void Parse_UnknownDurationFixture_DeserializesCorrectly()
    {
        var json = File.ReadAllText(Path.Combine("Fixtures", "codex_unknown_duration.json"));
        var result = JsonSerializer.Deserialize<CodexRateLimitsResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.NotNull(result.RateLimits);
        Assert.NotNull(result.RateLimits.Primary);
        Assert.Equal(45, result.RateLimits.Primary.WindowDurationMins);
        Assert.NotNull(result.RateLimits.Secondary);
        Assert.Equal(4320, result.RateLimits.Secondary.WindowDurationMins);
    }

    [Fact]
    public void Parse_AccountAuthenticatedFixture_DeserializesCorrectly()
    {
        var json = File.ReadAllText(Path.Combine("Fixtures", "codex_account_authenticated.json"));
        var result = JsonSerializer.Deserialize<CodexAccountResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.NotNull(result.Account);
        Assert.Equal("chatgpt", result.Account.Type);
        Assert.Equal("plus", result.Account.PlanType);
        Assert.True(result.RequiresOpenaiAuth);
    }

    [Fact]
    public void Parse_AccountUnauthenticatedFixture_DeserializesCorrectly()
    {
        var json = File.ReadAllText(Path.Combine("Fixtures", "codex_account_unauthenticated.json"));
        var result = JsonSerializer.Deserialize<CodexAccountResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Null(result.Account);
        Assert.True(result.RequiresOpenaiAuth);
    }

    [Fact]
    public void Parse_ToleratesUnknownFieldsGracefully()
    {
        var jsonWithExtraFields = @"{
            ""rateLimits"": {
                ""primary"": { ""usedPercent"": 10, ""windowDurationMins"": 300 },
                ""extraFutureObject"": { ""nested"": 123 },
                ""futureString"": ""abc""
            },
            ""futureField"": 42
        }";

        var result = JsonSerializer.Deserialize<CodexRateLimitsResult>(jsonWithExtraFields, JsonOptions);
        Assert.NotNull(result?.RateLimits?.Primary);
        Assert.Equal(10, result.RateLimits.Primary.UsedPercent);
    }
}
