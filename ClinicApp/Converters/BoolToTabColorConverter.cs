using System.Globalization;

namespace ClinicApp.Converters;

// Returns a highlight color when the tab is active (true), muted color when inactive (false)
public class BoolToTabColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
            return Colors.MediumPurple;   // active tab color
        return Colors.Gray;               // inactive tab color
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
