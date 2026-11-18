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
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Presentation.ViewModels;
using ShareX.ImageEditor.Presentation.Views;
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
        private const double AccentForegroundDarkSwitchRatio = 1.75;

        public static ImageEditorOptions CreateImageEditorOptions(bool showExitConfirmation = true)
        {
            var options = new ImageEditorOptions
            {
                ShowExitConfirmation = showExitConfirmation
            };

            ApplyImageEditorThemeOptions(options, SettingsManager.Settings?.ThemeMode ?? AppThemeMode.System);
            return options;
        }

        /// <summary>
        /// Editor options that remember the user's tool settings (colours, sizes, fonts...) between
        /// editor sessions, like ShareX's AnnotationOptions in its default task settings. The editor
        /// changes this object in place; call <see cref="PersistImageEditorOptionsAsync"/> when it
        /// closes. Theme fields still follow the app theme.
        /// </summary>
        public static ImageEditorOptions GetPersistedImageEditorOptions(bool showExitConfirmation = true)
        {
            ImageEditorOptions options = RegionCaptureAnnotationOptionsStore.GetEditorOptions();
            options.ShowExitConfirmation = showExitConfirmation;
            ApplyImageEditorThemeOptions(options, SettingsManager.Settings?.ThemeMode ?? AppThemeMode.System);
            return options;
        }

        public static async Task PersistImageEditorOptionsAsync()
        {
            try
            {
                bool saved = await RegionCaptureAnnotationOptionsStore.PersistAsync();
                DebugHelper.WriteLine($"Editor tool options {(saved ? "saved" : "NOT saved")} to workflow settings.");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to save editor tool options");
            }
        }

        public static void ApplyImageEditorThemeOptions(ImageEditorOptions options, AppThemeMode mode)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.UseSystemTheme = mode == AppThemeMode.System;
            options.UseSystemAccentColor = true;
        }

        /// <summary>
        /// Applies the specified theme mode to the application
        /// </summary>
        public static void ApplyTheme(AppThemeMode mode)
        {
            try
            {
                bool useDarkMode = ShouldUseDarkMode(mode);
                ThemeVariant appTheme = GetApplicationTheme(useDarkMode);
                ThemeVariant editorTheme = GetEditorTheme(useDarkMode);

                if (Application.Current != null)
                {
                    Application.Current.RequestedThemeVariant = appTheme;
                    UpdateAccentForeground(appTheme);
                    
                    // Keep embedded editors aligned with the app theme mode, then let ImageEditor
                    // own the actual theme and accent application inside EditorView.
                    SyncOpenEditorThemeOptions(mode);
                    ShareX.ImageEditor.Presentation.Theming.ThemeManager.SetTheme(editorTheme);
                    
                    DebugHelper.WriteLine($"Applied theme mode: {mode} (Dark: {useDarkMode}) -> app={appTheme.Key}, editor={editorTheme.Key}");
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
            ThemeVariant appTheme = GetApplicationTheme(ShouldUseDarkMode(SettingsManager.Settings?.ThemeMode ?? AppThemeMode.System));
            UpdateAccentForeground(appTheme, e);

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
                        ThemeVariant appTheme = GetApplicationTheme(isDark);
                        ThemeVariant editorTheme = GetEditorTheme(isDark);
                        Application.Current.RequestedThemeVariant = appTheme;
                        UpdateAccentForeground(appTheme);
                        
                        SyncOpenEditorThemeOptions(AppThemeMode.System);
                        ShareX.ImageEditor.Presentation.Theming.ThemeManager.SetTheme(editorTheme);
                        
                        DebugHelper.WriteLine($"System theme changed, applied: app={appTheme.Key}, editor={editorTheme.Key}");
                    }
                });
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to apply system theme change");
            }
        }

        private static ThemeVariant GetApplicationTheme(bool useDarkMode)
        {
            return useDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        private static ThemeVariant GetEditorTheme(bool useDarkMode)
        {
            return useDarkMode
                ? ShareX.ImageEditor.Presentation.Theming.ThemeManager.ShareXDark
                : ShareX.ImageEditor.Presentation.Theming.ThemeManager.ShareXLight;
        }

        private static void UpdateAccentForeground(ThemeVariant appTheme, PlatformColorValues? colorValues = null)
        {
            if (Application.Current == null)
            {
                return;
            }

            Color accentColor = GetAccentColor(appTheme, colorValues);
            if (accentColor.A == 0)
            {
                return;
            }

            Color foregroundColor = GetAccentForegroundColor(accentColor);

            Application.Current.Resources["XerahS.Color.Accent.Foreground"] = foregroundColor;

            if (Application.Current.TryGetResource("TextOnAccentFillColorPrimaryBrush", appTheme, out object? resourceValue) &&
                resourceValue is SolidColorBrush foregroundBrush)
            {
                foregroundBrush.Color = foregroundColor;
            }
        }

        private static Color GetAccentColor(ThemeVariant appTheme, PlatformColorValues? colorValues)
        {
            if (colorValues != null && colorValues.AccentColor1.A != 0)
            {
                return colorValues.AccentColor1;
            }

            if (Application.Current?.TryGetResource("SystemAccentColor", appTheme, out object? resourceValue) == true)
            {
                return resourceValue switch
                {
                    Color color => color,
                    SolidColorBrush brush => brush.Color,
                    _ => default
                };
            }

            return default;
        }

        private static Color GetAccentForegroundColor(Color accentColor)
        {
            Color lightForeground = GetThemeColor(
                ThemeVariant.Dark,
                "XerahS.Color.Text.Primary",
                Color.Parse("#F3F6F8"));

            Color darkForeground = GetThemeColor(
                ThemeVariant.Light,
                "XerahS.Color.Text.Primary",
                Color.Parse("#161B22"));

            double lightContrast = GetContrastRatio(lightForeground, accentColor);
            double darkContrast = GetContrastRatio(darkForeground, accentColor);

            return darkContrast >= lightContrast * AccentForegroundDarkSwitchRatio
                ? darkForeground
                : lightForeground;
        }

        private static Color GetThemeColor(ThemeVariant theme, string resourceKey, Color fallback)
        {
            if (Application.Current?.TryGetResource(resourceKey, theme, out object? resourceValue) != true)
            {
                return fallback;
            }

            return resourceValue switch
            {
                Color color => color,
                SolidColorBrush brush => brush.Color,
                _ => fallback
            };
        }

        private static double GetContrastRatio(Color firstColor, Color secondColor)
        {
            double firstLuminance = GetRelativeLuminance(firstColor);
            double secondLuminance = GetRelativeLuminance(secondColor);

            double lighter = Math.Max(firstLuminance, secondLuminance);
            double darker = Math.Min(firstLuminance, secondLuminance);

            return (lighter + 0.05) / (darker + 0.05);
        }

        private static double GetRelativeLuminance(Color color)
        {
            double red = LinearizeColorChannel(color.R);
            double green = LinearizeColorChannel(color.G);
            double blue = LinearizeColorChannel(color.B);

            return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
        }

        private static double LinearizeColorChannel(byte channel)
        {
            double normalized = channel / 255.0;

            return normalized <= 0.03928
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        private static void SyncOpenEditorThemeOptions(AppThemeMode mode)
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return;
            }

            foreach (Window window in desktop.Windows)
            {
                foreach (EditorView editorView in EnumerateEditorViews(window))
                {
                    if (editorView.DataContext is not MainViewModel editorViewModel)
                    {
                        continue;
                    }

                    bool useSystemThemeChanged = editorViewModel.Options.UseSystemTheme != (mode == AppThemeMode.System);
                    ApplyImageEditorThemeOptions(editorViewModel.Options, mode);

                    if (useSystemThemeChanged && editorView.IsLoaded)
                    {
                        RefreshEditorViewThemeTracking(editorView, editorViewModel);
                    }
                }
            }
        }

        private static IEnumerable<EditorView> EnumerateEditorViews(Visual root)
        {
            foreach (Visual child in root.GetVisualChildren())
            {
                if (child is EditorView editorView)
                {
                    yield return editorView;
                }

                foreach (EditorView descendant in EnumerateEditorViews(child))
                {
                    yield return descendant;
                }
            }
        }

        private static void RefreshEditorViewThemeTracking(EditorView editorView, MainViewModel editorViewModel)
        {
            // Toggling between explicit Light/Dark and System changes whether EditorView should
            // subscribe to platform color notifications. Reapplying the DataContext triggers its
            // existing DataContextChanged refresh path without duplicating theme logic here.
            editorView.DataContext = null;
            editorView.DataContext = editorViewModel;
        }
    }
}
