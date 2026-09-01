namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Providers;
using AIQuotaBar.App.ViewModels;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using Xunit;

public class ProviderCatalogTests
{
    private sealed class CustomUnknownUsageProvider : IUsageProvider
    {
        public string Id => "custom-unknown-provider";
        public string DisplayName => "Custom Unknown Provider";

        public Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderSnapshot(
                Id,
                DisplayName,
                ProviderStatus.Available,
                null,
                "Custom Plan",
                windows: Array.Empty<QuotaWindow>()));
        }
    }

    [Fact]
    public void Catalog_ContainsExactlySupportedProviders_InCanonicalOrder()
    {
        var all = ProviderCatalog.All;

        Assert.Equal(5, all.Count);
        Assert.Equal("codex", all[0].Id);
        Assert.Equal("antigravity", all[1].Id);
        Assert.Equal("claude-code", all[2].Id);
        Assert.Equal("grok-build", all[3].Id);
        Assert.Equal("github-copilot", all[4].Id);
    }

    [Fact]
    public void All_HasUniqueAccentColors_ForEveryProvider()
    {
        var all = ProviderCatalog.All;
        var accents = all.Select(p => p.AccentColor).ToList();

        Assert.Equal(5, accents.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("#10B981", ProviderCatalog.Codex.AccentColor);
        Assert.Equal("#38BDF8", ProviderCatalog.Antigravity.AccentColor);
        Assert.Equal("#D97757", ProviderCatalog.ClaudeCode.AccentColor);
        Assert.Equal("#D1D5DB", ProviderCatalog.GrokBuild.AccentColor);
        Assert.Equal("#A78BFA", ProviderCatalog.GitHubCopilot.AccentColor);
    }

    [Fact]
    public void CodexDescriptor_HasExpectedMetadata()
    {
        var codex = ProviderCatalog.Codex;

        Assert.Equal("codex", codex.Id);
        Assert.Equal("OpenAI Codex", codex.DisplayName);
        Assert.Equal("Codex", codex.ShortDisplayName);
        Assert.Equal(TimeSpan.FromSeconds(60), codex.RefreshInterval);
        Assert.Equal("#10B981", codex.AccentColor);
        Assert.Equal(new Uri("https://developers.openai.com/codex/cli/"), codex.SetupUri);

        Assert.Equal(2, codex.KnownQuotaWindows.Count);
        Assert.Equal("primary", codex.KnownQuotaWindows[0].Id);
        Assert.Equal("5-Hour", codex.KnownQuotaWindows[0].DisplayName);
        Assert.Equal("secondary", codex.KnownQuotaWindows[1].Id);
        Assert.Equal("Weekly", codex.KnownQuotaWindows[1].DisplayName);

        var providerInstance = codex.CreateProvider();
        Assert.NotNull(providerInstance);
        Assert.Equal("codex", providerInstance.Id);
        Assert.Equal("OpenAI Codex", providerInstance.DisplayName);
    }

    [Fact]
    public void AntigravityDescriptor_HasExpectedMetadata()
    {
        var agy = ProviderCatalog.Antigravity;

        Assert.Equal("antigravity", agy.Id);
        Assert.Equal("Google Antigravity", agy.DisplayName);
        Assert.Equal("Antigravity", agy.ShortDisplayName);
        Assert.Equal(TimeSpan.FromSeconds(180), agy.RefreshInterval);
        Assert.Equal("#38BDF8", agy.AccentColor);
        Assert.Equal(new Uri("https://antigravity.google/docs/cli/install/"), agy.SetupUri);

        Assert.Equal(4, agy.KnownQuotaWindows.Count);
        Assert.Equal("gemini_gemini-5h", agy.KnownQuotaWindows[0].Id);
        Assert.Equal("Gemini · 5-Hour", agy.KnownQuotaWindows[0].DisplayName);
        Assert.Equal("gemini_gemini-weekly", agy.KnownQuotaWindows[1].Id);
        Assert.Equal("Gemini · Weekly", agy.KnownQuotaWindows[1].DisplayName);
        Assert.Equal("claude_and_gpt_3p-5h", agy.KnownQuotaWindows[2].Id);
        Assert.Equal("Claude & GPT · 5-Hour", agy.KnownQuotaWindows[2].DisplayName);
        Assert.Equal("claude_and_gpt_3p-weekly", agy.KnownQuotaWindows[3].Id);
        Assert.Equal("Claude & GPT · Weekly", agy.KnownQuotaWindows[3].DisplayName);

        var providerInstance = agy.CreateProvider();
        Assert.NotNull(providerInstance);
        Assert.Equal("antigravity", providerInstance.Id);
        Assert.Equal("Google Antigravity", providerInstance.DisplayName);
    }

    [Fact]
    public void ClaudeCodeDescriptor_HasExpectedMetadata()
    {
        var claude = ProviderCatalog.ClaudeCode;

        Assert.Equal("claude-code", claude.Id);
        Assert.Equal("Claude Code", claude.DisplayName);
        Assert.Equal("Claude", claude.ShortDisplayName);
        Assert.Equal(TimeSpan.FromSeconds(180), claude.RefreshInterval);
        Assert.Equal("#D97757", claude.AccentColor);
        Assert.Equal(new Uri("https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview"), claude.SetupUri);

        Assert.Equal(3, claude.KnownQuotaWindows.Count);
        Assert.Equal("session-5h", claude.KnownQuotaWindows[0].Id);
        Assert.Equal("Session · 5-hour", claude.KnownQuotaWindows[0].DisplayName);
        Assert.Equal("weekly-all", claude.KnownQuotaWindows[1].Id);
        Assert.Equal("Weekly · All models", claude.KnownQuotaWindows[1].DisplayName);
        Assert.Equal("weekly-opus", claude.KnownQuotaWindows[2].Id);
        Assert.Equal("Weekly · Claude Opus", claude.KnownQuotaWindows[2].DisplayName);

        var providerInstance = claude.CreateProvider();
        Assert.NotNull(providerInstance);
        Assert.Equal("claude-code", providerInstance.Id);
        Assert.Equal("Claude Code", providerInstance.DisplayName);
    }

    [Fact]
    public void GrokBuildDescriptor_HasExpectedMetadata()
    {
        var grok = ProviderCatalog.GrokBuild;

        Assert.Equal("grok-build", grok.Id);
        Assert.Equal("Grok Build", grok.DisplayName);
        Assert.Equal("Grok", grok.ShortDisplayName);
        Assert.Equal(TimeSpan.FromSeconds(180), grok.RefreshInterval);
        Assert.Equal("#D1D5DB", grok.AccentColor);
        Assert.Equal(new Uri("https://docs.x.ai/build/overview"), grok.SetupUri);

        Assert.Equal(2, grok.KnownQuotaWindows.Count);
        Assert.Equal("shared-weekly", grok.KnownQuotaWindows[0].Id);
        Assert.Equal("Grok · Weekly", grok.KnownQuotaWindows[0].DisplayName);
        Assert.Equal("shared-monthly", grok.KnownQuotaWindows[1].Id);
        Assert.Equal("Grok · Monthly", grok.KnownQuotaWindows[1].DisplayName);

        var providerInstance = grok.CreateProvider();
        Assert.NotNull(providerInstance);
        Assert.Equal("grok-build", providerInstance.Id);
        Assert.Equal("Grok Build", providerInstance.DisplayName);
    }

    [Fact]
    public void GitHubCopilotDescriptor_HasExpectedMetadata()
    {
        var copilot = ProviderCatalog.GitHubCopilot;

        Assert.Equal("github-copilot", copilot.Id);
        Assert.Equal("GitHub Copilot", copilot.DisplayName);
        Assert.Equal("Copilot", copilot.ShortDisplayName);
        Assert.Equal(TimeSpan.FromSeconds(180), copilot.RefreshInterval);
        Assert.Equal("#A78BFA", copilot.AccentColor);
        Assert.Equal(new Uri("https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/install-copilot-cli"), copilot.SetupUri);

        Assert.Equal(3, copilot.KnownQuotaWindows.Count);
        Assert.Equal("premium", copilot.KnownQuotaWindows[0].Id);
        Assert.Equal("Premium interactions", copilot.KnownQuotaWindows[0].DisplayName);
        Assert.Equal("chat", copilot.KnownQuotaWindows[1].Id);
        Assert.Equal("Chat", copilot.KnownQuotaWindows[1].DisplayName);
        Assert.Equal("completions", copilot.KnownQuotaWindows[2].Id);
        Assert.Equal("Completions", copilot.KnownQuotaWindows[2].DisplayName);

        var providerInstance = copilot.CreateProvider();
        Assert.NotNull(providerInstance);
        Assert.Equal("github-copilot", providerInstance.Id);
        Assert.Equal("GitHub Copilot", providerInstance.DisplayName);
    }

    [Fact]
    public void GetDescriptor_ResolvesCaseInsensitively()
    {
        Assert.Same(ProviderCatalog.Codex, ProviderCatalog.GetDescriptor("codex"));
        Assert.Same(ProviderCatalog.Codex, ProviderCatalog.GetDescriptor("CODEX"));
        Assert.Same(ProviderCatalog.Antigravity, ProviderCatalog.GetDescriptor("antigravity"));
        Assert.Same(ProviderCatalog.Antigravity, ProviderCatalog.GetDescriptor("ANTIGRAVITY"));
        Assert.Same(ProviderCatalog.ClaudeCode, ProviderCatalog.GetDescriptor("claude-code"));
        Assert.Same(ProviderCatalog.GrokBuild, ProviderCatalog.GetDescriptor("grok-build"));
        Assert.Same(ProviderCatalog.GitHubCopilot, ProviderCatalog.GetDescriptor("github-copilot"));
        Assert.Null(ProviderCatalog.GetDescriptor("unknown_provider"));
    }

    [Fact]
    public void ProviderSectionViewModel_CustomProvider_ConstructsWithoutCatalogDependency_UsesNeutralFallback()
    {
        var customProvider = new CustomUnknownUsageProvider();
        Assert.Null(ProviderCatalog.GetDescriptor(customProvider.Id));

        var vm = new ProviderSectionViewModel(customProvider, TimeSpan.FromSeconds(30));

        Assert.Equal("custom-unknown-provider", vm.ProviderId);
        Assert.Equal("Custom Unknown Provider", vm.ProviderName);
        Assert.Equal("Custom Unknown Provider", vm.ShortDisplayName);
        Assert.Equal("#6B7280", vm.ProviderAccentColor); // Neutral gray fallback, NOT Codex green
    }
}
