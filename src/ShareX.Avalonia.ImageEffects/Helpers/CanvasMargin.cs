#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using ShareX.Avalonia.Common;
using System;
using System.ComponentModel;
using System.Globalization;

namespace ShareX.Avalonia.ImageEffects.Helpers
{
    [TypeConverter(typeof(CanvasMarginConverter))]
    public struct CanvasMargin
    {
        public int Left { get; set; }

        public int Top { get; set; }

        public int Right { get; set; }

        public int Bottom { get; set; }

        public CanvasMargin(int all)
            : this(all, all, all, all)
        {
        }

        public CanvasMargin(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Horizontal => Left + Right;

        public int Vertical => Top + Bottom;

        public int All => Left == Top && Left == Right && Left == Bottom ? Left : -1;

        public override string ToString()
        {
            return $"{Left}, {Top}, {Right}, {Bottom}";
        }
    }

    public class CanvasMarginConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string text)
            {
                string[] parts = text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                int[] numbers = Array.ConvertAll(parts, p => int.Parse(p.Trim(), CultureInfo.InvariantCulture));

                if (numbers.Length == 1)
                {
                    return new CanvasMargin(numbers[0]);
                }

                if (numbers.Length == 4)
                {
                    return new CanvasMargin(numbers[0], numbers[1], numbers[2], numbers[3]);
                }
            }

            return base.ConvertFrom(context, culture, value);
        }

        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is CanvasMargin margin)
            {
                return $"{margin.Left}, {margin.Top}, {margin.Right}, {margin.Bottom}";
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
