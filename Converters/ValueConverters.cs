using System.Globalization;

namespace PAGELY.Converters;

public class HasValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => ConverterHelpers.HasPath(value);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class InverseHasValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !ConverterHelpers.HasPath(value);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class PercentToProgressConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double d ? Math.Clamp(d / 100.0, 0, 1) : 0.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

file static class ConverterHelpers
{
    public static bool HasPath(object? value)
        => value is string s && !string.IsNullOrWhiteSpace(s) && File.Exists(s);
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StatusColorConverter : IValueConverter
{
    // Mirrors the Status* colours in Resources/Styles/Colors.xaml, darkened so the
    // text stays legible on the SurfaceAlt chip background.
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var label = value?.ToString() ?? "";
        return label switch
        {
            "Currently Reading" => Color.FromArgb("#8A6A38"),
            "Completed" => Color.FromArgb("#4E6A56"),
            "Dropped" => Color.FromArgb("#8A5449"),
            _ => Color.FromArgb("#5E7A8C")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
