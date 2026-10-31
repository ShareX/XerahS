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
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Cloud;
using XerahS.Core.Managers;
using XerahS.Core.SendTo;
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;

namespace XerahS.App
{
    internal class Program
    {
        private static XerahS.Common.SingleInstanceManager? _singleInstanceManager;
        private static string[] _startupArguments = Array.Empty<string>();

        private sealed class StartupOptions
        {
            public string[] ForwardedArguments { get; init; } = Array.Empty<string>();

            public string? PersonalFolderOverride { get; init; }
        }

        private sealed class IncomingPathSet
        {
            public List<string> Files { get; } = [];

            public List<string> Folders { get; } = [];
        }

        private sealed class IncomingPluginPackageSet
        {
            public List<string> PackagePaths { get; } = [];
        }
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                StartupOptions startupOptions = ParseStartupOptions(args ?? Array.Empty<string>());
                _startupArguments = startupOptions.ForwardedArguments;

                if (!string.IsNullOrWhiteSpace(startupOptions.PersonalFolderOverride))
                {
                    SettingsManager.PersonalFolder = startupOptions.PersonalFolderOverride;
                }

                (string mutexName, string pipeName) = GetSingleInstanceIdentifiers(startupOptions.PersonalFolderOverride);

                // Single instance enforcement
                _singleInstanceManager = new XerahS.Common.SingleInstanceManager(mutexName, pipeName, _startupArguments);

                if (!_singleInstanceManager.IsFirstInstance)
                {
                    // Arguments have been passed to the first instance, exit this instance
                    _singleInstanceManager.Dispose();
                    return;
                }

                // Subscribe to receive arguments from subsequent instances
                _singleInstanceManager.ArgumentsReceived += OnArgumentsReceived;

                // Initialize logging (path from PathsManager: LogsFolderBase / GetMainLogFilePath)
                var logPath = XerahS.Common.PathsManager.GetMainLogFilePath();
                XerahS.Common.DebugHelper.Init(logPath);
                RegisterGlobalExceptionHandlers();

                var dh = XerahS.Common.DebugHelper.Logger ?? throw new InvalidOperationException("Logger not initialised");
                dh.AsyncWrite = false; // Synchronous for startup

                dh.WriteLine($"{XerahS.Common.AppResources.AppName} starting.");
                dh.WriteLine("Running as first instance (single instance mode enabled).");

                var version = XerahS.Common.AppResources.Version;
                dh.WriteLine($"Version: {version}");

#if DEBUG
                dh.WriteLine("Build: Debug");
#else
                dh.WriteLine("Build: Release");
#endif

                dh.WriteLine($"Command line: \"{Environment.ProcessPath}\"");
                dh.WriteLine($"Personal path: {XerahS.Common.PathsManager.GetLogsFolderForMonth()}");
                if (!string.IsNullOrWhiteSpace(startupOptions.PersonalFolderOverride))
                {
                    dh.WriteLine($"Personal folder override: {startupOptions.PersonalFolderOverride}");
                }
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
                    ClearX11SessionManagement();

                    // Avalonia renders through X11/XWayland. On pure Wayland autostart, wait briefly for
                    // XWayland and adopt its socket when DISPLAY is unset (XIP0088 Phase 0 item 6).
                    var displayResult = LinuxDisplayBootstrap.EnsureDisplay();
                    XerahS.Common.DebugHelper.WriteLine($"Linux display: {displayResult.Message}");
                    if (!displayResult.Usable)
                    {
                        string message = LinuxDisplayBootstrap.BuildUserMessage(displayResult);
                        XerahS.Common.DebugHelper.WriteLine($"Startup stopped: {message}");
                        XerahS.Common.DebugHelper.Flush();
                        Console.Error.WriteLine(message);
                        LinuxDisplayBootstrap.NotifyStartupFailure(message);
                        Environment.ExitCode = 1;
                        return;
                    }
                }

                // Initialize settings first (Linux portal service preference is needed for platform init)
                XerahS.Core.SettingsManager.LoadInitialSettings();

                InitializePlatformServices();
                ApplyInitialWatchFolderRuntimePolicy();

                // Register callback for post-UI async initialization
                XerahS.UI.App.PostUIInitializationCallback = OnPostUIInitialization;

                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(_startupArguments);

                // Diagnostics treats a session without this line as a crash (DiagnosticsLogAnalyzer.CleanExitMessage).
                XerahS.Common.DebugHelper.WriteLine(XerahS.Core.Diagnostics.DiagnosticsLogAnalyzer.CleanExitMessage);
                XerahS.Common.DebugHelper.Flush();
            }
            catch (Exception ex)
            {
                // Provide helpful guidance for common Linux display issues
                bool isLinuxDisplayError = IsLinuxDisplayError(ex);
                if (isLinuxDisplayError)
                {
                    // Environment problem, not a bug: one readable line plus a desktop notification.
                    string displayMessage = LinuxDisplayBootstrap.BuildUserMessage(
                        new LinuxDisplayBootstrap.Result(false, Environment.GetEnvironmentVariable("DISPLAY"), false, ex.Message + "."));
                    XerahS.Common.DebugHelper.WriteLine($"Startup stopped: {displayMessage}");
                    LinuxDisplayBootstrap.NotifyStartupFailure(displayMessage);
                }
                else
                {
                    XerahS.Common.DebugHelper.WriteException(ex, "Critical application startup failure");
                }

                XerahS.Common.DebugHelper.Flush();

                if (isLinuxDisplayError)
                {
                    Console.Error.WriteLine("\n" + new string('=', 70));
                    Console.Error.WriteLine("ERROR: Unable to connect to display server");
                    Console.Error.WriteLine(new string('=', 70));
                    
                    var displayVar = Environment.GetEnvironmentVariable("DISPLAY");
                    var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
                    var isFlatpak = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID")) || 
                                   System.IO.File.Exists("/.flatpak-info");
                    
                    if (string.IsNullOrEmpty(displayVar) && string.IsNullOrEmpty(waylandDisplay))
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
                        if (isFlatpak)
                        {
                            Console.Error.WriteLine("  - Flatpak sandbox is missing the --socket=x11 permission");
                        }
                    }
                    Console.Error.WriteLine(new string('=', 70) + "\n");
                }
                else if (OperatingSystem.IsLinux())
                {
                    Console.Error.WriteLine("\n" + new string('=', 70));
                    Console.Error.WriteLine("ERROR: XerahS terminated due to an unexpected startup failure");
                    Console.Error.WriteLine(new string('=', 70));
                    Console.Error.WriteLine($"{ex.GetType().FullName}: {ex.Message}");
                }

                // Always tell the user where the full crash details were written.
                try
                {
                    Console.Error.WriteLine($"Full details were logged to: {XerahS.Common.PathsManager.GetMainLogFilePath()}");
                    Console.Error.WriteLine("Please attach this log file when reporting the problem at https://github.com/ShareX/XerahS/issues");
                }
                catch
                {
                    // Log path resolution must never mask the original failure.
                }

#if DEBUG
                if (!isLinuxDisplayError)
                {
                    throw;
                }
#endif
            }
        }

        private static StartupOptions ParseStartupOptions(string[] args)
        {
            if (args.Length == 0)
            {
                return new StartupOptions();
            }

            List<string> forwardedArguments = new List<string>(args.Length);
            string? personalFolderOverride = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg.Equals(AppContracts.Cli.SettingsFolderFlag, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        throw new ArgumentException($"{AppContracts.Cli.SettingsFolderFlag} requires a folder path.");
                    }

                    personalFolderOverride = NormalizePersonalFolder(args[++i]);
                    continue;
                }

                string prefix = AppContracts.Cli.SettingsFolderFlag + "=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string value = arg.Substring(prefix.Length);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new ArgumentException($"{AppContracts.Cli.SettingsFolderFlag} requires a folder path.");
                    }

                    personalFolderOverride = NormalizePersonalFolder(value);
                    continue;
                }

                forwardedArguments.Add(arg);
            }

            return new StartupOptions
            {
                ForwardedArguments = forwardedArguments.ToArray(),
                PersonalFolderOverride = personalFolderOverride
            };
        }

        private static string NormalizePersonalFolder(string path)
        {
            string fullPath = Path.GetFullPath(path);
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static (string MutexName, string PipeName) GetSingleInstanceIdentifiers(string? personalFolderOverride)
        {
            if (string.IsNullOrWhiteSpace(personalFolderOverride))
            {
                return (AppContracts.SingleInstance.MutexName, AppContracts.SingleInstance.PipeName);
            }

            string normalizedPath = NormalizePersonalFolder(personalFolderOverride);

            if (OperatingSystem.IsWindows())
            {
                normalizedPath = normalizedPath.ToUpperInvariant();
            }

            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));
            string suffix = Convert.ToHexString(hashBytes).Substring(0, 16);

            return (
                $"{AppContracts.SingleInstance.MutexName}-{suffix}",
                $"{AppContracts.SingleInstance.PipeName}-{suffix}");
        }

        private static bool IsLinuxDisplayError(Exception ex)
        {
            if (!OperatingSystem.IsLinux())
            {
                return false;
            }

            // Only inspect exception messages, never the stack trace. Any exception that
            // escapes the Avalonia run loop contains "Avalonia.X11" frames, which previously
            // misclassified unrelated failures (e.g. DBus tray errors) as display errors and
            // printed misleading "cannot connect to display" guidance (issue #270).
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current.Message.Contains("XOpenDisplay", StringComparison.OrdinalIgnoreCase) ||
                    current.Message.Contains("Unable to open display", StringComparison.OrdinalIgnoreCase) ||
                    current.Message.Contains("Could not connect to display", StringComparison.OrdinalIgnoreCase) ||
                    (current.Message.Contains("display", StringComparison.OrdinalIgnoreCase) &&
                     current.Message.Contains("connect", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Unsets X11 session management environment variables to prevent Avalonia from registering
        /// with XSMP (X Session Management Protocol). Without this, ksmserver (KDE) may receive a
        /// "cancel shutdown" signal from XerahS because Avalonia connects to the session manager but
        /// does not implement the SaveYourself/Die callback sequence. XerahS does not support session
        /// restore, so dropping the XSMP connection has no functional impact. Fixes issue #169.
        /// </summary>
        private static void ClearX11SessionManagement()
        {
            // Environment.SetEnvironmentVariable updates .NET's environment cache, but
            // native libSM may still see the inherited values through libc getenv().
            foreach (string name in new[] { "SESSION_MANAGER", "SM_CLIENT_ID" })
            {
                Environment.SetEnvironmentVariable(name, null);
                if (UnsetNativeEnvironmentVariable(name) != 0)
                {
                    DebugHelper.WriteLine($"Linux X11: could not clear native {name}.");
                }
            }
            DebugHelper.WriteLine("Linux X11: disabled session management to avoid blocking logout (issues #169, #293).");
        }

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "unsetenv", SetLastError = true)]
        private static extern int UnsetNativeEnvironmentVariable(string name);

        private static X11PlatformOptions CreateX11PlatformOptions() => new()
        {
            WmClass = "xerahs",
            EnableSessionManagement = false
        };

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
                XerahS.Common.DebugHelper.WriteLine("Running inside Flatpak sandbox. Desktop integration (capture, recording,");
                XerahS.Common.DebugHelper.WriteLine("  notifications, hotkeys) is portal-first; native CLI fallbacks are disabled.");
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
                    bool isIgnorableAvaloniaDbusException = 
                        eventArgs.Exception?.InnerException != null &&
                        eventArgs.Exception.InnerException.GetType().FullName == "Tmds.DBus.Protocol.DBusException" &&
                        eventArgs.Exception.InnerException.Message.Contains("ServiceUnknown");

                    if (!isIgnorableAvaloniaDbusException &&
                        XerahS.Common.DBusExceptionClassifier.IsConnectionGone(eventArgs.Exception))
                    {
                        // The session bus or a portal went away (logout, portal restart). Expected; not an error.
                        XerahS.Common.DebugHelper.WriteLine(
                            $"Unobserved D-Bus task ended after the connection closed: {XerahS.Common.DBusExceptionClassifier.Describe(eventArgs.Exception!)}");
                    }
                    else if (!isIgnorableAvaloniaDbusException)
                    {
                        XerahS.Common.DebugHelper.WriteException(eventArgs.Exception!, "Unobserved task exception");
                        XerahS.Common.DebugHelper.Flush();
                    }
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
                XerahS.Platform.Abstractions.IScreenCaptureService realCaptureService =
                    XerahS.Platform.Windows.WindowsScreenCaptureServiceFactory.Create(screenService);

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
                XerahS.Platform.Linux.Services.LinuxDesktopProfile.ConfigureOmaSnapPathOverride(
                    XerahS.Core.SettingsManager.Settings?.LinuxOmaSnapPathOverride);
                var linuxCaptureService = new XerahS.Platform.Linux.LinuxScreenCaptureService();
                var uiCaptureService = new XerahS.UI.Services.ScreenCaptureService(linuxCaptureService);

                bool useWaylandPortalServices = ResolveLinuxWaylandPortalServicesSetting();
                XerahS.Common.DebugHelper.WriteLine($"Linux: UseWaylandPortalServices={useWaylandPortalServices}");

                XerahS.Platform.Linux.LinuxPlatform.Initialize(uiCaptureService, useWaylandPortalServices);
                // NOTE: InitializeRecording() moved to async post-UI initialization in App.axaml.cs
                return;
            }
#endif
            // Fallback for non-Windows/MacOS (or generic stubs)
            // In future: LinuxPlatform.Initialize()
            System.Diagnostics.Debug.WriteLine("Warning: Platform not fully supported, services may not be fully functional.");
        }

        private static bool ResolveLinuxWaylandPortalServicesSetting()
        {
            bool? explicitSetting = XerahS.Core.SettingsManager.Settings?.LinuxUseWaylandPortalServices;
            if (explicitSetting.HasValue)
            {
                return explicitSetting.Value;
            }

            return XerahS.Core.SettingsManager.DefaultTaskSettings?.CaptureSettings?.UseModernCapture ?? true;
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
            XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "=== InitializeBackgroundServicesAsync() CALLED ===");

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
                    XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Background task started");
                    XerahS.Common.DebugHelper.WriteLine("Starting async services initialization...");

                    try
                    {
                        if (SettingsManager.Settings.NetworkMonitorEnabled)
                        {
                            XerahS.Common.NetworkMonitor.NetworkMonitorHost.Shared.EnsureStarted();
                            XerahS.Common.DebugHelper.WriteLine(
                                "Network monitor started. Outage log: " +
                                XerahS.Common.NetworkMonitor.NetworkMonitorHost.Shared.LogFilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        XerahS.Common.DebugHelper.WriteException(ex, "Failed to start network monitor");
                    }

                    AgentSkillBootstrapper.EnsureInstalled();

                    // 1. Initialize Plugins (ProviderCatalog)
                    try
                    {
                        XerahS.Core.Uploaders.ProviderContextManager.EnsureProviderContext();
                        XerahS.Uploaders.PluginSystem.ProviderCatalog.InitializeBuiltInProviders(); // Ensure built-ins
                        XerahS.Uploaders.PluginSystem.ProviderCatalog.LoadPlugins(XerahS.Common.PathsManager.GetPluginDirectories());
                        int pluginCount = XerahS.Uploaders.PluginSystem.ProviderCatalog.GetAllProviders().Count;
                        XerahS.Common.DebugHelper.WriteLine($"Plugins: {pluginCount} loaded");
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
                        XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Platform is Windows, calling WindowsPlatform.InitializeRecording()");
                        XerahS.Platform.Windows.WindowsPlatform.InitializeRecording();
                    }
#endif
#if MACOS
                    if (OperatingSystem.IsMacOS())
                    {
                        XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Platform is macOS, calling MacOSPlatform.InitializeRecording()");
                        XerahS.Platform.MacOS.MacOSPlatform.InitializeRecording();
                    }
#endif
#if LINUX
                    if (OperatingSystem.IsLinux())
                    {
                        XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Platform is Linux, calling LinuxPlatform.InitializeRecording()");
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
                            XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Fallback: Linux detected, calling LinuxPlatform.InitializeRecording()");
                            XerahS.Platform.Linux.LinuxPlatform.InitializeRecording();
                        }
#endif
#if MACOS
                        if (OperatingSystem.IsMacOS())
                        {
                            XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Fallback: macOS detected, calling MacOSPlatform.InitializeRecording()");
                            XerahS.Platform.MacOS.MacOSPlatform.InitializeRecording();
                        }
#endif
#if WINDOWS
                        if (OperatingSystem.IsWindows())
                        {
                            XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Fallback: Windows detected, calling WindowsPlatform.InitializeRecording()");
                            XerahS.Platform.Windows.WindowsPlatform.InitializeRecording();
                        }
#endif
                    }
                    asyncStopwatch.Stop();
                    XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", "Background task completed successfully");
                    XerahS.Common.DebugHelper.WriteLine("Async services initialization completed successfully");
                    
                    // Log startup time (captured on main thread) and async init time
                    XerahS.Common.DebugHelper.WriteLine($"Startup time: {startupTimeMs:F0} ms (+ {asyncStopwatch.ElapsedMilliseconds} ms async init)");
                }
                catch (Exception ex)
                {
                    XerahS.Common.TroubleshootingHelper.Log("ScreenRecorder", "PROGRAM", $"✗ Background task EXCEPTION: {ex.GetType().Name}: {ex.Message}");
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
        {
            return AppBuilder.Configure<XerahS.UI.App>()
                .UsePlatformDetect()
                // Avalonia defaults the X11 WM_CLASS to the entry assembly name ("XerahS"),
                // which does not match StartupWMClass=xerahs in our .desktop files.
                .With(CreateX11PlatformOptions())
                .WithInterFont()
                .LogToTrace();
        }

        /// <summary>
        /// Runs once after the Avalonia UI completes initialization.
        /// </summary>
        private static void OnPostUIInitialization()
        {
            InitializeBackgroundServicesAsync();
            ProcessIncomingArguments(_startupArguments, source: "startup");
        }

        /// <summary>
        /// Handles arguments received from subsequent application instances.
        /// This is called when another instance of the application is launched and passes its arguments here.
        /// </summary>
        private static void OnArgumentsReceived(string[] args)
        {
            string[] redactedArguments = XerahSCloudArgumentRedactor.Redact(args);
            XerahS.Common.DebugHelper.WriteLine($"Arguments received from another instance: {string.Join(" ", redactedArguments)}");

            if (AppContracts.Cli.IsPassiveStartupInvocation(args))
            {
                XerahS.Common.DebugHelper.WriteLine("Ignoring passive startup relay from a secondary instance.");
                return;
            }

            if (AppContracts.Cli.IsReloadWorkflowsInvocation(args))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(ReloadWorkflowsFromDisk);
                return;
            }

            // Hyprland keybinding trigger (XIP0088): run like a hotkey, never raise the main window.
            if (AppContracts.Cli.TryGetRunWorkflowId(args, out string runWorkflowId))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => RunWorkflowFromAutomation(runWorkflowId));
                return;
            }

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
                        
                        // Show in taskbar and bring to front. On macOS, SilentRun is also
                        // the menu-bar-only mode, so the Dock icon should stay hidden.
                        mainWindow.ShowInTaskbar = !OperatingSystem.IsMacOS() ||
                            !XerahS.Core.SettingsManager.Settings.SilentRun;
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
                        ProcessIncomingArguments(args, source: "secondary-instance");
                    }
                }
                catch (Exception ex)
                {
                    XerahS.Common.DebugHelper.WriteException(ex, "Failed to handle arguments from secondary instance");
                }
            });
        }

        /// <summary>
        /// Picks up WorkflowsConfig changes written by automation (omaxerahs) while the app is running,
        /// so the next save does not overwrite them and hotkeys/menus reflect the new state.
        /// </summary>
        private static void ReloadWorkflowsFromDisk()
        {
            try
            {
                XerahS.Core.SettingsManager.LoadWorkflowsConfig();
                if (Avalonia.Application.Current is XerahS.UI.App app && app.WorkflowManager != null)
                {
                    app.WorkflowManager.UpdateHotkeys(XerahS.Core.SettingsManager.WorkflowsConfig.Hotkeys);
                }

                XerahS.Common.DebugHelper.WriteLine("Workflows reloaded from disk at the request of an automation client.");
            }
            catch (Exception ex)
            {
                XerahS.Common.DebugHelper.WriteException(ex, "Failed to reload workflows from disk");
            }
        }

        private static void RunWorkflowFromAutomation(string workflowId)
        {
            try
            {
                XerahS.Core.Hotkeys.WorkflowSettings workflow = XerahS.Core.Automation.WorkflowAutomation.FindWorkflow(workflowId);
                if (Avalonia.Application.Current is not XerahS.UI.App app || !app.TryRunWorkflow(workflow))
                {
                    XerahS.Common.DebugHelper.WriteLine($"Workflow run '{workflowId}' ignored: workflows are not initialized yet.");
                }
            }
            catch (XerahS.Core.Automation.AutomationException ex)
            {
                XerahS.Common.DebugHelper.WriteLine($"Workflow run '{workflowId}' rejected: {ex.Message}");
            }
        }

        private static void ProcessIncomingArguments(string[]? args, string source)
        {
            if (args == null || args.Length == 0)
            {
                return;
            }

            if (AppContracts.Cli.TryGetRunWorkflowId(args, out string runWorkflowId))
            {
                // XerahS was started by "omaxerahs workflow run" because it was not running.
                RunWorkflowFromAutomation(runWorkflowId);
                return;
            }

            string? cloudCallbackArgument = args.FirstOrDefault(XerahSCloudOAuthCallbackParser.IsCallbackArgument);
            if (cloudCallbackArgument != null)
            {
                _ = ProcessCloudCallbackAsync(cloudCallbackArgument, source);
                return;
            }

            IncomingPluginPackageSet pluginPackages = ExtractIncomingPluginPackages(args);
            if (pluginPackages.PackagePaths.Count > 0)
            {
                XerahS.Common.DebugHelper.WriteLine(
                    $"Plugin integration ({source}): Scheduling installer for {pluginPackages.PackagePaths.Count} package(s).");
                OpenPluginPackageInstallers(pluginPackages.PackagePaths, source);
            }

            bool isSendToInvocation = IsSendToInvocation(args);
            IncomingPathSet pathSet = ExtractIncomingPaths(args, includeDirectories: isSendToInvocation);

            if (pathSet.Files.Count == 0 && pathSet.Folders.Count == 0)
            {
                if (pluginPackages.PackagePaths.Count > 0)
                {
                    return;
                }

                XerahS.Common.DebugHelper.WriteLine(
                    isSendToInvocation
                        ? $"Shell integration ({source}): No valid file or folder paths found in Send-to arguments."
                        : $"Shell integration ({source}): No valid file paths found in arguments.");
                return;
            }

            if (isSendToInvocation)
            {
                SendToSelection selection = SendToSelectionClassifier.Create(pathSet.Files, pathSet.Folders);

                XerahS.Common.DebugHelper.WriteLine(
                    $"Shell integration ({source}): Scheduling Send-to prompt for {selection.ItemCount} item(s).");

                _ = Task.Run(() => HandleSendToInvocationAsync(selection, source));
                return;
            }

            XerahS.Common.DebugHelper.WriteLine(
                $"Shell integration ({source}): Scheduling upload for {pathSet.Files.Count} file(s).");
            _ = Task.Run(() => UploadFilesFromIntegrationAsync(pathSet.Files));
        }

        private static async Task ProcessCloudCallbackAsync(string callbackArgument, string source)
        {
            try
            {
                if (!Uri.TryCreate(callbackArgument, UriKind.Absolute, out Uri? callbackUri))
                {
                    XerahS.Common.DebugHelper.WriteLine($"XerahS Cloud OAuth callback ({source}) rejected: invalid URI.");
                    return;
                }

                IXerahSCloudOAuthCoordinator? coordinator =
                    (Application.Current as XerahS.UI.App)?.ServiceProvider?.GetService<IXerahSCloudOAuthCoordinator>();
                if (coordinator == null)
                {
                    XerahS.Common.DebugHelper.WriteLine($"XerahS Cloud OAuth callback ({source}) rejected: coordinator unavailable.");
                    return;
                }

                XerahSCloudOAuthCompletion result = await coordinator.CompleteAsync(callbackUri).ConfigureAwait(false);
                XerahS.Common.DebugHelper.WriteLine($"XerahS Cloud OAuth callback ({source}) result: {result}.");
            }
            catch (Exception ex)
            {
                XerahS.Common.DebugHelper.WriteException(ex, $"XerahS Cloud OAuth callback ({source}) failed");
            }
        }

        private static void OpenPluginPackageInstallers(IReadOnlyList<string> packagePaths, string source)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    var factory = XerahS.UI.Services.UiViewModelFactoryAccessor.GetRequired();

                    foreach (string packagePath in packagePaths)
                    {
                        var viewModel = factory.CreatePluginInstallerViewModel();
                        await viewModel.LoadPackageAsync(packagePath).ConfigureAwait(true);
                        await factory.ViewDialogService.ShowPluginInstallerAsync(viewModel).ConfigureAwait(true);
                    }
                }
                catch (Exception ex)
                {
                    XerahS.Common.DebugHelper.WriteException(ex, $"Failed to open plugin installer from {source}");
                }
            });
        }

        private static bool IsSendToInvocation(IEnumerable<string> args)
        {
            foreach (string rawArg in args)
            {
                if (string.IsNullOrWhiteSpace(rawArg))
                {
                    continue;
                }

                if (rawArg.Trim().Equals(AppContracts.Cli.SendToFlag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IncomingPathSet ExtractIncomingPaths(IEnumerable<string> args, bool includeDirectories)
        {
            var comparer = OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var uniquePaths = new HashSet<string>(comparer);
            IncomingPathSet paths = new();

            bool skipNextAsPluginPath = false;

            foreach (string rawArg in args)
            {
                if (skipNextAsPluginPath)
                {
                    skipNextAsPluginPath = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rawArg))
                {
                    continue;
                }

                string arg = rawArg.Trim();

                if (arg.Equals(AppContracts.Cli.SendToFlag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (arg.Equals(AppContracts.Cli.LegacyInstallPluginFlag, StringComparison.OrdinalIgnoreCase))
                {
                    skipNextAsPluginPath = true;
                    continue;
                }

                if (!TryNormalizeLocalPath(arg, out string normalizedPath))
                {
                    continue;
                }

                if (File.Exists(normalizedPath))
                {
                    if (IsPluginPackagePath(normalizedPath))
                    {
                        continue;
                    }

                    if (uniquePaths.Add(normalizedPath))
                    {
                        paths.Files.Add(normalizedPath);
                    }
                }
                else if (includeDirectories && Directory.Exists(normalizedPath) && uniquePaths.Add(normalizedPath))
                {
                    paths.Folders.Add(normalizedPath);
                }
            }

            return paths;
        }

        private static IncomingPluginPackageSet ExtractIncomingPluginPackages(IEnumerable<string> args)
        {
            var comparer = OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var uniquePaths = new HashSet<string>(comparer);
            IncomingPluginPackageSet packages = new();
            bool nextArgIsPluginPath = false;

            foreach (string rawArg in args)
            {
                if (string.IsNullOrWhiteSpace(rawArg))
                {
                    continue;
                }

                string arg = rawArg.Trim();
                if (nextArgIsPluginPath)
                {
                    nextArgIsPluginPath = false;
                    AddPluginPackagePath(arg, uniquePaths, packages);
                    continue;
                }

                if (arg.Equals(AppContracts.Cli.LegacyInstallPluginFlag, StringComparison.OrdinalIgnoreCase))
                {
                    nextArgIsPluginPath = true;
                    continue;
                }

                AddPluginPackagePath(arg, uniquePaths, packages);
            }

            return packages;
        }

        private static void AddPluginPackagePath(
            string candidate,
            HashSet<string> uniquePaths,
            IncomingPluginPackageSet packages)
        {
            if (!TryNormalizeLocalPath(candidate, out string normalizedPath))
            {
                return;
            }

            if (!IsPluginPackagePath(normalizedPath))
            {
                return;
            }

            if (uniquePaths.Add(normalizedPath))
            {
                packages.PackagePaths.Add(normalizedPath);
            }
        }

        private static bool IsPluginPackagePath(string path)
        {
            return File.Exists(path) &&
                Path.GetExtension(path).Equals(".xsdp", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryNormalizeLocalPath(string input, out string normalizedPath)
        {
            normalizedPath = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string candidate = input.Trim();

            if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? absoluteUri) && absoluteUri.IsFile)
            {
                candidate = absoluteUri.LocalPath;
            }

            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            try
            {
                normalizedPath = Path.GetFullPath(candidate);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task UploadFilesFromIntegrationAsync(IReadOnlyCollection<string> files)
        {
            try
            {
                var taskManager = (Application.Current as XerahS.UI.App)?.ServiceProvider?.GetService<ITaskManager>();
                if (taskManager == null)
                {
                    XerahS.Common.DebugHelper.WriteLine("Shell integration: Task manager unavailable for incoming files.");
                    return;
                }

                foreach (string file in files)
                {
                    TaskSettings settings = CreateFileUploadTaskSettings();
                    settings.Job = WorkflowType.FileUpload;
                    await taskManager.StartFileTask(settings, file);
                }
            }
            catch (Exception ex)
            {
                XerahS.Common.DebugHelper.WriteException(ex, "Shell integration: Failed to upload incoming files.");
            }
        }

        private static async Task HandleSendToInvocationAsync(SendToSelection selection, string source)
        {
            try
            {
                var taskManager = (Application.Current as XerahS.UI.App)?.ServiceProvider?.GetService<ITaskManager>();
                if (taskManager == null)
                {
                    XerahS.Common.DebugHelper.WriteLine("Shell integration: Task manager unavailable for Send-to items.");
                    return;
                }

                SendToIntegrationCoordinator coordinator = new(
                    PlatformServices.UI,
                    taskManager,
                    CreateFileUploadTaskSettings);

                await coordinator.HandleAsync(selection, source);
            }
            catch (Exception ex)
            {
                XerahS.Common.DebugHelper.WriteException(ex, "Shell integration: Failed to process Send-to items.");
            }
        }

        private static TaskSettings CreateFileUploadTaskSettings()
        {
            var uploadWorkflow = SettingsManager.GetFirstWorkflow(WorkflowType.FileUpload);
            if (uploadWorkflow?.TaskSettings != null)
            {
                TaskSettings cloned = CloneTaskSettings(uploadWorkflow.TaskSettings);
                cloned.WorkflowId = uploadWorkflow.Id;
                return cloned;
            }

            return CloneTaskSettings(SettingsManager.DefaultTaskSettings ?? new TaskSettings());
        }

        private static TaskSettings CloneTaskSettings(TaskSettings source)
        {
            JsonSerializerSettings settings = new()
            {
                TypeNameHandling = TypeNameHandling.Auto,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };

            string json = JsonConvert.SerializeObject(source, settings);
            return JsonConvert.DeserializeObject<TaskSettings>(json, settings) ?? new TaskSettings();
        }
    }
}
