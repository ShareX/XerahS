using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ShareX.Avalonia.UI.ViewModels;

namespace ShareX.Avalonia.UI.Converters
{
    /// <summary>
    /// Converts EditorTool comparison to background color for tool button highlighting
    /// </summary>
    public class ActiveToolToBackgroundConverter : IValueConverter
    {
        public static readonly ActiveToolToBackgroundConverter Instance = new();

        // Active tool button color (violet gradient)
        private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#8B5CF6"));
        
        // Inactive tool button color (gray)
        private static readonly IBrush InactiveBrush = new SolidColorBrush(Color.Parse("#374151"));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is EditorTool activeTool && parameter is string toolName)
            {
                if (Enum.TryParse<EditorTool>(toolName, out var tool))
                {
                    return activeTool == tool ? ActiveBrush : InactiveBrush;
                }
            }
            return InactiveBrush;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts selected color comparison to border brush for color swatch highlighting
    /// </summary>
    public class SelectedColorToBorderConverter : IValueConverter
    {
        public static readonly SelectedColorToBorderConverter Instance = new();

        // Selected ring color
        private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));
        private static readonly IBrush UnselectedBrush = new SolidColorBrush(Colors.Transparent);

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string selectedColor && parameter is string swatchColor)
            {
                return string.Equals(selectedColor, swatchColor, StringComparison.OrdinalIgnoreCase) 
                    ? SelectedBrush 
                    : UnselectedBrush;
            }
            return UnselectedBrush;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts stroke width comparison to background for width button highlighting
    /// </summary>
    public class SelectedWidthToBackgroundConverter : IValueConverter
    {
        public static readonly SelectedWidthToBackgroundConverter Instance = new();

        private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#8B5CF6"));
        private static readonly IBrush UnselectedBrush = new SolidColorBrush(Color.Parse("#FFFFFF0D"));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int selectedWidth && parameter is string widthStr && int.TryParse(widthStr, out int width))
            {
                return selectedWidth == width ? SelectedBrush : UnselectedBrush;
            }
            return UnselectedBrush;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
