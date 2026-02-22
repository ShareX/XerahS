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

using System.Reflection;
using System.Runtime.InteropServices;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;

namespace XerahS.Bootstrap
{
    /// <summary>
    /// Shared bootstrap logic for ShareX initialization.
    /// Used by both UI and CLI hosts to ensure consistent configuration and service initialization.
    /// </summary>
    public static class ShareXBootstrap
    {
        /// <summary>
        /// Initialize ShareX platform services, logging, and configuration.
        /// </summary>
        /// <param name="options">Bootstrap configuration options</param>
        /// <returns>Result indicating success of initialization steps</returns>
        public static async Task<BootstrapResult> InitializeAsync(BootstrapOptions options)
        {
            var result = new BootstrapResult();

            try
            {
                // 1. Initialize logging
                if (options.EnableLogging)
                {
                    InitializeLogging(options.LogPath);
                }

                // 2. Load configuration (must be before platform init so UseModernCapture is available)
                SettingsManager.LoadInitialSettings();
                result.ConfigurationLoaded = true;

                // 3. Initialize platform services
                InitializePlatformServices(options.ScreenCaptureService);
                result.PlatformServicesInitialized = true;

                // 4. Initialize recording (async, critical for ScreenRecorder)
                if (options.InitializeRecording)
                {
                    await InitializeRecordingAsync();
                    result.RecordingInitialized = true;
                }

                // 5. Register UI services if provided
                if (options.UIService != null)
                {
                    PlatformServices.RegisterUIService(options.UIService);
                }

                if (options.ToastService != null)
                {
                    PlatformServices.RegisterToastService(options.ToastService);
                }

                return result;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Bootstrap initialization failed");
                throw;
            }
        }

        /// <summary>
        /// Initialize logging with datestamped file in Logs/yyyy-MM folder structure.
        /// </summary>
        private static void InitializeLogging(string? customLogPath = null)
        {
            string logPath;

            if (customLogPath != null)
            {
                logPath = customLogPath;
            }
            else
            {
                var now = DateTime.Now;
                var baseFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    SettingsManager.AppName);
                var logsFolder = Path.Combine(baseFolder, "Logs", now.ToString("yyyy-MM"));
                logPath = Path.Combine(logsFolder, $"ShareX-{now:yyyy-MM-dd}.log");
            }

            string? logDirectory = Path.GetDirectoryName(logPath);
            if (string.IsNullOrEmpty(logDirectory))
            {
                throw new ArgumentException($"Invalid log path: {logPath}", nameof(logPath));
            }

            Directory.CreateDirectory(logDirectory);
            DebugHelper.Init(logPath);

            var dh = DebugHelper.Logger;
            if (dh == null) return;
            dh.AsyncWrite = false; // Synchronous for startup

            dh.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - ShareX starting.");

            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            dh.WriteLine($"Version: {version} Dev");

#if DEBUG
            dh.WriteLine("Build: Debug");
#else
            dh.WriteLine("Build: Release");
#endif

            dh.WriteLine($"Command line: \"{Environment.ProcessPath}\"");
            dh.WriteLine($"Personal path: {Path.GetDirectoryName(logPath)}");
            dh.WriteLine($"Operating system: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
            dh.WriteLine($".NET version: {Environment.Version}");

            bool isElevated = false;
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    isElevated = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"Failed to check elevation status: {ex.Message}");
                    // isElevated remains false
                }
            }
            dh.WriteLine($"Running as elevated process: {isElevated}");
            dh.WriteLine($"Flags: Dev");

            dh.AsyncWrite = true; // Switch back to async
        }

        /// <summary>
        /// Initialize platform-specific services.
        /// </summary>
        private static void InitializePlatformServices(IScreenCaptureService? customCaptureService = null)
        {
#if WINDOWS
            if (OperatingSystem.IsWindows())
            {
                InitializeWindowsPlatform(customCaptureService);
                return;
            }
#elif MACOS
            if (OperatingSystem.IsMacOS())
            {
                InitializeMacOSPlatform(customCaptureService);
                return;
            }
#elif LINUX
            if (OperatingSystem.IsLinux())
            {
                InitializeLinuxPlatform();
                return;
            }
#endif
            // Fallback warning
            DebugHelper.WriteLine("Warning: Platform not fully supported, services may not be fully functional.");
        }

#if WINDOWS
        /// <summary>
        /// Initialize Windows platform services.
        /// </summary>
        private static void InitializeWindowsPlatform(IScreenCaptureService? customCaptureService)
        {
            // Create Windows platform services
            var screenService = new Platform.Windows.WindowsScreenService();

            // Create Windows capture service if not provided
            IScreenCaptureService captureService;

            if (customCaptureService != null)
            {
                captureService = customCaptureService;
            }
            else
            {
                // Choose between modern (Direct3D11/DXGI) or legacy (GDI+) capture
                if (Platform.Windows.WindowsModernCaptureService.IsSupported)
                {
                    DebugHelper.WriteLine("Windows: Using WindowsModernCaptureService (Direct3D11/DXGI)");
                    captureService = new Platform.Windows.WindowsModernCaptureService(screenService);
                }
                else
                {
                    DebugHelper.WriteLine("Windows: Using WindowsScreenCaptureService (GDI+)");
                    captureService = new Platform.Windows.WindowsScreenCaptureService(screenService);
                }
            }

            // Initialize Windows platform with capture service
            Platform.Windows.WindowsPlatform.Initialize(captureService);
        }
#endif

#if MACOS
        /// <summary>
        /// Initialize macOS platform services.
        /// </summary>
        private static void InitializeMacOSPlatform(IScreenCaptureService? customCaptureService)
        {
            IScreenCaptureService captureService;

            if (customCaptureService != null)
            {
                captureService = customCaptureService;
            }
            else
            {
                DebugHelper.WriteLine("macOS: Using MacOSScreenCaptureKitService (native)");
                captureService = new Platform.MacOS.MacOSScreenCaptureKitService();
            }

            Platform.MacOS.MacOSPlatform.Initialize(captureService);
        }
#endif

#if LINUX
        /// <summary>
        /// Initialize Linux platform services.
        /// </summary>
        private static void InitializeLinuxPlatform()
        {
            bool useModernCapture = Core.SettingsManager.DefaultTaskSettings?.CaptureSettings?.UseModernCapture ?? true;
            DebugHelper.WriteLine($"Linux: UseModernCapture={useModernCapture}");
            Platform.Linux.LinuxPlatform.Initialize(useModernCapture: useModernCapture);
        }
#endif

        /// <summary>
        /// Asynchronously initializes platform-specific recording capabilities.
        /// This is performed synchronously for CLI (must wait) and asynchronously for UI (non-blocking).
        /// </summary>
        private static async Task InitializeRecordingAsync()
        {
            Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "BOOTSTRAP", "=== InitializeRecordingAsync() CALLED ===");

            try
            {
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "BOOTSTRAP", "Starting recording initialization");
                DebugHelper.WriteLine("Starting recording initialization...");

                // InitializeRecording performs CPU-bound work (COM initialization, DirectX setup)
                // so Task.Run is used to avoid blocking the caller's synchronization context
#if WINDOWS
                if (OperatingSystem.IsWindows())
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "BOOTSTRAP", "Platform is Windows, calling WindowsPlatform.InitializeRecording()");
                    await Task.Run(() => Platform.Windows.WindowsPlatform.InitializeRecording());
                }
#elif MACOS
                if (OperatingSystem.IsMacOS())
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "BOOTSTRAP", "Platform is macOS, calling MacOSPlatform.InitializeRecording()");
                    await Task.Run(() => Platform.MacOS.MacOSPlatform.InitializeRecording());
                }
#elif LINUX
                if (OperatingSystem.IsLinux())
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "BOOTSTRAP", "Platform is Linux, calling LinuxPlatform.InitializeRecording()");
                    await Task.Run(() => Platform.Linux.LinuxPlatform.InitializeRecording());
                }
#endif

                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "BOOTSTRAP", "Recording initialization completed successfully");
                DebugHelper.WriteLine("Recording initialization completed successfully");
            }
            catch (Exception ex)
            {
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "BOOTSTRAP", $"✗ Recording initialization EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                DebugHelper.WriteException(ex, "Failed to initialize recording capabilities");
                // Don't rethrow - allow app to continue with fallback
            }
        }

        /// <summary>
        /// Start recording initialization on a background thread and store the task.
        /// Used by UI to avoid blocking startup.
        /// </summary>
        public static void StartRecordingInitializationAsync()
        {
            Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "BOOTSTRAP", "=== StartRecordingInitializationAsync() CALLED ===");

            // Run on a background thread to avoid blocking UI and store task in shared location
            Core.Managers.ScreenRecordingManager.PlatformInitializationTask = Task.Run(async () =>
            {
                await InitializeRecordingAsync();
            });
        }
    }
}
