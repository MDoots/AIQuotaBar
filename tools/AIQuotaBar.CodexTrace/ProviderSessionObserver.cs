using System.Text.Json;
using AIQuotaBar.Providers.Codex.Transport;

// Developer-only observer. Records approved RPC method names and counts, never
// raw messages, configuration, account identity, stderr or exception text.
sealed class ProviderSessionObserver : ICodexProcessRunner
{
    public string LastStep { get; private set; } = "launch";
    public string? FailureKind { get; private set; }
    public List<string> CompletedMethods { get; } = [];
    public int ConfiguredMarketplaces { get; private set; }
    public void Reset()
    {
        LastStep = "launch";
        FailureKind = null;
        CompletedMethods.Clear();
        ConfiguredMarketplaces = 0;
    }
    public async Task RunAsync(string executablePath, string arguments,
        Func<ICodexProcessSession, CancellationToken, Task> action, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await new StandardCodexProcessRunner().RunAsync(executablePath, arguments,
                async (session, token) =>
                {
                    await action(new ObservedSession(session, this), token);
                    LastStep = "shutdown";
                }, timeout, cancellationToken);
        }
        catch (Exception ex)
        {
            FailureKind = ex switch
            {
                TimeoutException => "Timeout", OperationCanceledException => "Cancelled",
                EndOfStreamException => "ClosedStream", CodexRpcException => "RpcError",
                _ => "CommunicationError"
            };
            throw;
        }
    }

    sealed class ObservedSession(ICodexProcessSession inner, ProviderSessionObserver owner) : ICodexProcessSession
    {
        readonly Dictionary<long, string> methods = [];
        public async Task WriteLineAsync(string line, CancellationToken token = default)
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.TryGetProperty("id", out var id) && id.TryGetInt64(out var number) &&
                root.TryGetProperty("method", out var method) && method.GetString() is { } name &&
                name is "initialize" or "config/read" or "account/read" or "account/rateLimits/read")
            {
                owner.LastStep = name;
                methods[number] = name;
            }
            await inner.WriteLineAsync(line, token);
        }
        public async Task<string?> ReadLineAsync(CancellationToken token = default)
        {
            var line = await inner.ReadLineAsync(token);
            if (line != null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("id", out var id) && id.TryGetInt64(out var number) &&
                        methods.TryGetValue(number, out var name) && root.TryGetProperty("result", out var result))
                    {
                        owner.CompletedMethods.Add(name);
                        if (name == "config/read" && result.ValueKind == JsonValueKind.Object && result.TryGetProperty("config", out var config) &&
                            config.ValueKind == JsonValueKind.Object && config.TryGetProperty("marketplaces", out var marketplaces) && marketplaces.ValueKind == JsonValueKind.Object)
                            owner.ConfiguredMarketplaces = marketplaces.EnumerateObject().Count();
                    }
                }
                catch (JsonException) { }
            }
            return line;
        }
    }
}
