namespace AIQuotaBar.App.Converters;

using System.Globalization;
using System.Windows.Data;
using AIQuotaBar.App.Health;

public sealed class RemainingToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IConvertible convertible)
        {
            try
            {
                var remaining = convertible.ToDouble(culture ?? CultureInfo.InvariantCulture);
                var level = QuotaHealthHelper.GetHealthLevel(remaining);
                return QuotaHealthHelper.GetBrush(level);
            }
            catch
            {
                // Fallback to neutral brush on conversion failure
            }
        }

        return QuotaHealthHelper.GetBrush(QuotaHealthLevel.Neutral);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
