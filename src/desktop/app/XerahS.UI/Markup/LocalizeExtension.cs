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

using Avalonia.Data;
using Avalonia.Markup.Xaml;
using XerahS.Common;

namespace XerahS.UI.Markup
{
    /// <summary>
    /// XAML markup extension that resolves a localized string by key.
    /// Usage: Text="{local:Localize Some_Key}"
    /// </summary>
    public sealed class LocalizeExtension : MarkupExtension
    {
        public LocalizeExtension()
        {
        }

        public LocalizeExtension(string key)
        {
            Key = key;
        }

        public string Key { get; set; } = string.Empty;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
            {
                return string.Empty;
            }

            // A live binding instead of a fixed string: the UI updates as soon as the culture
            // changes (LocalizationStrings raises PropertyChanged), no restart required.
            return new Binding($"[{Key}]")
            {
                Source = LocalizationStrings.Instance,
                Mode = BindingMode.OneWay
            };
        }
    }
}
