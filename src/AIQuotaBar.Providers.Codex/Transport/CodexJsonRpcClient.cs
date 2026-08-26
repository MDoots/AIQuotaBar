namespace AIQuotaBar.Providers.Codex.Transport;

using System.Text.Json;
using AIQuotaBar.Providers.Codex.Protocol;

public sealed class CodexRpcException : Exception
{
    public int? ErrorCode { get; }
    public string? ErrorMessage { get; }

    public CodexRpcException(int? code, string? message)
        : base($"Codex RPC error {code}: {message}")
    {
        ErrorCode = code;
        ErrorMessage = message;
    }

    public CodexRpcException(string message) : base(message)
    {
    }
}

public sealed class CodexJsonRpcClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ICodexProcessSession _session;
    private long _nextId = 0;

    public CodexJsonRpcClient(ICodexProcessSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public async Task<CodexInitializeResult?> InitializeAsync(
        string clientName = "AIQuotaBar",
        string clientVersion = "0.1.0",
        CancellationToken cancellationToken = default)
    {
        var initParams = new CodexInitializeParams
        {
            ClientInfo = new CodexClientInfo
            {
                Name = clientName,
                Version = clientVersion
            }
        };

        var result = await SendRequestAsync<CodexInitializeResult>(
            "initialize",
            initParams,
            cancellationToken).ConfigureAwait(false);

        // Send required initialized notification after initialize handshake
        await SendNotificationAsync("initialized", null, cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async Task<TResult?> SendRequestAsync<TResult>(
        string method,
        object? @params = null,
        CancellationToken cancellationToken = default)
    {
        var requestId = Interlocked.Increment(ref _nextId);
        var request = new CodexRpcRequest
        {
            Id = requestId,
            Method = method,
            Params = @params ?? new object()
        };

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        await _session.WriteLineAsync(requestJson, cancellationToken).ConfigureAwait(false);

        while (true)
        {
            var line = await _session.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line == null)
            {
                throw new EndOfStreamException($"Codex app-server closed stream before responding to '{method}' (id={requestId})");
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            CodexRpcMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<CodexRpcMessage>(line, JsonOptions);
            }
            catch (JsonException)
            {
                // Skip unparseable lines or diagnostics
                continue;
            }

            if (message == null)
            {
                continue;
            }

            // If it's a notification (no id), ignore and continue reading
            if (!message.Id.HasValue)
            {
                continue;
            }

            if (message.Id.Value == requestId)
            {
                if (message.Error != null)
                {
                    throw new CodexRpcException(message.Error.Code, message.Error.Message);
                }

                if (message.Result.HasValue)
                {
                    return JsonSerializer.Deserialize<TResult>(message.Result.Value.GetRawText(), JsonOptions);
                }

                return default;
            }
        }
    }

    public async Task SendNotificationAsync(
        string method,
        object? @params = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new CodexRpcNotification
        {
            Method = method,
            Params = @params
        };

        var json = JsonSerializer.Serialize(notification, JsonOptions);
        await _session.WriteLineAsync(json, cancellationToken).ConfigureAwait(false);
    }
}
