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
using Avalonia.WebView.Desktop;

namespace XerahS.App
{
    internal class Program
    {
        private static XerahS.Common.SingleInstanceManager? _singleInstanceManager;

        private const string MutexName = "XerahS-82E6AC09-0FFC-4992-B793-3F79E1F71E70";
        private const string PipeName = "XerahS-Pipe-1F42DA49-7B2A-4E6F-8A3C-D56F09E0C481";

        [STAThread]
        public static void Main(string[] args)
        {
            // Single instance enforcement
            _singleInstanceManager = new XerahS.Common.SingleInstanceManager(MutexName, PipeName, args);

            if (!_singleInstanceManager.IsFirstInstance)
            {
                // Arguments have been passed to the first instance, exit this instance
                _singleInstanceManager.Dispose();
                return;
            }

            // Subscribe to receive arguments from subsequent instances
            _singleInstanceManager.ArgumentsReceived += OnArgumentsReceived;

            // Initialize logging with datestamped file in Logs/yyyy-mm folder structure
            var baseFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), XerahS.Core.SettingsManager.AppName);
            var logsFolder = System.IO.Path.Combine(baseFolder, "Logs", DateTime.Now.ToString("yyyy-MM"));
            var logPath = System.IO.Path.Combine(logsFolder, $"{XerahS.Common.AppResources.AppName}-{DateTime.Now:yyyyMMdd}.log");
            XerahS.Common.DebugHelper.Init(logPath);

            var dh = XerahS.Common.DebugHelper.Logger ?? throw new InvalidOperationException("Logger not initialised");
            dh.AsyncWrite = false; // Synchronous for startup

            dh.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {XerahS.Common.AppResources.AppName} starting.");
            dh.WriteLine("Running as first instance (single instance mode enabled).");

            var version = XerahS.Common.AppResources.Version;
            dh.WriteLine($"Version: {version}");

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
                    if (identity != null)
                    {
                        var principal = new System.Security.Principal.WindowsPrincipal(identity);
                        isElevated = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                    }
                }
            }
            dh.WriteLine($"Running as elevated process: {isElevated}");
#if DEBUG
            dh.WriteLine("Flags: Debug");
#else
            dh.WriteLine("Flags: Release");
#endif

            dh.AsyncWrite = true; // Switch back to async

            InitializePlatformServices();

            // Register callback for post-UI async initialization
            XerahS.UI.App.PostUIInitializationCallback = InitializeRecordingAsync;

            // Initialize settings
            XerahS.Core.SettingsManager.LoadInitialSettings();

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
                XerahS.Common.DebugHelper.WriteLine("macOS: Using MacOSScreenCaptureKitService (native)");
                var macCaptureService = new XerahS.Platform.MacOS.MacOSScreenCaptureKitService();
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

            // Capture startup time on main thread
            double startupTimeMs = 0;
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                startupTimeMs = (DateTime.Now - process.StartTime).TotalMilliseconds;
            }
            catch (Exception ex)
            {
                // Fallback or ignore if permission denied
                System.Diagnostics.Debug.WriteLine($"Failed to get process start time: {ex.Message}");
            }

            // Run on a background thread to avoid blocking UI and store task in shared location
            XerahS.Core.Managers.ScreenRecordingManager.PlatformInitializationTask = System.Threading.Tasks.Task.Run(() =>
            {
                var asyncStopwatch = System.Diagnostics.Stopwatch.StartNew();
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
                    asyncStopwatch.Stop();
                    XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Background task completed successfully");
                    XerahS.Common.DebugHelper.WriteLine("Async recording initialization completed successfully");
                    
                    // Log startup time (captured on main thread) and async init time
                    XerahS.Common.DebugHelper.WriteLine($"Startup time: {startupTimeMs:F0} ms (+ {asyncStopwatch.ElapsedMilliseconds} ms async init)");
                }
                catch (Exception ex)
                {
                    XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", $"✗ Background task EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                    XerahS.Common.DebugHelper.WriteException(ex, "Failed to initialize recording capabilities");

                    // Notify user that recording may not be available
                    try
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            XerahS.Platform.Abstractions.PlatformServices.Toast?.ShowToast(new XerahS.Platform.Abstractions.ToastConfig
                            {
                                Title = "Recording Initialization Warning",
                                Text = "Screen recording initialization failed. Recording may not be available. Check logs for details.",
                                Duration = 6f
                            });
                        });
                    }
                    catch
                    {
                        // Ignore toast failure (UI may not be ready yet)
                    }
                    // Don't rethrow - allow app to continue with fallback
                }
            });
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<XerahS.UI.App>()
                .UsePlatformDetect()
                .UseDesktopWebView()
                .LogToTrace();

        /// <summary>
        /// Handles arguments received from subsequent application instances.
        /// This is called when another instance of the application is launched and passes its arguments here.
        /// </summary>
        private static void OnArgumentsReceived(string[] args)
        {
            XerahS.Common.DebugHelper.WriteLine($"Arguments received from another instance: {string.Join(" ", args)}");

            // Process the arguments on the UI thread to handle any UI-related actions
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    // Bring the main window to the foreground
                    if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                        desktop.MainWindow != null)
                    {
                        var mainWindow = desktop.MainWindow;
                        
                        // Restore if minimized
                        if (mainWindow.WindowState == Avalonia.Controls.WindowState.Minimized)
                        {
                            mainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                        }
                        
                        // Show in taskbar and bring to front
                        mainWindow.ShowInTaskbar = true;
                        mainWindow.Show();
                        mainWindow.Activate();
                        mainWindow.Topmost = true;
                        mainWindow.Topmost = false;
                    }

                    // TODO: Process arguments if needed (e.g., file paths to open, commands to execute)
                    // For now, just activating the window is the primary behavior
                    if (args.Length > 0)
                    {
                        XerahS.Common.DebugHelper.WriteLine($"Processing {args.Length} argument(s) from secondary instance");
                        // Future: Handle specific arguments like file paths for upload, capture commands, etc.
                    }
                }
                catch (Exception ex)
                {
                    XerahS.Common.DebugHelper.WriteException(ex, "Failed to handle arguments from secondary instance");
                }
            });
        }
    }
}

