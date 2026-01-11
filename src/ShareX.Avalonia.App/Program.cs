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

using Avalonia;

namespace XerahS.App
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Initialize logging with datestamped file in Logs/yyyy-mm folder structure
            var baseFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), XerahS.Core.SettingManager.AppName);
            var logsFolder = System.IO.Path.Combine(baseFolder, "Logs", DateTime.Now.ToString("yyyy-MM"));
            var logPath = System.IO.Path.Combine(logsFolder, $"ShareX-{DateTime.Now:yyyy-MM-dd}.log");
            XerahS.Common.DebugHelper.Init(logPath);

            var dh = XerahS.Common.DebugHelper.Logger;
            dh.AsyncWrite = false; // Synchronous for startup

            dh.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - ShareX starting.");

            var version = XerahS.Common.ShareXResources.Version;
            dh.WriteLine($"Version: {version} Dev");

#if DEBUG
            dh.WriteLine("Build: Debug");
#else
            dh.WriteLine("Build: Release");
#endif

            dh.WriteLine($"Command line: \"{Environment.ProcessPath}\"");
            dh.WriteLine($"Personal path: {logsFolder}");
            dh.WriteLine($"Operating system: {System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture})");
            dh.WriteLine($".NET version: {System.Environment.Version}");

            bool isElevated = false;
            if (OperatingSystem.IsWindows())
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    isElevated = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
            }
            dh.WriteLine($"Running as elevated process: {isElevated}");
            dh.WriteLine($"Flags: Dev");

            dh.AsyncWrite = true; // Switch back to async

            InitializePlatformServices();

            // Register callback for post-UI async initialization
            XerahS.UI.App.PostUIInitializationCallback = InitializeRecordingAsync;

            // Initialize settings
            XerahS.Core.SettingManager.LoadInitialSettings();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        private static void InitializePlatformServices()
        {
#if WINDOWS
            if (OperatingSystem.IsWindows())
            {
                // Create Windows platform services
                var screenService = new XerahS.Platform.Windows.WindowsScreenService();

                // Create Windows capture service
                XerahS.Platform.Abstractions.IScreenCaptureService realCaptureService;

                if (XerahS.Platform.Windows.WindowsModernCaptureService.IsSupported)
                {
                    XerahS.Common.DebugHelper.WriteLine("Windows: Using WindowsModernCaptureService (Direct3D11/DXGI)");
                    realCaptureService = new XerahS.Platform.Windows.WindowsModernCaptureService(screenService);
                }
                else
                {
                    XerahS.Common.DebugHelper.WriteLine("Windows: Using WindowsScreenCaptureService (GDI+)");
                    realCaptureService = new XerahS.Platform.Windows.WindowsScreenCaptureService(screenService);
                }

                // Create UI capture service (Wrapper with Region UI)
                // This delegates to realCaptureService for actual capture
                var uiCaptureService = new XerahS.UI.Services.ScreenCaptureService(realCaptureService);

                // Initialize Windows platform with our UI wrapper
                XerahS.Platform.Windows.WindowsPlatform.Initialize(uiCaptureService);
                // NOTE: InitializeRecording() moved to async post-UI initialization in App.axaml.cs
                return;
            }
#elif MACOS
            if (OperatingSystem.IsMacOS())
            {
                XerahS.Common.DebugHelper.WriteLine("macOS: Using MacOSScreenshotService (screencapture CLI)");
                var macCaptureService = new XerahS.Platform.MacOS.MacOSScreenshotService();
                var uiCaptureService = new XerahS.UI.Services.ScreenCaptureService(macCaptureService);

                XerahS.Platform.MacOS.MacOSPlatform.Initialize(uiCaptureService);
                // NOTE: InitializeRecording() moved to async post-UI initialization in App.axaml.cs
                return;
            }
#elif LINUX
            if (OperatingSystem.IsLinux())
            {
                XerahS.Platform.Linux.LinuxPlatform.Initialize();
                // NOTE: InitializeRecording() moved to async post-UI initialization in App.axaml.cs
                return;
            }
#endif
            // Fallback for non-Windows/MacOS (or generic stubs)
            // In future: LinuxPlatform.Initialize()
            System.Diagnostics.Debug.WriteLine("Warning: Platform not fully supported, services may not be fully functional.");
        }

        /// <summary>
        /// Asynchronously initializes platform-specific recording capabilities after the UI is loaded.
        /// This prevents blocking the main window from appearing during startup.
        /// Called via PostUIInitializationCallback from App.axaml.cs after OnFrameworkInitializationCompleted.
        /// </summary>
        private static void InitializeRecordingAsync()
        {
            XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "=== InitializeRecordingAsync() CALLED ===");

            // Run on a background thread to avoid blocking UI and store task in shared location
            XerahS.Core.Managers.ScreenRecordingManager.PlatformInitializationTask = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Background task started");
                    XerahS.Common.DebugHelper.WriteLine("Starting async recording initialization...");
#if WINDOWS
                    if (OperatingSystem.IsWindows())
                    {
                        XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Platform is Windows, calling WindowsPlatform.InitializeRecording()");
                        XerahS.Platform.Windows.WindowsPlatform.InitializeRecording();
                    }
#elif MACOS
                    if (OperatingSystem.IsMacOS())
                    {
                        XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Platform is macOS, calling MacOSPlatform.InitializeRecording()");
                        XerahS.Platform.MacOS.MacOSPlatform.InitializeRecording();
                    }
#elif LINUX
                    if (OperatingSystem.IsLinux())
                    {
                        XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Platform is Linux, calling LinuxPlatform.InitializeRecording()");
                        XerahS.Platform.Linux.LinuxPlatform.InitializeRecording();
                    }
#endif
                    XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Background task completed successfully");
                    XerahS.Common.DebugHelper.WriteLine("Async recording initialization completed successfully");
                }
                catch (Exception ex)
                {
                    XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", $"✗ Background task EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                    XerahS.Common.DebugHelper.WriteException(ex, "Failed to initialize recording capabilities");
                    // Don't rethrow - allow app to continue with fallback
                }
            });
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<XerahS.UI.App>()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
