namespace AIQuotaBar.Providers.Codex.Tests;

using System.Text.Json;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.Codex.Transport;
using Xunit;

public class CodexPollingSessionTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    // Unlike the old fault-only runner, this exercises the provider's actual session
    // action, handshake, configuration verification, account read and quota RPC.
    private sealed class SessionRunner : ICodexProcessRunner
    {
        public int Calls { get; private set; }
        public List<string> Methods { get; } = [];
        public List<string> Arguments { get; } = [];
        public Func<int, string, string> Result { get; set; } = (_, method) => method switch
        {
            "initialize" => "{}",
            "config/read" => Fixture("codex_config_plugins_disabled.json"),
            "account/read" => Fixture("codex_account_authenticated.json"),
            "account/rateLimits/read" => Fixture("codex_mixed_buckets.json"),
            _ => throw new InvalidOperationException()
        };

        public async Task RunAsync(string executablePath, string arguments,
            Func<ICodexProcessSession, CancellationToken, Task> sessionAction,
            TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Calls++;
            Arguments.Add(arguments);
            await sessionAction(new Session(this, Calls), cancellationToken);
        }

        private sealed class Session(SessionRunner runner, int call) : ICodexProcessSession
        {
            private readonly Queue<string> _responses = new();
            public Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var request = JsonDocument.Parse(line);
                var method = request.RootElement.GetProperty("method").GetString()!;
                runner.Methods.Add(method);
                if (request.RootElement.TryGetProperty("id", out var id))
                {
                    if (method == "config/read") Assert.False(request.RootElement.GetProperty("params").GetProperty("includeLayers").GetBoolean());
                    var result = runner.Result(call, method);
                    _responses.Enqueue("{\"id\":" + id.GetRawText() + ",\"result\":" + result + "}");
                }
                return Task.CompletedTask;
            }
            public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<string?>(_responses.Dequeue());
            }
        }
    }

    [Fact]
    public async Task SuccessfulSession_PreservesEveryFiniteBucketAndAccountScope()
    {
        var runner = new SessionRunner();
        var provider = new CodexUsageProvider(runner, () => "fixture.exe");
        var snapshot = await provider.GetUsageAsync();

        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal(new[] { "alpha_primary", "zeta_primary" }, snapshot.Windows.Select(w => w.Id));
        Assert.Equal(new double[] { 75, 0 }, snapshot.Windows.Select(w => w.RemainingPercent));
        Assert.Equal("ChatGPT Plus", snapshot.AccountPlan);
        Assert.NotNull(snapshot.AccountScope);
        Assert.DoesNotContain("user@example.com", snapshot.AccountScope);
        Assert.Equal(new[] { "initialize", "initialized", "config/read", "account/read", "account/rateLimits/read" }, runner.Methods);
        Assert.Equal("--disable plugins app-server", Assert.Single(runner.Arguments));
    }

    [Fact]
    public async Task SuccessfulSession_PreservesResetTimesAndBothWindows()
    {
        var runner = new SessionRunner();
        var normal = runner.Result;
        runner.Result = (call, method) => method == "account/rateLimits/read" ? Fixture("codex_plus_full.json") : normal(call, method);
        var snapshot = await new CodexUsageProvider(runner, () => "fixture.exe").GetUsageAsync();
        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal(2, snapshot.Windows.Count);
        Assert.All(snapshot.Windows, window => Assert.NotNull(window.ResetsAt));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1787755078), snapshot.Windows[0].ResetsAt);
    }

    [Fact]
    public async Task EachRefresh_ReReadsQuotaAndKeepsScopeForSameAccount()
    {
        var runner = new SessionRunner();
        var normal = runner.Result;
        runner.Result = (call, method) => method == "account/rateLimits/read"
            ? JsonSerializer.Serialize(new { rateLimits = new { primary = new { usedPercent = call * 10, windowDurationMins = 300 } } })
            : normal(call, method);
        var provider = new CodexUsageProvider(runner, () => "fixture.exe");
        var first = await provider.GetUsageAsync();
        var second = await provider.GetUsageAsync();
        Assert.Equal(90, Assert.Single(first.Windows).RemainingPercent);
        Assert.Equal(80, Assert.Single(second.Windows).RemainingPercent);
        Assert.Equal(first.AccountScope, second.AccountScope);
        Assert.Equal(2, runner.Methods.Count(m => m == "account/rateLimits/read"));
    }

    [Theory]
    [InlineData("codex_config_plugins_enabled.json")]
    [InlineData("codex_config_plugins_unknown.json")]
    public async Task UnverifiedConfiguration_IsContainedWithoutRetryOrQuotaRead(string fixture)
    {
        var runner = new SessionRunner();
        var normal = runner.Result;
        runner.Result = (call, method) => method == "config/read" ? Fixture(fixture) : normal(call, method);
        var provider = new CodexUsageProvider(runner, () => "fixture.exe");
        var first = await provider.GetUsageAsync();
        var second = await provider.GetUsageAsync();
        Assert.Equal(ProviderStatus.Error, first.Status);
        Assert.Equal(CodexUsageProvider.PausedMessage, first.StatusMessage);
        Assert.Equal(first.StatusMessage, second.StatusMessage);
        Assert.Empty(first.Windows);
        Assert.Equal(1, runner.Calls);
        Assert.DoesNotContain("account/rateLimits/read", runner.Methods);
    }

    [Fact]
    public async Task UnsupportedConfigurationMethod_IsContained()
    {
        var runner = new SessionRunner();
        var normal = runner.Result;
        runner.Result = (call, method) => method == "config/read" ? throw new CodexRpcException(-32601, "Unsupported") : normal(call, method);
        var provider = new CodexUsageProvider(runner, () => "fixture.exe");
        Assert.Equal(CodexUsageProvider.PausedMessage, (await provider.GetUsageAsync()).StatusMessage);
        await provider.GetUsageAsync();
        Assert.Equal(1, runner.Calls);
    }

    [Fact]
    public async Task DifferentExecutable_CanVerifyConfigurationAfterContainment()
    {
        var runner = new SessionRunner();
        var normal = runner.Result;
        runner.Result = (call, method) => call == 1 && method == "config/read" ? Fixture("codex_config_plugins_enabled.json") : normal(call, method);
        var path = "fixture-one.exe";
        var provider = new CodexUsageProvider(runner, () => path);
        Assert.Equal(ProviderStatus.Error, (await provider.GetUsageAsync()).Status);
        path = "fixture-two.exe";
        Assert.Equal(ProviderStatus.Available, (await provider.GetUsageAsync()).Status);
        Assert.Equal(2, runner.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AccountChange_IsObservedEvenIfNewQuotaReadFails(bool quotaFails)
    {
        var runner = new SessionRunner();
        var normal = runner.Result;
        runner.Result = (call, method) =>
        {
            if (method == "account/read") return JsonSerializer.Serialize(new { account = new { type = "chatgpt", email = $"fixture-{call}@example.invalid", planType = "plus" } });
            if (call == 2 && quotaFails && method == "account/rateLimits/read") throw new TimeoutException();
            return normal(call, method);
        };
        var provider = new CodexUsageProvider(runner, () => "fixture.exe");
        var first = await provider.GetUsageAsync();
        var second = await provider.GetUsageAsync();
        Assert.NotNull(first.AccountScope);
        Assert.NotNull(second.AccountScope);
        Assert.NotEqual(first.AccountScope, second.AccountScope);
        Assert.Equal(quotaFails ? ProviderStatus.Timeout : ProviderStatus.Available, second.Status);
    }

    [Fact]
    public async Task AccountEnrichmentFailure_DoesNotDiscardSuccessfulQuota()
    {
        var runner = new SessionRunner();
        var normal = runner.Result;
        runner.Result = (call, method) => method == "account/read" ? throw new CodexRpcException(-32601, "Unsupported") : normal(call, method);
        var snapshot = await new CodexUsageProvider(runner, () => "fixture.exe").GetUsageAsync();
        Assert.Equal(ProviderStatus.Available, snapshot.Status);
        Assert.Equal(2, snapshot.Windows.Count);
        Assert.Null(snapshot.AccountScope);
    }

    [Fact]
    public async Task SignedOutAccount_IsUnauthenticatedAndCanRecoverAfterOfficialSignIn()
    {
        var runner = new SessionRunner();
        var normal = runner.Result;
        runner.Result = (call, method) => call == 1 ? method switch
        {
            "account/read" => Fixture("codex_account_unauthenticated.json"),
            "account/rateLimits/read" => "null",
            _ => normal(call, method)
        } : normal(call, method);
        var provider = new CodexUsageProvider(runner, () => "fixture.exe");
        Assert.Equal(ProviderStatus.Unauthenticated, (await provider.GetUsageAsync()).Status);
        Assert.Equal(ProviderStatus.Available, (await provider.GetUsageAsync()).Status);
        Assert.Equal(2, runner.Calls);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("crash")]
    public async Task TransientFailure_AllowsNextNormallyRequestedRead(string fault)
    {
        var runner = new SessionRunner();
        var normal = runner.Result;
        runner.Result = (call, method) => call == 1 && method == "account/rateLimits/read"
            ? throw (fault == "timeout" ? new TimeoutException() : new EndOfStreamException())
            : normal(call, method);
        var provider = new CodexUsageProvider(runner, () => "fixture.exe");
        Assert.Equal(fault == "timeout" ? ProviderStatus.Timeout : ProviderStatus.Error, (await provider.GetUsageAsync()).Status);
        Assert.Equal(1, runner.Calls);
        Assert.Equal(ProviderStatus.Available, (await provider.GetUsageAsync()).Status);
        Assert.Equal(2, runner.Calls);
    }

    [Fact]
    public async Task CancellationDuringAccountRead_DoesNotIssueQuotaRequestOrBlockFutureRefresh()
    {
        using var cts = new CancellationTokenSource();
        var runner = new SessionRunner();
        var normal = runner.Result;
        runner.Result = (call, method) =>
        {
            if (call == 1 && method == "account/read") { cts.Cancel(); cts.Token.ThrowIfCancellationRequested(); }
            return normal(call, method);
        };
        var provider = new CodexUsageProvider(runner, () => "fixture.exe");
        Assert.Equal(ProviderStatus.Cancelled, (await provider.GetUsageAsync(cts.Token)).Status);
        Assert.DoesNotContain("account/rateLimits/read", runner.Methods);
        Assert.Equal(ProviderStatus.Available, (await provider.GetUsageAsync()).Status);
    }
}
