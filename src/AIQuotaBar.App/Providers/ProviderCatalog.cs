namespace AIQuotaBar.App.Providers;

using AIQuotaBar.Providers.Antigravity;
using AIQuotaBar.Providers.Antigravity.Transport;
using AIQuotaBar.Providers.Codex;
using AIQuotaBar.Providers.Codex.Transport;

public static class ProviderCatalog
{
    public static readonly ProviderDescriptor Codex = new()
    {
        Id = "codex",
        DisplayName = "OpenAI Codex",
        ShortDisplayName = "Codex",
        RefreshInterval = TimeSpan.FromSeconds(60),
        CreateProvider = () => new CodexUsageProvider(),
        LocateExecutable = () => CodexProcessLocator.LocateExecutable(),
        SetupUri = new Uri("https://developers.openai.com/codex/cli/"),
        KnownQuotaWindows = new[]
        {
            new KnownQuotaWindowDescriptor("primary", "5-Hour"),
            new KnownQuotaWindowDescriptor("secondary", "Weekly")
        }
    };

    public static readonly ProviderDescriptor Antigravity = new()
    {
        Id = "antigravity",
        DisplayName = "Google Antigravity",
        ShortDisplayName = "Antigravity",
        RefreshInterval = TimeSpan.FromSeconds(180),
        CreateProvider = () => new AntigravityUsageProvider(),
        LocateExecutable = () => AntigravityProcessLocator.LocateExecutable(),
        SetupUri = new Uri("https://antigravity.google/download"),
        KnownQuotaWindows = new[]
        {
            new KnownQuotaWindowDescriptor("gemini_gemini-5h", "Gemini · 5-Hour"),
            new KnownQuotaWindowDescriptor("gemini_gemini-weekly", "Gemini · Weekly"),
            new KnownQuotaWindowDescriptor("claude_and_gpt_3p-5h", "Claude & GPT · 5-Hour"),
            new KnownQuotaWindowDescriptor("claude_and_gpt_3p-weekly", "Claude & GPT · Weekly")
        }
    };

    public static IReadOnlyList<ProviderDescriptor> All { get; } = new[]
    {
        Codex,
        Antigravity
    };

    public static ProviderDescriptor? GetDescriptor(string providerId)
    {
        return All.FirstOrDefault(d => string.Equals(d.Id, providerId, StringComparison.OrdinalIgnoreCase));
    }
}
