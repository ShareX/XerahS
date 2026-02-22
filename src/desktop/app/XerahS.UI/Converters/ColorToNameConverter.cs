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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Avalonia.Data.Converters;
using XerahS.Common;
using XerahS.UI.Helpers;

namespace XerahS.UI.Converters
{
    public sealed class ColorToNameConverter : IValueConverter
    {
        private static readonly Dictionary<string, string> StandardColorNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["000000"] = "Black",
            ["404040"] = "Dark Gray",
            ["FF0000"] = "Red",
            ["FF6A00"] = "Orange",
            ["FFD800"] = "Yellow",
            ["B6FF00"] = "Lime",
            ["4CFF00"] = "Green",
            ["00FF21"] = "Bright Green",
            ["00FF90"] = "Spring Green",
            ["00FFFF"] = "Cyan",
            ["0094FF"] = "Sky Blue",
            ["0026FF"] = "Blue",
            ["4800FF"] = "Indigo",
            ["B200FF"] = "Violet",
            ["FF00DC"] = "Magenta",
            ["FF006E"] = "Hot Pink",
            ["FFFFFF"] = "White",
            ["808080"] = "Gray",
            ["7F0000"] = "Dark Red",
            ["7F3300"] = "Brown",
            ["7F6A00"] = "Olive",
            ["5B7F00"] = "Olive Green",
            ["267F00"] = "Dark Green",
            ["007F0E"] = "Forest Green",
            ["007F46"] = "Teal",
            ["007F7F"] = "Dark Cyan",
            ["004A7F"] = "Steel Blue",
            ["00137F"] = "Navy",
            ["21007F"] = "Deep Blue",
            ["57007F"] = "Deep Purple",
            ["7F006E"] = "Plum",
            ["7F0037"] = "Deep Pink"
        };

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Avalonia.Media.Color color)
            {
                return string.Empty;
            }

            var drawing = ColorConversion.ToDrawingColor(color);
            if (drawing.IsNamedColor && !string.IsNullOrEmpty(drawing.Name))
            {
                return drawing.Name;
            }

            var hex = ColorHelpers.ColorToHex(drawing);
            if (StandardColorNames.TryGetValue(hex, out var name))
            {
                return name;
            }

            return "Custom color";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Avalonia.Data.BindingOperations.DoNothing;
        }
    }
}
