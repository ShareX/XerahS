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
using System;

namespace ShareX.Ava.App
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Initialize logging with datestamped file in Logs/yyyy-mm folder structure
            var baseFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ShareX.Ava.Core.SettingManager.AppName);
            var logsFolder = System.IO.Path.Combine(baseFolder, "Logs", DateTime.Now.ToString("yyyy-MM"));
            var logPath = System.IO.Path.Combine(logsFolder, $"ShareX-{DateTime.Now:yyyy-MM-dd}.log");
            ShareX.Ava.Common.DebugHelper.Init(logPath);
            
            var dh = ShareX.Ava.Common.DebugHelper.Logger;
            dh.AsyncWrite = false; // Synchronous for startup

            dh.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - ShareX starting.");
            
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
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
            
            // Initialize settings
            ShareX.Ava.Core.SettingManager.LoadInitialSettings();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        private static void InitializePlatformServices()
        {
#if WINDOWS
            if (OperatingSystem.IsWindows())
            {
                // Create Windows platform services
                var screenService = new ShareX.Ava.Platform.Windows.WindowsScreenService();

                // Create Windows capture service
                ShareX.Ava.Platform.Abstractions.IScreenCaptureService realCaptureService;

                if (ShareX.Ava.Platform.Windows.WindowsModernCaptureService.IsSupported)
                {
                    ShareX.Ava.Common.DebugHelper.WriteLine("Windows: Using WindowsModernCaptureService (Direct3D11/DXGI)");
                    realCaptureService = new ShareX.Ava.Platform.Windows.WindowsModernCaptureService(screenService);
                }
                else
                {
                    ShareX.Ava.Common.DebugHelper.WriteLine("Windows: Using WindowsScreenCaptureService (GDI+)");
                    realCaptureService = new ShareX.Ava.Platform.Windows.WindowsScreenCaptureService(screenService);
                }

                // Create UI capture service (Wrapper with Region UI)
                // This delegates to realCaptureService for actual capture
                var uiCaptureService = new ShareX.Ava.UI.Services.ScreenCaptureService(realCaptureService);

                // Initialize Windows platform with our UI wrapper
                ShareX.Ava.Platform.Windows.WindowsPlatform.Initialize(uiCaptureService);
                return;
            }
#elif MACOS
            if (OperatingSystem.IsMacOS())
            {
                ShareX.Ava.Common.DebugHelper.WriteLine("macOS: Using MacOSScreenshotService (screencapture CLI)");
                var macCaptureService = new ShareX.Ava.Platform.MacOS.MacOSScreenshotService();
                var uiCaptureService = new ShareX.Ava.UI.Services.ScreenCaptureService(macCaptureService);

                ShareX.Ava.Platform.MacOS.MacOSPlatform.Initialize(uiCaptureService);
                return;
            }
#elif LINUX
            if (OperatingSystem.IsLinux())
            {
                ShareX.Ava.Platform.Linux.LinuxPlatform.Initialize();
                return;
            }
#endif
            // Fallback for non-Windows/MacOS (or generic stubs)
            // In future: LinuxPlatform.Initialize()
            System.Diagnostics.Debug.WriteLine("Warning: Platform not fully supported, services may not be fully functional.");
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<ShareX.Ava.UI.App>()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
