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

namespace XerahS.Mobile.UI.ViewModels;

/// <summary>
/// Converts boolean IsConfigured to a brush color (Green for configured, Orange for not)
/// </summary>
public class BoolToConfiguredBrushConverter : IValueConverter
{
    private static readonly ISolidColorBrush ConfiguredBrush = new SolidColorBrush(Colors.Green);
    private static readonly ISolidColorBrush NotConfiguredBrush = new SolidColorBrush(Colors.Orange);

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool isConfigured && isConfigured)
        {
            return ConfiguredBrush;
        }
        return NotConfiguredBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
