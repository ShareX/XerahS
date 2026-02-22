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
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Platform.Abstractions;

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
            try
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
                RegisterGlobalExceptionHandlers();

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

                // Validate display environment on Linux before starting Avalonia
                if (OperatingSystem.IsLinux())
                {
                    ValidateLinuxDisplayEnvironment();
                }

                // Initialize settings first (UseModernCapture must be available for platform init)
                XerahS.Core.SettingsManager.LoadInitialSettings();

                InitializePlatformServices();
                ApplyInitialWatchFolderRuntimePolicy();

                // Register callback for post-UI async initialization
                XerahS.UI.App.PostUIInitializationCallback = InitializeBackgroundServicesAsync;

                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                XerahS.Common.DebugHelper.WriteException(ex, "Critical application startup failure");
                XerahS.Common.DebugHelper.Flush();

                // Provide helpful guidance for common Linux display issues
                bool isLinuxDisplayError = IsLinuxDisplayError(ex);
                if (isLinuxDisplayError)
                {
                    Console.Error.WriteLine("\n" + new string('=', 70));
                    Console.Error.WriteLine("ERROR: Unable to connect to display server");
                    Console.Error.WriteLine(new string('=', 70));
                    
                    var displayVar = Environment.GetEnvironmentVariable("DISPLAY");
                    var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
                    var isFlatpak = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID")) || 
                                   System.IO.File.Exists("/.flatpak-info");
                    
                    if (isFlatpak)
                    {
                        Console.Error.WriteLine("Running inside Flatpak sandbox detected.");
                        Console.Error.WriteLine("\nSOLUTION: Run from host system using one of these methods:");
                        Console.Error.WriteLine("  1. Use flatpak-spawn:");
                        Console.Error.WriteLine("     flatpak-spawn --host bash -c \"export PATH=\\\"$HOME/.dotnet:$PATH\\\" && ");
                        Console.Error.WriteLine("       cd /home/Public/GitHub/ShareXteam/XerahS && ");
                        Console.Error.WriteLine("       dotnet run --project src/desktop/app/XerahS.App/XerahS.App.csproj\"");
                        Console.Error.WriteLine("\n  2. Run from a non-sandboxed terminal (recommended)");
                    }
                    else if (string.IsNullOrEmpty(displayVar) && string.IsNullOrEmpty(waylandDisplay))
                    {
                        Console.Error.WriteLine("No display environment variables found.");
                        Console.Error.WriteLine("\nSOLUTION: Set the DISPLAY environment variable:");
                        Console.Error.WriteLine("  export DISPLAY=:0     # For X11");
                        Console.Error.WriteLine("  # or check: echo $WAYLAND_DISPLAY");
                    }
                    else
                    {
                        Console.Error.WriteLine($"DISPLAY={displayVar}");
                        Console.Error.WriteLine($"WAYLAND_DISPLAY={waylandDisplay}");
                        Console.Error.WriteLine("\nDisplay variables are set but connection failed.");
                        Console.Error.WriteLine("Possible issues:");
                        Console.Error.WriteLine("  - X11 server not running");
                        Console.Error.WriteLine("  - Permission denied to access display");
                        Console.Error.WriteLine("  - Running in restricted environment (container/sandbox)");
                    }
                    Console.Error.WriteLine(new string('=', 70) + "\n");
                }

#if DEBUG
                if (!isLinuxDisplayError)
                {
                    throw;
                }
#endif
            }
        }

        private static bool IsLinuxDisplayError(Exception ex)
        {
            if (!OperatingSystem.IsLinux())
            {
                return false;
            }

            return ex.Message.Contains("XOpenDisplay", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("display", StringComparison.OrdinalIgnoreCase) ||
                   ex.ToString().Contains("Avalonia.X11", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validates Linux display environment and warns about common issues before Avalonia initialization
        /// </summary>
        private static void ValidateLinuxDisplayEnvironment()
        {
            var displayVar = Environment.GetEnvironmentVariable("DISPLAY");
            var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
            var xdgSessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            var xdgRuntimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            var flatpakId = Environment.GetEnvironmentVariable("FLATPAK_ID");
            var containerEnv = Environment.GetEnvironmentVariable("container");
            bool flatpakInfoExists = System.IO.File.Exists("/.flatpak-info");
            bool isFlatpak = !string.IsNullOrEmpty(flatpakId) || flatpakInfoExists;
            bool isSnap = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SNAP"));
            bool dockerMarker = System.IO.File.Exists("/.dockerenv");
            bool containerEnvMarker = System.IO.File.Exists("/run/.containerenv");
            string? waylandSocketPath = GetWaylandSocketPath(xdgRuntimeDir, waylandDisplay);
            bool waylandSocketExists = !string.IsNullOrEmpty(waylandSocketPath) && System.IO.File.Exists(waylandSocketPath);
            string? x11SocketPath = GetX11SocketPath(displayVar);
            bool x11SocketExists = !string.IsNullOrEmpty(x11SocketPath) && System.IO.File.Exists(x11SocketPath);

            XerahS.Common.DebugHelper.WriteLine($"Display environment check:");
            XerahS.Common.DebugHelper.WriteLine($"  CurrentDirectory={Environment.CurrentDirectory}");
            XerahS.Common.DebugHelper.WriteLine($"  AppContext.BaseDirectory={AppContext.BaseDirectory}");
            XerahS.Common.DebugHelper.WriteLine($"  DISPLAY={displayVar ?? "<not set>"}");
            XerahS.Common.DebugHelper.WriteLine($"  WAYLAND_DISPLAY={waylandDisplay ?? "<not set>"}");
            XerahS.Common.DebugHelper.WriteLine($"  XDG_SESSION_TYPE={xdgSessionType ?? "<not set>"}");
            XerahS.Common.DebugHelper.WriteLine($"  XDG_RUNTIME_DIR={xdgRuntimeDir ?? "<not set>"}");
            XerahS.Common.DebugHelper.WriteLine($"  Flatpak sandbox: {isFlatpak} (FLATPAK_ID={flatpakId ?? "<not set>"}, /.flatpak-info={flatpakInfoExists})");
            XerahS.Common.DebugHelper.WriteLine($"  Snap sandbox: {isSnap} (SNAP={Environment.GetEnvironmentVariable("SNAP") ?? "<not set>"})");
            XerahS.Common.DebugHelper.WriteLine($"  Container markers: container={containerEnv ?? "<not set>"}, /.dockerenv={dockerMarker}, /run/.containerenv={containerEnvMarker}");
            XerahS.Common.DebugHelper.WriteLine($"  Wayland socket path: {waylandSocketPath ?? "<unresolved>"} (exists={waylandSocketExists})");
            XerahS.Common.DebugHelper.WriteLine($"  X11 socket path: {x11SocketPath ?? "<unresolved>"} (exists={x11SocketExists})");

            if (isFlatpak)
            {
                XerahS.Common.DebugHelper.WriteLine("WARNING: Running inside Flatpak sandbox.");
                XerahS.Common.DebugHelper.WriteLine("  Display access may be restricted. If XerahS fails to start,");
                XerahS.Common.DebugHelper.WriteLine("  run from a host terminal or use 'flatpak-spawn --host dotnet run ...'");
            }

            if (string.IsNullOrEmpty(displayVar) && string.IsNullOrEmpty(waylandDisplay))
            {
                XerahS.Common.DebugHelper.WriteLine("WARNING: No display environment variables set.");
                XerahS.Common.DebugHelper.WriteLine("  Continuing without synthetic DISPLAY/WAYLAND_DISPLAY defaults.");
            }
            else
            {
                if (string.Equals(xdgSessionType, "wayland", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(waylandDisplay) &&
                    !waylandSocketExists)
                {
                    XerahS.Common.DebugHelper.WriteLine("WARNING: WAYLAND_DISPLAY is set but resolved socket path does not exist.");
                }

                if (!string.IsNullOrEmpty(displayVar) && !x11SocketExists)
                {
                    XerahS.Common.DebugHelper.WriteLine("WARNING: DISPLAY is set but resolved X11 socket path does not exist.");
                }
            }
        }

        private static string? GetWaylandSocketPath(string? xdgRuntimeDir, string? waylandDisplay)
        {
            if (string.IsNullOrWhiteSpace(xdgRuntimeDir))
            {
                return null;
            }

            string socketName = !string.IsNullOrWhiteSpace(waylandDisplay) ? waylandDisplay : "wayland-0";
            return System.IO.Path.Combine(xdgRuntimeDir, socketName);
        }

        private static string? GetX11SocketPath(string? displayVar)
        {
            if (string.IsNullOrWhiteSpace(displayVar))
            {
                return null;
            }

            string value = displayVar.Trim();
            int colonIndex = value.LastIndexOf(':');
            if (colonIndex < 0 || colonIndex == value.Length - 1)
            {
                return null;
            }

            string displayToken = value[(colonIndex + 1)..];
            int dotIndex = displayToken.IndexOf('.');
            if (dotIndex >= 0)
            {
                displayToken = displayToken[..dotIndex];
            }

            if (!int.TryParse(displayToken, out int displayNumber))
            {
                return null;
            }

            return $"/tmp/.X11-unix/X{displayNumber}";
        }

        private static void RegisterGlobalExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            {
                try
                {
                    if (eventArgs.ExceptionObject is Exception ex)
                    {
                        XerahS.Common.DebugHelper.WriteException(ex, "Unhandled AppDomain exception");
                    }
                    else
                    {
                        XerahS.Common.DebugHelper.WriteException(
                            $"Unhandled exception object: {eventArgs.ExceptionObject ?? "<null>"}",
                            "Unhandled AppDomain exception");
                    }

                    XerahS.Common.DebugHelper.WriteLine($"Unhandled AppDomain exception terminating={eventArgs.IsTerminating}");
                    XerahS.Common.DebugHelper.Flush();
                }
                catch
                {
                    // Avoid throwing from global exception handlers.
                }
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
            {
                try
                {
                    XerahS.Common.DebugHelper.WriteException(eventArgs.Exception, "Unobserved task exception");
                    XerahS.Common.DebugHelper.Flush();
                }
                catch
                {
                    // Avoid throwing from global exception handlers.
                }
                finally
                {
                    eventArgs.SetObserved();
                }
            };
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
                XerahS.Common.DebugHelper.WriteLine("Linux: Initializing platform services");
                var linuxCaptureService = new XerahS.Platform.Linux.LinuxScreenCaptureService();
                var uiCaptureService = new XerahS.UI.Services.ScreenCaptureService(linuxCaptureService);

                bool useModernCapture = XerahS.Core.SettingsManager.DefaultTaskSettings?.CaptureSettings?.UseModernCapture ?? true;
                XerahS.Common.DebugHelper.WriteLine($"Linux: UseModernCapture={useModernCapture}");

                XerahS.Platform.Linux.LinuxPlatform.Initialize(uiCaptureService, useModernCapture);
                // NOTE: InitializeRecording() moved to async post-UI initialization in App.axaml.cs
                return;
            }
#endif
            // Fallback for non-Windows/MacOS (or generic stubs)
            // In future: LinuxPlatform.Initialize()
            System.Diagnostics.Debug.WriteLine("Warning: Platform not fully supported, services may not be fully functional.");
        }

        private static void ApplyInitialWatchFolderRuntimePolicy()
        {
            try
            {
                bool daemonRunning = IsWatchFolderDaemonRunning();
                if (daemonRunning)
                {
                    WatchFolderManager.Instance.Stop();
                    XerahS.Common.DebugHelper.WriteLine("Watch folder daemon is running. In-process watchers remain stopped.");
                }
                else
                {
                    WatchFolderManager.Instance.StartOrReloadFromCurrentSettings();
                    XerahS.Common.DebugHelper.WriteLine("Watch folder daemon is not running. In-process watchers started from current settings.");
                }
            }
            catch (Exception ex)
            {
                XerahS.Common.DebugHelper.WriteException(ex, "Failed to apply initial watch folder runtime policy.");
                WatchFolderManager.Instance.StartOrReloadFromCurrentSettings();
            }
        }

        private static bool IsWatchFolderDaemonRunning()
        {
            IWatchFolderDaemonService daemonService = PlatformServices.WatchFolderDaemon;
            if (!daemonService.IsSupported)
            {
                return false;
            }

            WatchFolderDaemonScope scope = ResolveEffectiveWatchFolderDaemonScope();
            if (!daemonService.SupportsScope(scope))
            {
                return false;
            }

            WatchFolderDaemonStatus status = RunWatchFolderDaemonCall(() => daemonService.GetStatusAsync(scope));
            return status.State == WatchFolderDaemonState.Running;
        }

        private static T RunWatchFolderDaemonCall<T>(Func<Task<T>> daemonCall)
        {
            return Task.Run(daemonCall).GetAwaiter().GetResult();
        }

        private static WatchFolderDaemonScope ResolveEffectiveWatchFolderDaemonScope()
        {
            if (OperatingSystem.IsWindows())
            {
                return WatchFolderDaemonScope.System;
            }

            return SettingsManager.Settings.WatchFolderDaemonScope;
        }

        /// <summary>
        /// Asynchronously initializes platform-specific services (recording, plugins) after the UI is loaded.
        /// This prevents blocking the main window from appearing during startup.
        /// Called via PostUIInitializationCallback from App.axaml.cs after OnFrameworkInitializationCompleted.
        /// </summary>
        private static void InitializeBackgroundServicesAsync()
        {
            XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "=== InitializeBackgroundServicesAsync() CALLED ===");

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
                    XerahS.Common.DebugHelper.WriteLine("Starting async services initialization...");
                    
                    // 1. Initialize Plugins (ProviderCatalog)
                    try
                    {
                        XerahS.Common.DebugHelper.WriteLine("Initializing Plugins...");
                        XerahS.Core.Uploaders.ProviderContextManager.EnsureProviderContext();
                        XerahS.Uploaders.PluginSystem.ProviderCatalog.InitializeBuiltInProviders(); // Ensure built-ins
                        
                        var pluginPaths = XerahS.Common.PathsManager.GetPluginDirectories();
                        XerahS.Common.DebugHelper.WriteLine($"Scanning for plugins in: {string.Join(", ", pluginPaths)}");
                        
                        XerahS.Uploaders.PluginSystem.ProviderCatalog.LoadPlugins(pluginPaths);
                        
                        int pluginCount = XerahS.Uploaders.PluginSystem.ProviderCatalog.GetAllProviders().Count;
                        XerahS.Common.DebugHelper.WriteLine($"Plugins initialized. Total providers: {pluginCount}");
                    }
                    catch (Exception ex)
                    {
                        XerahS.Common.DebugHelper.WriteException(ex, "Failed to initialize plugins");
                    }

                    // 2. Initialize Recording Platform Services
                    // Use runtime checks instead of preprocessor directives for reliability
                    XerahS.Common.DebugHelper.WriteLine("Initializing recording platform services...");

                    // Debug: Show which preprocessor symbols are defined
                    string definedSymbols = "";
#if WINDOWS
                    definedSymbols += "WINDOWS ";
#endif
#if MACOS
                    definedSymbols += "MACOS ";
#endif
#if LINUX
                    definedSymbols += "LINUX ";
#endif
                    XerahS.Common.DebugHelper.WriteLine($"Preprocessor symbols defined: [{definedSymbols.Trim()}]");
                    XerahS.Common.DebugHelper.WriteLine($"Runtime OS: IsLinux={OperatingSystem.IsLinux()}, IsMacOS={OperatingSystem.IsMacOS()}, IsWindows={OperatingSystem.IsWindows()}");
#if WINDOWS
                    if (OperatingSystem.IsWindows())
                    {
                        XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Platform is Windows, calling WindowsPlatform.InitializeRecording()");
                        XerahS.Platform.Windows.WindowsPlatform.InitializeRecording();
                    }
#endif
#if MACOS
                    if (OperatingSystem.IsMacOS())
                    {
                        XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Platform is macOS, calling MacOSPlatform.InitializeRecording()");
                        XerahS.Platform.MacOS.MacOSPlatform.InitializeRecording();
                    }
#endif
#if LINUX
                    if (OperatingSystem.IsLinux())
                    {
                        XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Platform is Linux, calling LinuxPlatform.InitializeRecording()");
                        XerahS.Platform.Linux.LinuxPlatform.InitializeRecording();
                    }
#endif
                    // Fallback: Initialize based on runtime OS detection if no preprocessor symbol matched
                    if (XerahS.RegionCapture.ScreenRecording.ScreenRecorderService.NativeRecordingServiceFactory == null &&
                        XerahS.RegionCapture.ScreenRecording.ScreenRecorderService.FallbackServiceFactory == null)
                    {
                        XerahS.Common.DebugHelper.WriteLine("No recording service initialized via preprocessor - trying runtime detection");
#if LINUX
                        if (OperatingSystem.IsLinux())
                        {
                            XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Fallback: Linux detected, calling LinuxPlatform.InitializeRecording()");
                            XerahS.Platform.Linux.LinuxPlatform.InitializeRecording();
                        }
#endif
#if MACOS
                        if (OperatingSystem.IsMacOS())
                        {
                            XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Fallback: macOS detected, calling MacOSPlatform.InitializeRecording()");
                            XerahS.Platform.MacOS.MacOSPlatform.InitializeRecording();
                        }
#endif
#if WINDOWS
                        if (OperatingSystem.IsWindows())
                        {
                            XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Fallback: Windows detected, calling WindowsPlatform.InitializeRecording()");
                            XerahS.Platform.Windows.WindowsPlatform.InitializeRecording();
                        }
#endif
                    }
                    asyncStopwatch.Stop();
                    XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Background task completed successfully");
                    XerahS.Common.DebugHelper.WriteLine("Async services initialization completed successfully");
                    
                    // Log startup time (captured on main thread) and async init time
                    XerahS.Common.DebugHelper.WriteLine($"Startup time: {startupTimeMs:F0} ms (+ {asyncStopwatch.ElapsedMilliseconds} ms async init)");
                }
                catch (Exception ex)
                {
                    XerahS.Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", $"✗ Background task EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                    XerahS.Common.DebugHelper.WriteException(ex, "Failed to initialize background services");

                    // Notify user that recording/services may not be available
                    try
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            XerahS.Platform.Abstractions.PlatformServices.Toast?.ShowToast(new XerahS.Platform.Abstractions.ToastConfig
                            {
                                Title = "Initialization Warning",
                                Text = "Background services initialization failed. Check logs for details.",
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
