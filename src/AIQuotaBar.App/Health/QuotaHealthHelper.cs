namespace AIQuotaBar.App.Health;

using System.Drawing;
using System.Windows.Media;

public static class QuotaHealthHelper
{
    public const double HealthyThreshold = 30.0;
    public const double WarningThreshold = 10.0;

    // Hex codes: Healthy: #10B981, Warning: #F59E0B, Critical: #EF4444, Neutral: #6B7280
    public static readonly System.Drawing.Color DrawingHealthy = System.Drawing.Color.FromArgb(16, 185, 129);
    public static readonly System.Drawing.Color DrawingWarning = System.Drawing.Color.FromArgb(245, 158, 11);
    public static readonly System.Drawing.Color DrawingCritical = System.Drawing.Color.FromArgb(239, 68, 68);
    public static readonly System.Drawing.Color DrawingNeutral = System.Drawing.Color.FromArgb(107, 114, 128);

    public static readonly System.Windows.Media.Color MediaHealthy = System.Windows.Media.Color.FromRgb(16, 185, 129);
    public static readonly System.Windows.Media.Color MediaWarning = System.Windows.Media.Color.FromRgb(245, 158, 11);
    public static readonly System.Windows.Media.Color MediaCritical = System.Windows.Media.Color.FromRgb(239, 68, 68);
    public static readonly System.Windows.Media.Color MediaNeutral = System.Windows.Media.Color.FromRgb(107, 114, 128);

    private static readonly SolidColorBrush HealthyBrush = new(MediaHealthy);
    private static readonly SolidColorBrush WarningBrush = new(MediaWarning);
    private static readonly SolidColorBrush CriticalBrush = new(MediaCritical);
    private static readonly SolidColorBrush NeutralBrush = new(MediaNeutral);

    static QuotaHealthHelper()
    {
        HealthyBrush.Freeze();
        WarningBrush.Freeze();
        CriticalBrush.Freeze();
        NeutralBrush.Freeze();
    }

    public static QuotaHealthLevel GetHealthLevel(double remainingPercent)
    {
        if (double.IsNaN(remainingPercent) || double.IsInfinity(remainingPercent))
        {
            return QuotaHealthLevel.Neutral;
        }

        if (remainingPercent > HealthyThreshold)
        {
            return QuotaHealthLevel.Healthy;
        }

        if (remainingPercent >= WarningThreshold)
        {
            return QuotaHealthLevel.Warning;
        }

        return QuotaHealthLevel.Critical;
    }

    public static System.Drawing.Color GetDrawingColor(QuotaHealthLevel level) => level switch
    {
        QuotaHealthLevel.Healthy => DrawingHealthy,
        QuotaHealthLevel.Warning => DrawingWarning,
        QuotaHealthLevel.Critical => DrawingCritical,
        _ => DrawingNeutral
    };

    public static System.Windows.Media.Color GetMediaColor(QuotaHealthLevel level) => level switch
    {
        QuotaHealthLevel.Healthy => MediaHealthy,
        QuotaHealthLevel.Warning => MediaWarning,
        QuotaHealthLevel.Critical => MediaCritical,
        _ => MediaNeutral
    };

    public static SolidColorBrush GetBrush(QuotaHealthLevel level) => level switch
    {
        QuotaHealthLevel.Healthy => HealthyBrush,
        QuotaHealthLevel.Warning => WarningBrush,
        QuotaHealthLevel.Critical => CriticalBrush,
        _ => NeutralBrush
    };
}
