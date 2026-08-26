namespace AIQuotaBar.Providers.Codex.Protocol;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class CodexRpcRequest
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; set; }
}

public sealed class CodexRpcNotification
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; set; }
}

public sealed class CodexRpcMessage
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("error")]
    public CodexRpcError? Error { get; set; }
}

public sealed class CodexRpcError
{
    [JsonPropertyName("code")]
    public int? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

public sealed class CodexInitializeParams
{
    [JsonPropertyName("clientInfo")]
    public CodexClientInfo ClientInfo { get; set; } = new();
}

public sealed class CodexClientInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "AIQuotaBar";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.1.0";
}

public sealed class CodexInitializeResult
{
    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("codexHome")]
    public string? CodexHome { get; set; }

    [JsonPropertyName("platformFamily")]
    public string? PlatformFamily { get; set; }

    [JsonPropertyName("platformOs")]
    public string? PlatformOs { get; set; }
}

public sealed class CodexRateLimitsResult
{
    [JsonPropertyName("rateLimits")]
    public CodexRateLimitSnapshot? RateLimits { get; set; }

    [JsonPropertyName("rateLimitsByLimitId")]
    public Dictionary<string, CodexRateLimitSnapshot>? RateLimitsByLimitId { get; set; }
}

public sealed class CodexRateLimitSnapshot
{
    [JsonPropertyName("limitId")]
    public string? LimitId { get; set; }

    [JsonPropertyName("limitName")]
    public string? LimitName { get; set; }

    [JsonPropertyName("planType")]
    public string? PlanType { get; set; }

    [JsonPropertyName("primary")]
    public CodexRateLimitWindow? Primary { get; set; }

    [JsonPropertyName("secondary")]
    public CodexRateLimitWindow? Secondary { get; set; }

    [JsonPropertyName("rateLimitReachedType")]
    public string? RateLimitReachedType { get; set; }

    [JsonPropertyName("spendControlReached")]
    public bool? SpendControlReached { get; set; }
}

public sealed class CodexRateLimitWindow
{
    [JsonPropertyName("usedPercent")]
    public int? UsedPercent { get; set; }

    [JsonPropertyName("windowDurationMins")]
    public long? WindowDurationMins { get; set; }

    [JsonPropertyName("resetsAt")]
    public long? ResetsAt { get; set; }
}

public sealed class CodexAccountResult
{
    [JsonPropertyName("account")]
    public CodexAccountInfo? Account { get; set; }

    [JsonPropertyName("requiresOpenaiAuth")]
    public bool? RequiresOpenaiAuth { get; set; }
}

public sealed class CodexAccountInfo
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("planType")]
    public string? PlanType { get; set; }
}
