using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace ShareX.Avalonia.UI.Converters
{
    /// <summary>
    /// Converts a boolean to an active/inactive background brush
    /// </summary>
    public class BoolToActiveBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
            {
                return new SolidColorBrush(Color.Parse("#8B5CF6")); // Active purple
            }
            return new SolidColorBrush(Color.Parse("#25263A")); // Inactive dark slate
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
