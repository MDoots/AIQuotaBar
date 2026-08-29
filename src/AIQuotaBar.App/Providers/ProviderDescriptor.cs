namespace AIQuotaBar.App.Providers;

using AIQuotaBar.Core.Interfaces;

public sealed class ProviderDescriptor
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string ShortDisplayName { get; init; }
    public required TimeSpan RefreshInterval { get; init; }
    public string AccentColor { get; init; } = "#10B981";
    public required Func<IUsageProvider> CreateProvider { get; init; }
    public required Func<string?> LocateExecutable { get; init; }
    public required Uri SetupUri { get; init; }
    public required IReadOnlyList<KnownQuotaWindowDescriptor> KnownQuotaWindows { get; init; }
}
