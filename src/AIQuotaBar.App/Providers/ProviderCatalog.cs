namespace AIQuotaBar.App.Providers;

using AIQuotaBar.Providers.Antigravity;
using AIQuotaBar.Providers.Antigravity.Transport;
using AIQuotaBar.Providers.ClaudeCode;
using AIQuotaBar.Providers.Codex;
using AIQuotaBar.Providers.Codex.Transport;
using AIQuotaBar.Providers.GitHubCopilot;
using AIQuotaBar.Providers.GrokBuild;

public static class ProviderCatalog
{
    public static readonly ProviderDescriptor Codex = new()
    {
        Id = "codex",
        DisplayName = "OpenAI Codex",
        ShortDisplayName = "Codex",
        RefreshInterval = TimeSpan.FromSeconds(60),
        AccentColor = "#10B981",
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
        AccentColor = "#38BDF8",
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

    public static readonly ProviderDescriptor ClaudeCode = new()
    {
        Id = "claude-code",
        DisplayName = "Claude Code",
        ShortDisplayName = "Claude",
        RefreshInterval = TimeSpan.FromSeconds(180),
        AccentColor = "#D97757",
        CreateProvider = () => new ClaudeCodeUsageProvider(),
        LocateExecutable = () => ClaudeCodeProcessLocator.LocateExecutable(),
        SetupUri = new Uri("https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview"),
        KnownQuotaWindows = new[]
        {
            new KnownQuotaWindowDescriptor("session-5h", "5-Hour Session"),
            new KnownQuotaWindowDescriptor("weekly-all", "Weekly")
        }
    };

    public static readonly ProviderDescriptor GrokBuild = new()
    {
        Id = "grok-build",
        DisplayName = "Grok Build",
        ShortDisplayName = "Grok",
        RefreshInterval = TimeSpan.FromSeconds(180),
        AccentColor = "#D1D5DB",
        CreateProvider = () => new GrokBuildUsageProvider(),
        LocateExecutable = () => GrokBuildProcessLocator.LocateExecutable(),
        SetupUri = new Uri("https://docs.x.ai/build/overview"),
        KnownQuotaWindows = new[]
        {
            new KnownQuotaWindowDescriptor("shared-weekly", "Grok · Weekly"),
            new KnownQuotaWindowDescriptor("shared-monthly", "Grok · Monthly")
        }
    };

    public static readonly ProviderDescriptor GitHubCopilot = new()
    {
        Id = "github-copilot",
        DisplayName = "GitHub Copilot",
        ShortDisplayName = "Copilot",
        RefreshInterval = TimeSpan.FromSeconds(180),
        AccentColor = "#A78BFA",
        CreateProvider = () => new GitHubCopilotUsageProvider(),
        LocateExecutable = () => GitHubCopilotProcessLocator.LocateExecutable(),
        SetupUri = new Uri("https://docs.github.com/copilot/how-tos/copilot-cli"),
        KnownQuotaWindows = new[]
        {
            new KnownQuotaWindowDescriptor("premium", "Premium")
        }
    };

    public static IReadOnlyList<ProviderDescriptor> All { get; } = new[]
    {
        Codex,
        Antigravity,
        ClaudeCode,
        GrokBuild,
        GitHubCopilot
    };

    public static ProviderDescriptor? GetDescriptor(string providerId)
    {
        return All.FirstOrDefault(d => string.Equals(d.Id, providerId, StringComparison.OrdinalIgnoreCase));
    }
}
