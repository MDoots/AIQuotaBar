namespace AIQuotaBar.App.Tests;

using System.IO;
using AIQuotaBar.App.Providers;
using AIQuotaBar.App.Settings;
using AIQuotaBar.App.ViewModels;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using Xunit;

public class SettingsProviderSetupTests
{
    private sealed class StubUsageProvider : IUsageProvider
    {
        public string Id { get; }
        public string DisplayName { get; }

        public StubUsageProvider(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        public Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderSnapshot(Id, DisplayName, ProviderStatus.Available));
    }

    [Fact]
    public void SettingsViewModel_PopulatesSetupItemsForAllProviders()
    {
        var settings = new AppSettings();
        var manager = new SettingsManager(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json"));
        using var widgetVm = new WidgetViewModel();

        using var settingsVm = new SettingsViewModel(settings, manager, widgetVm);

        Assert.Equal(5, settingsVm.ProviderSetupItems.Count);
        Assert.Contains(settingsVm.ProviderSetupItems, i => i.ProviderId == "codex" && i.DisplayName == "OpenAI Codex");
        Assert.Contains(settingsVm.ProviderSetupItems, i => i.ProviderId == "antigravity" && i.DisplayName == "Google Antigravity");
        Assert.Contains(settingsVm.ProviderSetupItems, i => i.ProviderId == "claude-code" && i.DisplayName == "Claude Code");
        Assert.Contains(settingsVm.ProviderSetupItems, i => i.ProviderId == "grok-build" && i.DisplayName == "Grok Build");
        Assert.Contains(settingsVm.ProviderSetupItems, i => i.ProviderId == "github-copilot" && i.DisplayName == "GitHub Copilot");
    }

    [Theory]
    [InlineData("codex", "OpenAI Codex", "Codex", "AIQuotaBar requires the Codex CLI. Install it, then rescan.")]
    [InlineData("antigravity", "Google Antigravity", "Antigravity", "AIQuotaBar requires the Antigravity CLI (agy). Install it, then rescan.")]
    [InlineData("claude-code", "Claude Code", "Claude", "AIQuotaBar requires the Claude Code CLI. Install it, then rescan.")]
    [InlineData("grok-build", "Grok Build", "Grok", "AIQuotaBar requires the Grok CLI. Install it, then rescan.")]
    [InlineData("github-copilot", "GitHub Copilot", "Copilot", "AIQuotaBar requires the GitHub Copilot CLI. Install it, then rescan.")]
    public void ProviderSetupItem_NotDetectedState_FormatsCorrectly(string id, string name, string shortName, string expectedDetail)
    {
        var descriptor = new ProviderDescriptor
        {
            Id = id,
            DisplayName = name,
            ShortDisplayName = shortName,
            RefreshInterval = TimeSpan.FromSeconds(60),
            CreateProvider = () => new StubUsageProvider(id, name),
            LocateExecutable = () => null,
            SetupUri = new Uri("https://example.com"),
            KnownQuotaWindows = Array.Empty<KnownQuotaWindowDescriptor>()
        };

        var item = new ProviderSetupItemViewModel(descriptor);
        item.UpdateStatus(ProviderDiscoveryStatus.NotDetected, null);

        Assert.Equal("Not detected", item.StatusLabel);
        Assert.Equal(expectedDetail, item.DetailText);
    }

    [Fact]
    public void ProviderSetupItem_CheckingState_FormatsCorrectly()
    {
        var descriptor = ProviderCatalog.Codex;
        var item = new ProviderSetupItemViewModel(descriptor);
        item.UpdateStatus(ProviderDiscoveryStatus.Checking, null);

        Assert.Equal("Checking", item.StatusLabel);
        Assert.Equal("Checking local installation...", item.DetailText);
    }

    [Fact]
    public void ProviderSetupItem_ConnectedState_FormatsCorrectly()
    {
        var descriptor = ProviderCatalog.Codex;
        var section = new ProviderSectionViewModel(new StubUsageProvider("codex", "OpenAI Codex"), TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.Detected);
        section.ApplySnapshot(new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 25.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        var item = new ProviderSetupItemViewModel(descriptor);
        item.UpdateStatus(ProviderDiscoveryStatus.Detected, section);

        Assert.Equal("Connected", item.StatusLabel);
        Assert.Equal("Detected locally", item.DetailText);
    }

    [Fact]
    public void ProviderSetupItem_SignRequiredState_FormatsCorrectly()
    {
        var descriptor = ProviderCatalog.Codex;
        var section = new ProviderSectionViewModel(new StubUsageProvider("codex", "OpenAI Codex"), TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.Detected);
        section.ApplySnapshot(new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Unauthenticated, statusMessage: "Codex account requires login"));

        var item = new ProviderSetupItemViewModel(descriptor);
        item.UpdateStatus(ProviderDiscoveryStatus.Detected, section);

        Assert.Equal("Sign-in required", item.StatusLabel);
        Assert.Equal("Sign in via the Codex CLI, then rescan.", item.DetailText);
    }

    [Fact]
    public void ProviderSetupItem_NeedsAttentionState_FormatsCorrectly()
    {
        var descriptor = ProviderCatalog.Antigravity;
        var section = new ProviderSectionViewModel(new StubUsageProvider("antigravity", "Google Antigravity"), TimeSpan.FromSeconds(180), "Antigravity", ProviderDiscoveryStatus.Detected);
        section.ApplySnapshot(new ProviderSnapshot(
            "antigravity", "Google Antigravity", ProviderStatus.Timeout, statusMessage: "Antigravity CLI query timed out"));

        var item = new ProviderSetupItemViewModel(descriptor);
        item.UpdateStatus(ProviderDiscoveryStatus.Detected, section);

        Assert.Equal("Needs attention", item.StatusLabel);
        Assert.Equal("Antigravity CLI query timed out", item.DetailText);
    }

    [Fact]
    public void ProviderSetupItem_OpenGuideCommand_InvokesLauncherSafely()
    {
        Uri? launchedUri = null;
        var descriptor = ProviderCatalog.Codex;
        var item = new ProviderSetupItemViewModel(descriptor, uri => launchedUri = uri);

        item.OpenGuideCommand.Execute(null);

        Assert.Equal(new Uri("https://developers.openai.com/codex/cli/"), launchedUri);
    }

    [Fact]
    public void ProviderSetupItem_OpenGuideCommand_HandlesExceptionWithoutCrashing()
    {
        var descriptor = ProviderCatalog.Codex;
        var item = new ProviderSetupItemViewModel(descriptor, _ => throw new InvalidOperationException("Failed to spawn process"));

        // Must not throw
        var exception = Record.Exception(() => item.OpenGuideCommand.Execute(null));
        Assert.Null(exception);
    }

    [Fact]
    public async Task SettingsViewModel_RescanCommand_TogglesIsRescanning()
    {
        var settings = new AppSettings();
        var manager = new SettingsManager(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json"));
        using var widgetVm = new WidgetViewModel();

        using var settingsVm = new SettingsViewModel(settings, manager, widgetVm);

        Assert.True(settingsVm.CanRescan);
        Assert.False(settingsVm.IsRescanning);

        await settingsVm.RescanProvidersAsync();

        Assert.True(settingsVm.CanRescan);
        Assert.False(settingsVm.IsRescanning);
    }

    [Fact]
    public void ProviderSetupItem_UnknownState_FormatsAsChecking()
    {
        var descriptor = ProviderCatalog.Codex;
        var item = new ProviderSetupItemViewModel(descriptor);
        item.UpdateStatus(ProviderDiscoveryStatus.Unknown, null);

        Assert.Equal("Checking", item.StatusLabel);
        Assert.Equal("Checking local installation...", item.DetailText);
    }

    [Fact]
    public void ProviderSetupItem_ErrorState_FormatsAsNeedsAttentionWithSafeMessage()
    {
        var descriptor = ProviderCatalog.Codex;
        var item = new ProviderSetupItemViewModel(descriptor);
        item.UpdateStatus(ProviderDiscoveryStatus.Error, null, "Unable to check OpenAI Codex installation.");

        Assert.Equal("Needs attention", item.StatusLabel);
        Assert.Equal("Unable to check OpenAI Codex installation.", item.DetailText);
    }

    [Fact]
    public void SettingsViewModel_Disposal_UnregistersFromWidgetViewModel()
    {
        var settings = new AppSettings();
        var manager = new SettingsManager(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json"));
        using var widgetVm = new WidgetViewModel();

        var settingsVm = new SettingsViewModel(settings, manager, widgetVm);
        settingsVm.Dispose();

        // Trigger widget events and ensure no exceptions
        widgetVm.DockMode = AIQuotaBar.App.Layout.WidgetDockMode.Top;
        Assert.Equal(AIQuotaBar.App.Layout.WidgetDockMode.Floating, settings.DockMode); // Unchanged because disposed vm did not update settings
    }

    [Fact]
    public void SettingsViewModel_Disposal_DoesNotReceiveFurtherProviderPropertyUpdates()
    {
        var settings = new AppSettings();
        var manager = new SettingsManager(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json"));
        var provider = new StubUsageProvider("codex", "OpenAI Codex");
        var section = new ProviderSectionViewModel(provider, TimeSpan.FromSeconds(60), "Codex", ProviderDiscoveryStatus.Detected);
        using var widgetVm = new WidgetViewModel(new[] { section });

        var settingsVm = new SettingsViewModel(settings, manager, widgetVm);
        var codexItem = settingsVm.ProviderSetupItems[0];
        Assert.Equal("Detected", codexItem.StatusLabel);

        // While open, changing provider status updates setup item
        section.ApplySnapshot(new ProviderSnapshot("codex", "OpenAI Codex", ProviderStatus.Unauthenticated, statusMessage: "Requires login"));
        Assert.Equal("Sign-in required", codexItem.StatusLabel);

        // Dispose VM
        settingsVm.Dispose();

        // Further provider changes should not update the disposed VM's item
        section.ApplySnapshot(new ProviderSnapshot(
            "codex", "OpenAI Codex", ProviderStatus.Available,
            windows: new[] { new QuotaWindow("primary", "5-Hour", 90.0, TimeSpan.FromHours(5), null, QuotaWindowStatus.Active) }));

        Assert.Equal("Sign-in required", codexItem.StatusLabel); // Unchanged after disposal!
    }

    [Fact]
    public void ProviderCatalog_AllProviderSetupUris_PointToOfficialCliDocs()
    {
        Assert.Equal(new Uri("https://developers.openai.com/codex/cli/"), ProviderCatalog.Codex.SetupUri);
        Assert.Equal(new Uri("https://antigravity.google/docs/cli/install/"), ProviderCatalog.Antigravity.SetupUri);
        Assert.Equal(new Uri("https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview"), ProviderCatalog.ClaudeCode.SetupUri);
        Assert.Equal(new Uri("https://docs.x.ai/build/overview"), ProviderCatalog.GrokBuild.SetupUri);
        Assert.Equal(new Uri("https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/install-copilot-cli"), ProviderCatalog.GitHubCopilot.SetupUri);
    }

    [Theory]
    [InlineData("codex", "Sign in via the Codex CLI, then rescan.")]
    [InlineData("antigravity", "Sign in via the Antigravity CLI (agy), then rescan.")]
    [InlineData("claude-code", "Sign in via the Claude Code CLI, then rescan.")]
    [InlineData("grok-build", "Sign in via the Grok CLI, then rescan.")]
    [InlineData("github-copilot", "Sign in via the GitHub Copilot CLI, then rescan.")]
    public void ProviderSetupItem_SignRequiredState_ExplicitCliGuidance(string providerId, string expectedDetail)
    {
        var descriptor = ProviderCatalog.GetDescriptor(providerId)!;
        var section = new ProviderSectionViewModel(new StubUsageProvider(descriptor.Id, descriptor.DisplayName), TimeSpan.FromSeconds(60), descriptor.ShortDisplayName, ProviderDiscoveryStatus.Detected);
        section.ApplySnapshot(new ProviderSnapshot(descriptor.Id, descriptor.DisplayName, ProviderStatus.Unauthenticated));

        var item = new ProviderSetupItemViewModel(descriptor);
        item.UpdateStatus(ProviderDiscoveryStatus.Detected, section);

        Assert.Equal("Sign-in required", item.StatusLabel);
        Assert.Equal(expectedDetail, item.DetailText);
    }
}
