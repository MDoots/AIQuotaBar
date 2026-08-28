namespace AIQuotaBar.App.Tray;

using AIQuotaBar.App.Health;
using AIQuotaBar.App.ViewModels;

public static class TrayHealthCalculator
{
    public const int MaxTooltipLength = 63;

    public static TrayHealthState Calculate(IEnumerable<ProviderSectionViewModel>? providers)
    {
        if (providers == null)
        {
            return CreateEmptyState(hasVisibleProviders: false, isWaitingForData: false);
        }

        var providerList = providers.ToList();
        var visibleProviders = providerList.Where(p => p.IsVisibleByPreference).ToList();

        if (visibleProviders.Count == 0)
        {
            return CreateEmptyState(hasVisibleProviders: false, isWaitingForData: false);
        }

        // Check if any visible providers have loaded windows
        var hasAnyLoadedWindows = visibleProviders.Any(p => p.AllWindows.Count > 0);
        var visibleWindows = visibleProviders
            .SelectMany(p => p.VisibleWindows.Select(w => (Provider: p, Window: w)))
            .ToList();

        if (visibleWindows.Count == 0)
        {
            // If providers have loaded windows but none are visible, user deselected all rows.
            // If providers have not loaded windows yet (e.g. startup / loading), we are waiting for data.
            var isWaiting = !hasAnyLoadedWindows;
            return CreateEmptyState(hasVisibleProviders: true, isWaitingForData: isWaiting);
        }

        // Filter for valid quota values
        var validCandidates = visibleWindows
            .Where(vw => IsValidPercentage(vw.Window.RemainingPercent))
            .ToList();

        if (validCandidates.Count == 0)
        {
            return CreateEmptyState(hasVisibleProviders: true, isWaitingForData: true);
        }

        // Determine the lowest RemainingPercent using actual double precision
        var lowest = validCandidates
            .OrderBy(vw => vw.Window.RemainingPercent)
            .ThenBy(vw => vw.Provider.ProviderName)
            .ThenBy(vw => vw.Window.DisplayName)
            .First();

        var lowestPercent = lowest.Window.RemainingPercent;
        var healthLevel = QuotaHealthHelper.GetHealthLevel(lowestPercent);
        var roundedPercent = (int)Math.Round(lowestPercent, MidpointRounding.AwayFromZero);

        var rowLabel = FormatRowLabel(lowest.Provider.ProviderName, lowest.Window.RawDisplayName);

        var tooltip = SafeTruncate($"AIQuotaBar — {roundedPercent}% · {rowLabel}", MaxTooltipLength);
        var menuStatus = $"Lowest quota: {roundedPercent}% — {rowLabel}";

        return new TrayHealthState(
            HealthLevel: healthLevel,
            LowestRemainingPercent: lowestPercent,
            ProviderName: lowest.Provider.ProviderName,
            WindowName: lowest.Window.RawDisplayName,
            HasVisibleQuotaData: true,
            HasVisibleProviders: true,
            TooltipText: tooltip,
            StatusMenuText: menuStatus);
    }

    public static string FormatRowLabel(string? providerName, string? windowName)
    {
        var pName = providerName?.Trim() ?? string.Empty;
        var wName = windowName?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(pName) && string.IsNullOrEmpty(wName))
        {
            return "Quota";
        }

        if (string.IsNullOrEmpty(pName))
        {
            return wName;
        }

        if (string.IsNullOrEmpty(wName))
        {
            return pName;
        }

        // Normalize provider name for concise display (e.g., "OpenAI Codex" -> "Codex")
        var conciseProvider = pName;
        if (pName.StartsWith("OpenAI ", StringComparison.OrdinalIgnoreCase))
        {
            conciseProvider = pName["OpenAI ".Length..].Trim();
        }
        else if (pName.StartsWith("Google ", StringComparison.OrdinalIgnoreCase))
        {
            conciseProvider = pName["Google ".Length..].Trim();
        }

        // If the window name already includes concise provider name or a dot separator like "Gemini · Weekly", format cleanly
        if (wName.Contains('·'))
        {
            // e.g. "Gemini · Weekly" -> "Gemini Weekly"
            var parts = wName.Split('·', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return string.Join(" ", parts);
        }

        if (wName.StartsWith(conciseProvider, StringComparison.OrdinalIgnoreCase))
        {
            return wName;
        }

        return $"{conciseProvider} {wName}";
    }

    public static string SafeTruncate(string? text, int maxLength = MaxTooltipLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "AIQuotaBar";
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        if (maxLength <= 3)
        {
            return text[..maxLength];
        }

        return string.Concat(text.AsSpan(0, maxLength - 3), "...");
    }

    private static bool IsValidPercentage(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static TrayHealthState CreateEmptyState(bool hasVisibleProviders, bool isWaitingForData)
    {
        var tooltip = isWaitingForData
            ? SafeTruncate("AIQuotaBar — Waiting for quota data")
            : SafeTruncate("AIQuotaBar — No quota rows selected");

        var status = isWaitingForData
            ? "Waiting for quota data"
            : "No quota rows selected";

        return new TrayHealthState(
            HealthLevel: QuotaHealthLevel.Neutral,
            LowestRemainingPercent: null,
            ProviderName: null,
            WindowName: null,
            HasVisibleQuotaData: false,
            HasVisibleProviders: hasVisibleProviders,
            TooltipText: tooltip,
            StatusMenuText: status);
    }
}
