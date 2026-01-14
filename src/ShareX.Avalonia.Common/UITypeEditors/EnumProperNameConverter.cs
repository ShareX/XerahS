#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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

using System;
using System.ComponentModel;
using System.Globalization;

namespace XerahS.Common
{
    public class EnumProperNameConverter : EnumConverter
    {
        private readonly Type enumType;

        public EnumProperNameConverter(Type type) : base(type)
        {
            enumType = type;
        }

        public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destType)
        {
            if (destType == typeof(string))
            {
                return true;
            }

            if (destType is null)
            {
                return false;
            }

            return base.CanConvertTo(context, destType);
        }

        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destType)
        {
            if (destType == typeof(string))
            {
                if (value is Enum enumValue)
                {
                    return GeneralHelpers.GetProperName(enumValue.ToString());
                }
            }

            return base.ConvertTo(context, culture, value, destType);
        }

        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type? srcType)
        {
            if (srcType == typeof(string))
            {
                return true;
            }

            if (srcType is null)
            {
                return false;
            }

            return base.CanConvertFrom(context, srcType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object? value)
        {
            if (value is string stringValue)
            {
                foreach (Enum e in Enum.GetValues(enumType).OfType<Enum>())
                {
                    if (GeneralHelpers.GetProperName(e.ToString()) == stringValue)
                    {
                        return e;
                    }
                }

                return Enum.Parse(enumType, stringValue);
            }

            return base.ConvertFrom(context, culture, value ?? throw new ArgumentNullException(nameof(value)));
        }
    }
}




