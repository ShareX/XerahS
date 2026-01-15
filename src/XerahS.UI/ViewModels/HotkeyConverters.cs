using Avalonia.Data.Converters;
using Avalonia.Media;
using XerahS.Platform.Abstractions;
using System.Globalization;

namespace XerahS.UI.ViewModels;

/// <summary>
/// Converts HotkeyStatus to a status color (green=registered, yellow=not configured, red=failed)
/// </summary>
public class HotkeyStatusColorConverter : IValueConverter
{
    public static readonly HotkeyStatusColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is HotkeyStatus status)
        {
            return status switch
            {
                HotkeyStatus.Registered => new SolidColorBrush(Colors.LimeGreen),
                HotkeyStatus.Failed => new SolidColorBrush(Colors.Red),
                HotkeyStatus.NotConfigured => new SolidColorBrush(Colors.Orange),
                HotkeyStatus.Recording => new SolidColorBrush(Colors.Yellow),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool to FontWeight (Bold if true, Normal if false)
/// </summary>
public class BoolToFontWeightConverter : IValueConverter
{
    public static readonly BoolToFontWeightConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            return FontWeight.Bold;
        }
        return FontWeight.Normal;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
