namespace AIQuotaBar.App.Settings;

public sealed class AppSettings
{
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WidgetWidth { get; set; }
    public bool IsAlwaysOnTop { get; set; } = true;
    public bool IsCompactMode { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
}
