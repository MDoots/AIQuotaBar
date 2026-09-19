using System.Diagnostics;
using System.Text.Json;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;
using AIQuotaBar.Providers.Antigravity;
using AIQuotaBar.Providers.ClaudeCode;
using AIQuotaBar.Providers.Codex;
using AIQuotaBar.Providers.GitHubCopilot;
using AIQuotaBar.Providers.GrokBuild;

// Developer diagnostic only. Uses the same providers as the widget, without
// starting a model session or printing account, path, raw output or plan data.
if (args.Length != 2 || !int.TryParse(args[1], out var seconds) || seconds is < 1 or > 30)
    return 2;
var timeout = TimeSpan.FromSeconds(seconds);
IUsageProvider? provider = args[0] switch
{
    "codex" => new CodexUsageProvider(defaultTimeout: timeout),
    "antigravity" => new AntigravityUsageProvider(defaultTimeout: timeout),
    "claude-code" => new ClaudeCodeUsageProvider(defaultTimeout: timeout),
    "grok-build" => new GrokBuildUsageProvider(defaultTimeout: timeout),
    "github-copilot" => new GitHubCopilotUsageProvider(defaultTimeout: timeout),
    _ => null
};
if (provider == null) return 2;
var watch = Stopwatch.StartNew();
ProviderSnapshot snapshot;
try { snapshot = await provider.GetUsageAsync(); }
catch { snapshot = new ProviderSnapshot(provider.Id, provider.DisplayName, ProviderStatus.Error); }
Console.WriteLine(JsonSerializer.Serialize(new
{
    Provider = provider.Id,
    Status = snapshot.Status.ToString(),
    WindowCount = snapshot.Windows.Count,
    FiniteWindowCount = snapshot.Windows.Count(w => double.IsFinite(w.RemainingPercent)),
    ObservedAtUtc = snapshot.Timestamp,
    DurationMilliseconds = watch.ElapsedMilliseconds,
    CliVersion = (string?)null,
    VersionCheck = "Not queried",
    QuotaObserved = snapshot.Status == ProviderStatus.Available && snapshot.Windows.Count > 0
}));
return snapshot.Status is ProviderStatus.Error or ProviderStatus.Timeout ? 1 : 0;
