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

namespace XerahS.Platform.Abstractions
{
    /// <summary>
    /// Service for detecting system theme preferences (dark/light mode)
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// Returns true if the system prefers dark mode
        /// </summary>
        bool IsDarkModePreferred { get; }

        /// <summary>
        /// Returns true if the system prefers light mode (explicitly set, not just "no preference")
        /// </summary>
        bool IsLightModePreferred { get; }

        /// <summary>
        /// Returns the raw color scheme value from the system
        /// 0 = no preference, 1 = dark, 2 = light
        /// </summary>
        int ColorScheme { get; }

        /// <summary>
        /// Event fired when the system theme changes
        /// </summary>
        event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        /// <summary>
        /// Starts monitoring for theme changes. Call this after UI is initialized.
        /// </summary>
        void StartMonitoring();

        /// <summary>
        /// Stops monitoring for theme changes
        /// </summary>
        void StopMonitoring();
    }

    /// <summary>
    /// Event args for theme change events
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// True if the new theme is dark mode
        /// </summary>
        public bool IsDarkMode { get; }

        /// <summary>
        /// The raw color scheme value (0 = no preference, 1 = dark, 2 = light)
        /// </summary>
        public int ColorScheme { get; }

        public ThemeChangedEventArgs(bool isDarkMode, int colorScheme)
        {
            IsDarkMode = isDarkMode;
            ColorScheme = colorScheme;
        }
    }
}
