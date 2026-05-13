using System.Globalization;

namespace ClinicApp.Converters;

// Returns a highlight color when the tab is active (true), muted color when inactive (false)
public class BoolToGenderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isFemale && isFemale)
            return "Female";
        return "Male";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string gender && gender == "Female";
    }
}
