using System.Globalization;

namespace ClinicApp.Converters;

// Active tab = white card background, inactive = muted green
public class BoolToTabBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
            return isActive ? Colors.White : Color.FromArgb("#E8D0DA");
        return Colors.White;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// Active tab label = primary green, inactive = gray
public class BoolToTabTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
            return isActive ? Color.FromArgb("#1A6B2F") : Color.FromArgb("#6B7280");
        return Color.FromArgb("#6B7280");
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// Active tab label = Bold, inactive = None
public class BoolToFontAttrConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
            return isActive ? FontAttributes.Bold : FontAttributes.None;
        return FontAttributes.None;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
