namespace AIQuotaBar.App.Settings;

using AIQuotaBar.App.Layout;

public sealed class AppSettings
{
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WidgetWidth { get; set; }
    public bool IsAlwaysOnTop { get; set; } = true;
    public bool IsCompactMode { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public bool LowQuotaNotificationsEnabled { get; set; } = true;
    public WidgetDockMode DockMode { get; set; } = WidgetDockMode.Floating;
    public double DockedHorizontalAnchor { get; set; } = 0.5;
    public bool AutoHideDockedBar { get; set; } = true;

    public Dictionary<string, bool> ProviderVisibility { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> QuotaWindowVisibility { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void NormalizeVisibilityDictionaries()
    {
        if (!Enum.IsDefined(typeof(WidgetDockMode), DockMode))
        {
            DockMode = WidgetDockMode.Floating;
        }

        if (double.IsNaN(DockedHorizontalAnchor) || double.IsInfinity(DockedHorizontalAnchor))
        {
            DockedHorizontalAnchor = 0.5;
        }
        else
        {
            DockedHorizontalAnchor = Math.Clamp(DockedHorizontalAnchor, 0.0, 1.0);
        }

        ProviderVisibility = new Dictionary<string, bool>(
            ProviderVisibility ?? Enumerable.Empty<KeyValuePair<string, bool>>(),
            StringComparer.OrdinalIgnoreCase);

        QuotaWindowVisibility = new Dictionary<string, bool>(
            QuotaWindowVisibility ?? Enumerable.Empty<KeyValuePair<string, bool>>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool IsProviderVisible(string? providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return true;
        }

        if (ProviderVisibility != null && ProviderVisibility.TryGetValue(providerId, out var isVisible))
        {
            return isVisible;
        }

        return true;
    }

    public bool IsQuotaWindowVisible(string? providerId, string? windowId)
    {
        if (string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(windowId))
        {
            return true;
        }

        var key = $"{providerId}:{windowId}";
        if (QuotaWindowVisibility != null && QuotaWindowVisibility.TryGetValue(key, out var isVisible))
        {
            return isVisible;
        }

        return true;
    }

    public void SetProviderVisible(string providerId, bool isVisible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ProviderVisibility ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        ProviderVisibility[providerId] = isVisible;
    }

    public void SetQuotaWindowVisible(string providerId, string windowId, bool isVisible)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(windowId);
        QuotaWindowVisibility ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        QuotaWindowVisibility[$"{providerId}:{windowId}"] = isVisible;
    }

    public void ResetVisibilityDefaults()
    {
        ProviderVisibility?.Clear();
        QuotaWindowVisibility?.Clear();
    }
}
