#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)
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
