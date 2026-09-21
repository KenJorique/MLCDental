using System.Globalization;

namespace ClinicApp.Converters;

// Returns false (hides the element) when the bound number is exactly 0; true otherwise. Used to hide the ₱0 labels on the Billing chart.
public class ZeroAmountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d) return d != 0;
        if (value is decimal m) return m != 0;
        if (value is int i) return i != 0;
        return true; // unknown type — fail open, don't hide anything unexpectedly
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
