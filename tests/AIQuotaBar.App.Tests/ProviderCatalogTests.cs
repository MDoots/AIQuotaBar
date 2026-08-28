namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Providers;
using Xunit;

public class ProviderCatalogTests
{
    [Fact]
    public void Catalog_ContainsExactlySupportedProviders()
    {
        var all = ProviderCatalog.All;

        Assert.Equal(2, all.Count);
        Assert.Contains(all, p => p.Id == "codex");
        Assert.Contains(all, p => p.Id == "antigravity");
    }

    [Fact]
    public void CodexDescriptor_HasExpectedMetadata()
    {
        var codex = ProviderCatalog.Codex;

        Assert.Equal("codex", codex.Id);
        Assert.Equal("OpenAI Codex", codex.DisplayName);
        Assert.Equal("Codex", codex.ShortDisplayName);
        Assert.Equal(TimeSpan.FromSeconds(60), codex.RefreshInterval);
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
        Assert.Equal(new Uri("https://antigravity.google/download"), agy.SetupUri);

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
    public void GetDescriptor_ResolvesCaseInsensitively()
    {
        Assert.Same(ProviderCatalog.Codex, ProviderCatalog.GetDescriptor("codex"));
        Assert.Same(ProviderCatalog.Codex, ProviderCatalog.GetDescriptor("CODEX"));
        Assert.Same(ProviderCatalog.Antigravity, ProviderCatalog.GetDescriptor("antigravity"));
        Assert.Same(ProviderCatalog.Antigravity, ProviderCatalog.GetDescriptor("ANTIGRAVITY"));
        Assert.Null(ProviderCatalog.GetDescriptor("unknown_provider"));
    }
}
