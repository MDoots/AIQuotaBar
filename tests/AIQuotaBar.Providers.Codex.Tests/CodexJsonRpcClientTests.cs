namespace AIQuotaBar.Providers.Codex.Tests;

using System.Text.Json;
using AIQuotaBar.Providers.Codex.Protocol;
using AIQuotaBar.Providers.Codex.Transport;
using Xunit;

public class CodexJsonRpcClientTests
{
    private sealed class MockProcessSession : ICodexProcessSession
    {
        private readonly Queue<string> _incomingLines = new();
        public List<string> WrittenLines { get; } = new();

        public void EnqueueLine(string line) => _incomingLines.Enqueue(line);

        public Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
        {
            WrittenLines.Add(line);
            return Task.CompletedTask;
        }

        public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
        {
            if (_incomingLines.Count > 0)
            {
                return Task.FromResult<string?>(_incomingLines.Dequeue());
            }
            return Task.FromResult<string?>(null);
        }
    }

    [Fact]
    public async Task InitializeAsync_FollowsHandshakeOrdering()
    {
        var session = new MockProcessSession();
        // Server response to initialize (id: 1)
        session.EnqueueLine(@"{""id"":1,""result"":{""userAgent"":""test"",""codexHome"":""C:\\home"",""platformFamily"":""windows"",""platformOs"":""windows""}}");

        var client = new CodexJsonRpcClient(session);
        var result = await client.InitializeAsync("AIQuotaBar", "0.1.0");

        Assert.NotNull(result);
        Assert.Equal("test", result.UserAgent);

        // Verify written lines: 1st is initialize request, 2nd is initialized notification
        Assert.Equal(2, session.WrittenLines.Count);

        var initReq = JsonSerializer.Deserialize<CodexRpcRequest>(session.WrittenLines[0]);
        Assert.Equal("initialize", initReq?.Method);
        Assert.Equal(1, initReq?.Id);

        var initializedNotif = JsonSerializer.Deserialize<CodexRpcNotification>(session.WrittenLines[1]);
        Assert.Equal("initialized", initializedNotif?.Method);
    }

    [Fact]
    public async Task SendRequestAsync_IgnoresUnrelatedServerNotificationBeforeResponse()
    {
        var session = new MockProcessSession();
        // 1. Unsolicited server notification arriving first
        session.EnqueueLine(@"{""method"":""remoteControl/status/changed"",""params"":{""status"":""disabled""}}");
        // 2. Expected response for id 1
        session.EnqueueLine(@"{""id"":1,""result"":{""rateLimits"":{""limitId"":""codex""}}}");

        var client = new CodexJsonRpcClient(session);
        var result = await client.SendRequestAsync<CodexRateLimitsResult>("account/rateLimits/read");

        Assert.NotNull(result?.RateLimits);
        Assert.Equal("codex", result.RateLimits.LimitId);
    }

    [Fact]
    public async Task SendRequestAsync_ThrowsCodexRpcException_OnErrorResponse()
    {
        var session = new MockProcessSession();
        session.EnqueueLine(@"{""id"":1,""error"":{""code"":-32600,""message"":""Invalid Request""}}");

        var client = new CodexJsonRpcClient(session);

        var ex = await Assert.ThrowsAsync<CodexRpcException>(() =>
            client.SendRequestAsync<CodexRateLimitsResult>("invalid/method"));

        Assert.Equal(-32600, ex.ErrorCode);
        Assert.Contains("Invalid Request", ex.Message);
    }

    [Fact]
    public async Task SendRequestAsync_ThrowsEndOfStreamException_WhenStreamClosesEarly()
    {
        var session = new MockProcessSession(); // Empty stream

        var client = new CodexJsonRpcClient(session);

        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            client.SendRequestAsync<CodexRateLimitsResult>("account/rateLimits/read"));
    }
}
