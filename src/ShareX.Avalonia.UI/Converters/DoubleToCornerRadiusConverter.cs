using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ShareX.Ava.UI.Converters
{
    public class DoubleToCornerRadiusConverter : IValueConverter
    {
        public static readonly DoubleToCornerRadiusConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double radius && !double.IsNaN(radius) && !double.IsInfinity(radius))
            {
                var clamped = Math.Max(0, radius);
                return new CornerRadius(clamped);
            }

            return new CornerRadius(0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is CornerRadius cornerRadius)
            {
                return cornerRadius.TopLeft;
            }

            return 0d;
        }
    }
}
