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

using System.Globalization;
using System.Resources;

namespace XerahS.Common
{
    public static class Resources
    {
        private static readonly ResourceManager resourceManager = new ResourceManager("XerahS.Common.Resources", typeof(Resources).Assembly);

        public static ResourceManager ResourceManager => resourceManager;

        /// <summary>
        /// Returns a localized string for the current UI culture, falling back to the
        /// invariant (English) resource when no localized value is available.
        /// </summary>
        public static string GetString(string key)
        {
            return resourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }

        /// <summary>
        /// Returns a formatted localized string using the current UI culture.
        /// </summary>
        public static string GetString(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentUICulture, GetString(key), args);
        }
    }
}
