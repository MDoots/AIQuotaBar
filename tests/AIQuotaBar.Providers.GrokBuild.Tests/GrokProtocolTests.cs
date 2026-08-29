namespace AIQuotaBar.Providers.GrokBuild.Tests;

using AIQuotaBar.Providers.GrokBuild.Protocol;
using AIQuotaBar.Providers.GrokBuild.Transport;
using Xunit;

public class GrokProtocolTests
{
    private sealed class MockGrokSession : IGrokProcessSession
    {
        private readonly Queue<string> _responses;
        public List<string> SentLines { get; } = new();

        public MockGrokSession(IEnumerable<string> responses)
        {
            _responses = new Queue<string>(responses);
        }

        public Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
        {
            SentLines.Add(line);
            return Task.CompletedTask;
        }

        public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
        {
            if (_responses.Count > 0)
            {
                return Task.FromResult<string?>(_responses.Dequeue());
            }
            return Task.FromResult<string?>(null);
        }
    }

    [Fact]
    public async Task Client_IgnoresNotifications_AndReturnsMatchingResponse()
    {
        var responses = new[]
        {
            // Notification with no id
            "{\"jsonrpc\":\"2.0\",\"method\":\"_x.ai/mcp/servers_updated\",\"params\":{}}",
            // Response to id 1 (initialize)
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":1}}",
            // Response to id 2 (authenticate)
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{\"_meta\":{\"subscription_tier\":\"Free\"}}}",
            // Response to id 3 (x.ai/billing)
            "{\"jsonrpc\":\"2.0\",\"id\":3,\"result\":{\"config\":{\"creditUsagePercent\":10.0,\"isUnifiedBillingUser\":true},\"subscription_tier\":\"Free\"}}"
        };

        var session = new MockGrokSession(responses);
        var client = new GrokJsonRpcClient(session);

        await client.InitializeAsync("TestClient", "1.0");
        await client.AuthenticateAsync();
        var billing = await client.GetBillingAsync();

        Assert.NotNull(billing);
        Assert.Equal("Free", billing.SubscriptionTier);
        Assert.Equal(10.0, billing.Config?.CreditUsagePercent);
        Assert.Equal(3, session.SentLines.Count);
        Assert.Contains("x.ai/billing", session.SentLines[2]);
    }

    [Fact]
    public async Task Client_TriesXAiBillingFirst_AndFallsBackToUnderscoreBillingOnMethodNotFound()
    {
        var responses = new[]
        {
            // Response to id 1 (x.ai/billing -> Method not found)
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":-32601,\"message\":\"Method not found: x.ai/billing\"}}",
            // Response to id 2 (_x.ai/billing -> Success)
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{\"config\":{\"creditUsagePercent\":25.0},\"subscription_tier\":\"Free\"}}"
        };

        var session = new MockGrokSession(responses);
        var client = new GrokJsonRpcClient(session);

        var billing = await client.GetBillingAsync();

        Assert.NotNull(billing);
        Assert.Equal(25.0, billing.Config?.CreditUsagePercent);
        Assert.Equal(2, session.SentLines.Count);
        Assert.Contains("x.ai/billing", session.SentLines[0]);
        Assert.Contains("_x.ai/billing", session.SentLines[1]);
    }

    [Fact]
    public async Task Client_BillingNon32601Error_DoesNotFallback_ThrowsException()
    {
        var responses = new[]
        {
            // Response to id 1 (x.ai/billing -> Internal error -32603)
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":-32603,\"message\":\"Internal server error\"}}"
        };

        var session = new MockGrokSession(responses);
        var client = new GrokJsonRpcClient(session);

        var ex = await Assert.ThrowsAsync<GrokRpcException>(() => client.GetBillingAsync());
        Assert.Equal(-32603, ex.ErrorCode);
        Assert.Single(session.SentLines); // Did NOT send _x.ai/billing fallback
    }

    [Fact]
    public async Task Client_WhenAuthFails_ThrowsGrokAuthException()
    {
        var responses = new[]
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":-32602,\"message\":\"authentication failed: token expired\"}}"
        };

        var session = new MockGrokSession(responses);
        var client = new GrokJsonRpcClient(session);

        await Assert.ThrowsAsync<GrokAuthException>(() => client.AuthenticateAsync());
    }
}
