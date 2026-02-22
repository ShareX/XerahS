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
using Avalonia.Platform; // Added this line
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
                    var targetTheme = useDarkMode ? ShareX.ImageEditor.Helpers.ThemeManager.ShareXDark : ShareX.ImageEditor.Helpers.ThemeManager.ShareXLight;
                    Application.Current.RequestedThemeVariant = targetTheme;
                    
                    // Also update the ImageEditor's ThemeManager so windows subscribed to it get the update
                    ShareX.ImageEditor.Helpers.ThemeManager.SetTheme(targetTheme);
                    
                    DebugHelper.WriteLine($"Applied theme mode: {mode} (Dark: {useDarkMode}) -> {targetTheme.Key}");
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
            if (mode == AppThemeMode.Dark) return true;
            if (mode == AppThemeMode.Light) return false;
            
            // For System mode
            return GetSystemPrefersDarkMode();
        }

        /// <summary>
        /// Gets the system's dark mode preference
        /// </summary>
        public static bool GetSystemPrefersDarkMode()
        {
            try
            {
                // 1. Try PlatformServices header (Windows/MacOS native checks)
                if (PlatformServices.IsThemeServiceInitialized)
                {
                    return PlatformServices.Theme.IsDarkModePreferred;
                }

                // 2. Fallback: Use Avalonia's built-in platform theme variant detection
                // This is often more reliable on recent Avalonia versions for system theme
                var platformTheme = Application.Current?.PlatformSettings?.GetColorValues();
                if (platformTheme != null)
                {
                    return platformTheme.ThemeVariant == PlatformThemeVariant.Dark;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to get system dark mode preference");
            }

            // Default fallback
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
                
                // Initialize monitoring if supported
                if (PlatformServices.IsThemeServiceInitialized)
                {
                    // Unsubscribe first to avoid double subscription on re-init
                    PlatformServices.Theme.ThemeChanged -= OnSystemThemeChanged;
                    PlatformServices.Theme.ThemeChanged += OnSystemThemeChanged;
                    PlatformServices.Theme.StartMonitoring();
                }
                
                // Also subscribe to Avalonia's platform color values change for redundancy/cross-platform support
                if (Application.Current?.PlatformSettings != null)
                {
                    Application.Current.PlatformSettings.ColorValuesChanged -= OnPlatformColorValuesChanged;
                    Application.Current.PlatformSettings.ColorValuesChanged += OnPlatformColorValuesChanged;
                }

                ApplyTheme(themeMode);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to initialize theme service");
            }
        }
        
        private static void OnPlatformColorValuesChanged(object? sender, PlatformColorValues e)
        {
            // Similar to OnSystemThemeChanged, but triggered by Avalonia
            var currentMode = SettingsManager.Settings?.ThemeMode ?? AppThemeMode.System;
            if (currentMode != AppThemeMode.System)
            {
                return;
            }
            
            bool isDark = e.ThemeVariant == PlatformThemeVariant.Dark;
            UpdateSystemTheme(isDark);
        }

        private static void OnSystemThemeChanged(object? sender, ThemeChangedEventArgs e)
        {
            // Only respond to system theme changes if we're in System mode
            var currentMode = SettingsManager.Settings?.ThemeMode ?? AppThemeMode.System;
            if (currentMode != AppThemeMode.System)
            {
                return;
            }

            UpdateSystemTheme(e.IsDarkMode);
        }
        
        private static void UpdateSystemTheme(bool isDark)
        {
            try
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (Application.Current != null)
                    {
                        var targetTheme = isDark ? ShareX.ImageEditor.Helpers.ThemeManager.ShareXDark : ShareX.ImageEditor.Helpers.ThemeManager.ShareXLight;
                        Application.Current.RequestedThemeVariant = targetTheme;
                        
                        // Sync ImageEditor theme manager
                        ShareX.ImageEditor.Helpers.ThemeManager.SetTheme(targetTheme);
                        
                        DebugHelper.WriteLine($"System theme changed, applied: {(isDark ? "Dark" : "Light")}");
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
