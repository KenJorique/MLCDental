using System.Globalization;

namespace ClinicApp.Converters;

// Returns a highlight color when the tab is active (true), muted color when inactive (false)
public class BoolToTabColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
            return Color.FromArgb("#2563EB");   // active tab — matches ClinicApp accent
        return Color.FromArgb("#E5E7EB");       // inactive tab — light gray, matches card borders
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}