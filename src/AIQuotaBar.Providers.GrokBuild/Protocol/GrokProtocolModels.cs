namespace AIQuotaBar.Providers.GrokBuild.Protocol;

using System.Text.Json.Serialization;

public sealed class GrokJsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public object? Params { get; set; }
}

public sealed class GrokJsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

public sealed class GrokJsonRpcResponse<T>
{
    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; set; }

    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }

    [JsonPropertyName("error")]
    public GrokJsonRpcError? Error { get; set; }
}

public sealed class GrokInitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public int? ProtocolVersion { get; set; }

    [JsonPropertyName("capabilities")]
    public object? Capabilities { get; set; }

    [JsonPropertyName("serverInfo")]
    public GrokServerInfo? ServerInfo { get; set; }

    [JsonPropertyName("authMethods")]
    public IReadOnlyList<GrokAuthMethod>? AuthMethods { get; set; }

    [JsonPropertyName("_meta")]
    public GrokInitializeMeta? Meta { get; set; }
}

public sealed class GrokServerInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

public sealed class GrokAuthMethod
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("interactive")]
    public bool? Interactive { get; set; }
}

public sealed class GrokInitializeMeta
{
    [JsonPropertyName("defaultAuthMethodId")]
    public string? DefaultAuthMethodId { get; set; }
}

public sealed class GrokBillingResult
{
    [JsonPropertyName("config")]
    public GrokBillingConfig? Config { get; set; }

    [JsonPropertyName("subscription_tier")]
    public string? SubscriptionTier { get; set; }

    [JsonPropertyName("subscriptionTier")]
    public string? SubscriptionTierCamel { get; set; }

    public string? EffectiveTier => SubscriptionTier ?? SubscriptionTierCamel;
}

public sealed class GrokBillingConfig
{
    [JsonPropertyName("currentPeriod")]
    public GrokCurrentPeriod? CurrentPeriod { get; set; }

    [JsonPropertyName("creditUsagePercent")]
    public double? CreditUsagePercent { get; set; }

    [JsonPropertyName("isUnifiedBillingUser")]
    public bool? IsUnifiedBillingUser { get; set; }

    [JsonPropertyName("monthlyLimit")]
    public GrokNumericVal? MonthlyLimit { get; set; }

    [JsonPropertyName("used")]
    public GrokNumericVal? Used { get; set; }

    [JsonPropertyName("billingPeriodStart")]
    public string? BillingPeriodStart { get; set; }

    [JsonPropertyName("billingPeriodEnd")]
    public string? BillingPeriodEnd { get; set; }
}

public sealed class GrokCurrentPeriod
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("start")]
    public string? Start { get; set; }

    [JsonPropertyName("end")]
    public string? End { get; set; }
}

public sealed class GrokNumericVal
{
    [JsonPropertyName("val")]
    public double? Val { get; set; }
}
