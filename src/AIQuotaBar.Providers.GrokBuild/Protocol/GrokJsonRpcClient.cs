namespace AIQuotaBar.Providers.GrokBuild.Protocol;

using System.Text.Json;
using AIQuotaBar.Providers.GrokBuild.Transport;

public class GrokRpcException : Exception
{
    public int ErrorCode { get; }
    public string? ErrorMessage { get; }

    public GrokRpcException(int errorCode, string? errorMessage)
        : base($"Grok RPC error {errorCode}: {errorMessage ?? "Unknown error"}")
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}

public class GrokAuthException : Exception
{
    public GrokAuthException(string message) : base(message) { }
}

public sealed class GrokJsonRpcClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IGrokProcessSession _session;
    private int _nextId = 1;

    public GrokJsonRpcClient(IGrokProcessSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public async Task InitializeAsync(string clientName, string clientVersion, CancellationToken cancellationToken = default)
    {
        var initParams = new
        {
            protocolVersion = 1,
            clientInfo = new
            {
                name = clientName,
                version = clientVersion
            },
            capabilities = new
            {
                _meta = new
                {
                    billing = true
                }
            }
        };

        await SendRequestAsync<object>("initialize", initParams, cancellationToken).ConfigureAwait(false);
    }

    public async Task AuthenticateAsync(string methodId = "cached_token", CancellationToken cancellationToken = default)
    {
        var authParams = new
        {
            methodId = methodId
        };

        try
        {
            await SendRequestAsync<object>("authenticate", authParams, cancellationToken).ConfigureAwait(false);
        }
        catch (GrokRpcException rpcEx) when (rpcEx.ErrorCode == -32602 || rpcEx.ErrorMessage?.Contains("auth", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new GrokAuthException("Grok authentication failed: " + rpcEx.ErrorMessage);
        }
    }

    public async Task<GrokBillingResult?> GetBillingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await SendRequestAsync<GrokBillingResult>("_x.ai/billing", new { }, cancellationToken).ConfigureAwait(false);
        }
        catch (GrokRpcException rpcEx) when (rpcEx.ErrorCode == -32601)
        {
            // Method not found: try x.ai/billing as fallback
            return await SendRequestAsync<GrokBillingResult>("x.ai/billing", new { }, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<T?> SendRequestAsync<T>(string method, object? parameters, CancellationToken cancellationToken = default)
    {
        var requestId = _nextId++;
        var request = new GrokJsonRpcRequest
        {
            Id = requestId,
            Method = method,
            Params = parameters
        };

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        await _session.WriteLineAsync(requestJson, cancellationToken).ConfigureAwait(false);

        while (true)
        {
            var line = await _session.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line == null)
            {
                throw new EndOfStreamException("Grok process closed standard output before response.");
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                // If this is an unnumbered notification (e.g. method without ID), skip it
                if (!root.TryGetProperty("id", out var idProp) || idProp.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                if (idProp.GetInt32() != requestId)
                {
                    continue;
                }

                if (root.TryGetProperty("error", out var errorProp) && errorProp.ValueKind == JsonValueKind.Object)
                {
                    var code = errorProp.TryGetProperty("code", out var codeProp) ? codeProp.GetInt32() : -1;
                    var message = errorProp.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                    throw new GrokRpcException(code, message);
                }

                if (root.TryGetProperty("result", out var resultProp))
                {
                    return JsonSerializer.Deserialize<T>(resultProp.GetRawText(), JsonOptions);
                }

                return default;
            }
            catch (JsonException)
            {
                // Non-JSON line from stdout, continue reading
            }
        }
    }
}
