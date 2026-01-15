using Avalonia.Data.Converters;
using XerahS.Common;
using System.Globalization;

namespace XerahS.UI.Converters;

public class EnumToDescriptionConverter : IValueConverter
{
    public static readonly EnumToDescriptionConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Enum e)
        {
            return e.GetLocalizedDescription();
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
