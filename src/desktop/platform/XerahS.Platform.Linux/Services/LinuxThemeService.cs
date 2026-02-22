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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services
{
    /// <summary>
    /// Linux implementation of IThemeService using XDG Settings Portal for dark mode detection.
    /// Falls back to GTK settings if the portal is not available.
    /// </summary>
    public class LinuxThemeService : IThemeService, IDisposable
    {
        private const string PortalBusName = "org.freedesktop.portal.Desktop";
        private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");
        private const string AppearanceNamespace = "org.freedesktop.appearance";
        private const string ColorSchemeKey = "color-scheme";

        // Color scheme values from XDG portal spec
        private const int ColorSchemeNoPreference = 0;
        private const int ColorSchemeDark = 1;
        private const int ColorSchemeLight = 2;

        private int _colorScheme = ColorSchemeNoPreference;
        private Connection? _connection;
        private IDisposable? _signalSubscription;
        private bool _isMonitoring;
        private bool _disposed;

        public bool IsDarkModePreferred => _colorScheme == ColorSchemeDark;
        public bool IsLightModePreferred => _colorScheme == ColorSchemeLight;
        public int ColorScheme => _colorScheme;

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        public LinuxThemeService()
        {
            // Try to read initial theme preference
            _colorScheme = ReadColorScheme();
        }

        /// <summary>
        /// Reads the current color scheme preference from the system
        /// </summary>
        private int ReadColorScheme()
        {
            // First try XDG Settings Portal (preferred method for modern Linux desktops)
            int? portalResult = TryReadFromSettingsPortal();
            if (portalResult.HasValue)
            {
                Debug.WriteLine($"LinuxThemeService: Got color-scheme from XDG portal: {portalResult.Value}");
                return portalResult.Value;
            }

            // Fall back to gsettings (GNOME/GTK)
            int? gsettingsResult = TryReadFromGSettings();
            if (gsettingsResult.HasValue)
            {
                Debug.WriteLine($"LinuxThemeService: Got color-scheme from gsettings: {gsettingsResult.Value}");
                return gsettingsResult.Value;
            }

            // Fall back to checking GTK theme name (legacy)
            bool? gtkDark = TryReadGtkThemeDark();
            if (gtkDark.HasValue)
            {
                Debug.WriteLine($"LinuxThemeService: Got dark mode from GTK theme name: {gtkDark.Value}");
                return gtkDark.Value ? ColorSchemeDark : ColorSchemeLight;
            }

            Debug.WriteLine("LinuxThemeService: No theme preference detected, defaulting to dark");
            // Default to dark mode for no preference (common choice for developer tools)
            return ColorSchemeDark;
        }

        /// <summary>
        /// Reads color scheme from XDG Settings Portal via D-Bus
        /// </summary>
        private int? TryReadFromSettingsPortal()
        {
            try
            {
                using var connection = new Connection(Address.Session);
                connection.ConnectAsync().GetAwaiter().GetResult();

                var proxy = connection.CreateProxy<ISettingsPortal>(PortalBusName, PortalObjectPath);
                var result = proxy.ReadAsync(AppearanceNamespace, ColorSchemeKey).GetAwaiter().GetResult();

                // The result is a variant containing the actual value
                if (result is object[] arr && arr.Length > 0)
                {
                    // Unwrap nested variants
                    object value = arr[0];
                    while (value is object[] nested && nested.Length > 0)
                    {
                        value = nested[0];
                    }

                    if (value is uint uintVal)
                    {
                        return (int)uintVal;
                    }
                    if (value is int intVal)
                    {
                        return intVal;
                    }
                    if (int.TryParse(value?.ToString(), out int parsed))
                    {
                        return parsed;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LinuxThemeService: Failed to read from Settings Portal: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads color scheme from gsettings (GNOME desktop)
        /// </summary>
        private int? TryReadFromGSettings()
        {
            try
            {
                // Check GNOME color-scheme setting
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "gsettings",
                    Arguments = "get org.gnome.desktop.interface color-scheme",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process == null) return null;

                string output = process.StandardOutput.ReadToEnd().Trim().Trim('\'');
                process.WaitForExit(1000);

                if (process.ExitCode != 0) return null;

                return output switch
                {
                    "prefer-dark" => ColorSchemeDark,
                    "prefer-light" => ColorSchemeLight,
                    "default" => ColorSchemeNoPreference,
                    _ => null
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LinuxThemeService: Failed to read from gsettings: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if the GTK theme name contains "dark"
        /// </summary>
        private bool? TryReadGtkThemeDark()
        {
            try
            {
                // Try gsettings for GTK theme
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "gsettings",
                    Arguments = "get org.gnome.desktop.interface gtk-theme",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (process == null) return null;

                string output = process.StandardOutput.ReadToEnd().Trim().Trim('\'');
                process.WaitForExit(1000);

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    return output.Contains("dark", StringComparison.OrdinalIgnoreCase) ||
                           output.Contains("Dark", StringComparison.Ordinal);
                }
            }
            catch
            {
                // Ignore
            }

            // Try reading GTK settings file
            try
            {
                string settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config", "gtk-3.0", "settings.ini");

                if (File.Exists(settingsPath))
                {
                    string content = File.ReadAllText(settingsPath);
                    // Look for gtk-theme-name=...
                    foreach (string line in content.Split('\n'))
                    {
                        if (line.StartsWith("gtk-theme-name=", StringComparison.OrdinalIgnoreCase))
                        {
                            string themeName = line.Substring("gtk-theme-name=".Length).Trim();
                            return themeName.Contains("dark", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
            }
            catch
            {
                // Ignore
            }

            return null;
        }

        public void StartMonitoring()
        {
            if (_isMonitoring || _disposed) return;

            try
            {
                _connection = new Connection(Address.Session);
                _connection.ConnectAsync().GetAwaiter().GetResult();

                var proxy = _connection.CreateProxy<ISettingsPortal>(PortalBusName, PortalObjectPath);

                // Subscribe to SettingChanged signal
                _signalSubscription = proxy.WatchSettingChangedAsync(
                    OnSettingChanged,
                    ex => Debug.WriteLine($"LinuxThemeService: Signal error: {ex.Message}")
                ).GetAwaiter().GetResult();

                _isMonitoring = true;
                Debug.WriteLine("LinuxThemeService: Started monitoring for theme changes");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LinuxThemeService: Failed to start monitoring: {ex.Message}");
            }
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            _signalSubscription?.Dispose();
            _signalSubscription = null;

            _connection?.Dispose();
            _connection = null;

            _isMonitoring = false;
            Debug.WriteLine("LinuxThemeService: Stopped monitoring for theme changes");
        }

        private void OnSettingChanged((string Namespace, string Key, object Value) change)
        {
            if (change.Namespace != AppearanceNamespace || change.Key != ColorSchemeKey)
            {
                return;
            }

            int newColorScheme = ColorSchemeNoPreference;

            // Unwrap the value
            object value = change.Value;
            while (value is object[] nested && nested.Length > 0)
            {
                value = nested[0];
            }

            if (value is uint uintVal)
            {
                newColorScheme = (int)uintVal;
            }
            else if (value is int intVal)
            {
                newColorScheme = intVal;
            }
            else if (int.TryParse(value?.ToString(), out int parsed))
            {
                newColorScheme = parsed;
            }

            if (newColorScheme != _colorScheme)
            {
                _colorScheme = newColorScheme;
                Debug.WriteLine($"LinuxThemeService: Theme changed to color-scheme={newColorScheme} (dark={IsDarkModePreferred})");
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(IsDarkModePreferred, newColorScheme));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopMonitoring();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// D-Bus interface for the XDG Settings Portal
        /// </summary>
        [DBusInterface("org.freedesktop.portal.Settings")]
        private interface ISettingsPortal : IDBusObject
        {
            Task<object> ReadAsync(string Namespace, string Key);
            Task<IDisposable> WatchSettingChangedAsync(Action<(string Namespace, string Key, object Value)> handler, Action<Exception>? onError = null);
        }
    }
}
