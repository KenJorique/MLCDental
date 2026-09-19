using System.Globalization;

namespace ClinicApp.Converters;

public class PasswordToggleIconConverter : IValueConverter
{
    // Returns the "visibility" (show) glyph when hidden, "visibility_off" (hide) glyph when shown.
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isHidden = value is bool b && b;
        return isHidden ? "\ue8f4" : "\ue8f5";
    }

    // One-way binding only; reverse conversion isn't needed for an icon glyph.
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
