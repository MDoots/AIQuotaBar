namespace AIQuotaBar.App.Layout;

public enum WidgetLayoutMode
{
    Micro,
    Minimal,
    Compact,
    Full
}

public static class ResponsiveLayoutHelper
{
    public const double MinWidgetWidth = 180.0;
    public const double MaxWidgetWidth = 1200.0;
    public const double DefaultWidgetWidth = 330.0;

    // Breakpoint thresholds for client widget width (excluding window shadow margins)
    public const double FullBreakpoint = 420.0;
    public const double CompactBreakpoint = 300.0;
    public const double MinimalBreakpoint = 240.0;
    public const double MicroBreakpoint = 180.0;

    public static WidgetLayoutMode GetLayoutMode(double width)
    {
        if (double.IsNaN(width) || double.IsInfinity(width))
        {
            return WidgetLayoutMode.Compact;
        }

        if (width >= FullBreakpoint)
        {
            return WidgetLayoutMode.Full;
        }

        if (width >= CompactBreakpoint)
        {
            return WidgetLayoutMode.Compact;
        }

        if (width >= MinimalBreakpoint)
        {
            return WidgetLayoutMode.Minimal;
        }

        return WidgetLayoutMode.Micro;
    }

    public static double ClampWidth(double? width)
    {
        if (!width.HasValue || double.IsNaN(width.Value) || double.IsInfinity(width.Value))
        {
            return DefaultWidgetWidth;
        }

        return Math.Clamp(width.Value, MinWidgetWidth, MaxWidgetWidth);
    }
}
