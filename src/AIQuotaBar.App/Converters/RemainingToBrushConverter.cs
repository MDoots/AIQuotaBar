namespace AIQuotaBar.App.Converters;

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

public sealed class RemainingToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush HealthyBrush = new(Color.FromRgb(16, 185, 129)); // #10B981
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(245, 158, 11)); // #F59E0B
    private static readonly SolidColorBrush CriticalBrush = new(Color.FromRgb(239, 68, 68)); // #EF4444
    private static readonly SolidColorBrush NeutralBrush = new(Color.FromRgb(107, 114, 128)); // #6B7280

    static RemainingToBrushConverter()
    {
        HealthyBrush.Freeze();
        WarningBrush.Freeze();
        CriticalBrush.Freeze();
        NeutralBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int remaining)
        {
            if (remaining > 30)
            {
                return HealthyBrush;
            }
            if (remaining >= 10)
            {
                return WarningBrush;
            }
            return CriticalBrush;
        }

        return NeutralBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
