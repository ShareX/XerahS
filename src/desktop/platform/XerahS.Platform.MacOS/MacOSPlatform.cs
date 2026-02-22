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

using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.MacOS.Services;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Platform.MacOS
{
    /// <summary>
    /// Initializes macOS platform services
    /// </summary>
    public static class MacOSPlatform
    {
        /// <summary>
        /// Initializes all macOS platform services
        /// </summary>
        public static void Initialize(IScreenCaptureService? screenCaptureService = null)
        {
            var screenService = new MacOSScreenService();

            if (screenCaptureService == null)
            {
                // Use native ScreenCaptureKit service which includes automatic fallback to CLI
                screenCaptureService = new MacOSScreenCaptureKitService();
                DebugHelper.WriteLine("macOS: Using MacOSScreenCaptureKitService (native ScreenCaptureKit with CLI fallback)");
            }

            PlatformServices.Initialize(
                platformInfo: new MacOSPlatformInfo(),
                screenService: screenService,
                clipboardService: new MacOSClipboardService(),
                windowService: new MacOSWindowService(),
                screenCaptureService: screenCaptureService,
                hotkeyService: new MacOSHotkeyService(),
                inputService: new MacOSInputService(),
                fontService: new MacOSFontService(),
                startupService: new UnsupportedStartupService(),
                systemService: new MacOSSystemService(),
                notificationService: new MacOSNotificationService(),
                diagnosticService: new Services.MacOSDiagnosticService(),
                watchFolderDaemonService: new MacOSWatchFolderDaemonService()
            );

            // Register OCR service stub (Apple Vision framework integration planned)
            PlatformServices.Ocr = new MacOSOcrService();
        }

        /// <summary>
        /// Initialize screen recording for macOS.
        /// Uses native ScreenCaptureKit when available (macOS 12.3+), with FFmpeg fallback.
        /// </summary>
        public static void InitializeRecording()
        {
            try
            {
                // Check if native ScreenCaptureKit recording is available
                if (MacOSNativeRecordingService.IsAvailable())
                {
                    DebugHelper.WriteLine("macOS: Native ScreenCaptureKit recording available");
                    
                    // Set up factories for ScreenRecordingManager to create native recording service
                    // The manager will use this as the primary option
                    ScreenRecorderService.CaptureSourceFactory = null; // Not using capture/encoder pattern
                    ScreenRecorderService.EncoderFactory = null;
                    
                    // Register native service factory (this is the key change!)
                    ScreenRecorderService.NativeRecordingServiceFactory = () => new MacOSNativeRecordingService();
                    
                    // Also register FFmpeg as fallback
                    ScreenRecorderService.FallbackServiceFactory = () => new FFmpegRecordingService();
                    
                    DebugHelper.WriteLine("macOS: Screen recording initialized successfully");
                    DebugHelper.WriteLine("  - Primary: Native ScreenCaptureKit (AVAssetWriter)");
                    DebugHelper.WriteLine("  - Fallback: FFmpeg (avfoundation)");
                    DebugHelper.WriteLine("  - Output format: MP4 (H.264)");
                    DebugHelper.WriteLine("  - Supported modes: Screen, Region");
                    
                    Console.WriteLine("[MacOSPlatform] Native ScreenCaptureKit recording initialized");
                }
                else
                {
                    // Fallback to FFmpeg-only
                    DebugHelper.WriteLine("macOS: Native ScreenCaptureKit not available, using FFmpeg");
                    
                    ScreenRecorderService.FallbackServiceFactory = () => new FFmpegRecordingService();
                    
                    DebugHelper.WriteLine("macOS: Screen recording initialized with FFmpeg backend");
                    DebugHelper.WriteLine("  - Recording backend: FFmpeg (avfoundation)");
                    DebugHelper.WriteLine("  - Supported modes: Screen, Window, Region");
                    DebugHelper.WriteLine("  - Codecs: H.264, HEVC, VP9, AV1");
                    
                    Console.WriteLine("[MacOSPlatform] FFmpeg recording initialized (native unavailable)");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to initialize macOS screen recording");
                Console.WriteLine($"[MacOSPlatform] Recording init failed: {ex.Message}");
            }
        }
    }
}
