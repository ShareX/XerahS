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

using Avalonia;
using Avalonia.Styling;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.Services
{
    /// <summary>
    /// Service for managing application theme based on user preference
    /// </summary>
    public static class ThemeService
    {
        /// <summary>
        /// Applies the specified theme mode to the application
        /// </summary>
        public static void ApplyTheme(AppThemeMode mode)
        {
            try
            {
                bool useDarkMode = ShouldUseDarkMode(mode);

                if (Application.Current != null)
                {
                    Application.Current.RequestedThemeVariant = useDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
                    DebugHelper.WriteLine($"Applied theme mode: {mode} (Dark: {useDarkMode})");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to apply theme");
            }
        }

        /// <summary>
        /// Determines if dark mode should be used based on the theme mode setting
        /// </summary>
        public static bool ShouldUseDarkMode(AppThemeMode mode)
        {
            return mode switch
            {
                AppThemeMode.Dark => true,
                AppThemeMode.Light => false,
                AppThemeMode.System => GetSystemPrefersDarkMode(),
                _ => GetSystemPrefersDarkMode()
            };
        }

        /// <summary>
        /// Gets the system's dark mode preference
        /// </summary>
        public static bool GetSystemPrefersDarkMode()
        {
            try
            {
                if (PlatformServices.IsThemeServiceInitialized)
                {
                    return PlatformServices.Theme.IsDarkModePreferred;
                }

                // Fallback: check Avalonia's actual theme variant
                if (Application.Current?.ActualThemeVariant != null)
                {
                    return Application.Current.ActualThemeVariant == ThemeVariant.Dark;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to get system dark mode preference");
            }

            // Default to dark mode
            return true;
        }

        /// <summary>
        /// Initializes the theme service and applies the saved theme preference
        /// </summary>
        public static void Initialize()
        {
            try
            {
                var themeMode = SettingsManager.Settings?.ThemeMode ?? AppThemeMode.System;
                ApplyTheme(themeMode);

                // Subscribe to system theme changes if using System mode
                if (themeMode == AppThemeMode.System && PlatformServices.IsThemeServiceInitialized)
                {
                    PlatformServices.Theme.ThemeChanged += OnSystemThemeChanged;
                    PlatformServices.Theme.StartMonitoring();
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to initialize theme service");
            }
        }

        private static void OnSystemThemeChanged(object? sender, ThemeChangedEventArgs e)
        {
            // Only respond to system theme changes if we're in System mode
            var currentMode = SettingsManager.Settings?.ThemeMode ?? AppThemeMode.System;
            if (currentMode != AppThemeMode.System)
            {
                return;
            }

            try
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (Application.Current != null)
                    {
                        Application.Current.RequestedThemeVariant = e.IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
                        DebugHelper.WriteLine($"System theme changed, applied: {(e.IsDarkMode ? "Dark" : "Light")}");
                    }
                });
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to apply system theme change");
            }
        }
    }
}
