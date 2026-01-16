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

using XerahS.Common;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows
{
    /// <summary>
    /// Initializes Windows platform services
    /// </summary>
    public static class WindowsPlatform
    {
        /// <summary>
        /// Initializes all Windows platform services
        /// </summary>
        public static void Initialize(IScreenCaptureService? screenCaptureService = null)
        {
            var screenService = new WindowsScreenService();

            // If no service provided, use modern DXGI capture if supported, otherwise GDI+
            if (screenCaptureService == null)
            {
                if (WindowsModernCaptureService.IsSupported)
                {
                    DebugHelper.WriteLine("Modern DXGI screen capture is supported. Using WindowsModernCaptureService.");
                    screenCaptureService = new WindowsModernCaptureService(screenService);
                }
                else
                {
                    DebugHelper.WriteLine("Modern DXGI screen capture is NOT supported (requires Windows 8+). Using legacy GDI+ WindowsScreenCaptureService.");
                    screenCaptureService = new WindowsScreenCaptureService(screenService);
                }
            }

            PlatformServices.Initialize(
                platformInfo: new WindowsPlatformInfo(),
                screenService: screenService,
                clipboardService: new WindowsClipboardService(),
                windowService: new WindowsWindowService(),
                screenCaptureService: screenCaptureService,
                hotkeyService: new WindowsHotkeyService(),
                inputService: new WindowsInputService(),
                fontService: new WindowsFontService(),
                systemService: new Services.WindowsSystemService(),
                notificationService: new WindowsNotificationService()
            );

            // Register AUMID for UWP Toast Notifications
            SetAUMID("ShareXTeam.XerahS");
        }

        private static void SetAUMID(string aumid)
        {
            try
            {
                SetCurrentProcessExplicitAppUserModelID(aumid);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to set AUMID");
            }
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID(
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string AppID);

        /// <summary>
        /// Initializes native screen recording support
        /// Uses Windows.Graphics.Capture + Media Foundation when available
        /// Falls back to FFmpeg on unsupported systems (Stage 4)
        /// </summary>
        public static void InitializeRecording()
        {
            Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", "=== WindowsPlatform.InitializeRecording() START ===");

            try
            {
                // Get OS version for logging
                var osVersion = Environment.OSVersion.Version;
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", $"OS Version: {osVersion.Major}.{osVersion.Minor}.{osVersion.Build}");

                // Check Windows.Graphics.Capture support
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", "Checking WindowsGraphicsCaptureSource.IsSupported...");
                bool wgcSupported = Recording.WindowsGraphicsCaptureSource.IsSupported;
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", $"WindowsGraphicsCaptureSource.IsSupported = {wgcSupported}");

                // Check Media Foundation support
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", "Checking MediaFoundationEncoder.IsAvailable...");
                bool mfAvailable = Recording.MediaFoundationEncoder.IsAvailable;
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", $"MediaFoundationEncoder.IsAvailable = {mfAvailable}");

                // Check if native recording is supported
                if (wgcSupported && mfAvailable)
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", "✓ Native recording (WGC + Media Foundation) is supported. Enabling modern recording.");
                    DebugHelper.WriteLine("Native recording (WGC + Media Foundation) is supported. Enabling modern recording.");

                    // Set up factory functions for native recording
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", "Setting CaptureSourceFactory...");
                    XerahS.RegionCapture.ScreenRecording.ScreenRecorderService.CaptureSourceFactory =
                        () => new Recording.WindowsGraphicsCaptureSource();

                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", "Setting EncoderFactory...");
                    XerahS.RegionCapture.ScreenRecording.ScreenRecorderService.EncoderFactory =
                        () => new Recording.MediaFoundationEncoder();

                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", "✓ Factories registered successfully");
                }
                else
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", $"✗ Native recording NOT supported. WGC={wgcSupported}, MF={mfAvailable}");
                    DebugHelper.WriteLine("Native recording NOT supported. Requires Windows 10 1803+ and Media Foundation.");
                    DebugHelper.WriteLine($"  - Windows.Graphics.Capture support: {wgcSupported}");
                    DebugHelper.WriteLine($"  - Media Foundation availability: {mfAvailable}");
                    DebugHelper.WriteLine("Initializing FFmpeg fallback recording service.");

                    // Set up FFmpeg fallback
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", "Setting FallbackServiceFactory (FFmpeg)...");
                    XerahS.RegionCapture.ScreenRecording.ScreenRecorderService.FallbackServiceFactory =
                        () => new XerahS.RegionCapture.ScreenRecording.FFmpegRecordingService();
                }

                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", "=== WindowsPlatform.InitializeRecording() COMPLETE ===");
            }
            catch (Exception ex)
            {
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", $"✗ EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "INIT", $"Stack trace: {ex.StackTrace}");
                DebugHelper.WriteException(ex, "Failed to initialize recording support");
            }
        }
    }
}
