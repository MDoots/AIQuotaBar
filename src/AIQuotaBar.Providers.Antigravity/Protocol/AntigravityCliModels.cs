namespace AIQuotaBar.Providers.Antigravity.Protocol;

using System.Text.Json.Serialization;

public sealed class AntigravityCliResponse
{
    [JsonPropertyName("conversation_id")]
    public string? ConversationId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("response")]
    public string? Response { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("command")]
    public AntigravityCommand? Command { get; set; }
}

public sealed class AntigravityCommand
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("data")]
    public AntigravityUsageData? Data { get; set; }
}

public sealed class AntigravityUsageData
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("groups")]
    public List<AntigravityGroup>? Groups { get; set; }
}

public sealed class AntigravityGroup
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("buckets")]
    public List<AntigravityBucket>? Buckets { get; set; }
}

public sealed class AntigravityBucket
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("window")]
    public string? Window { get; set; }

    [JsonPropertyName("remaining_fraction")]
    public double? RemainingFraction { get; set; }

    [JsonPropertyName("reset_time")]
    public string? ResetTime { get; set; }
}
