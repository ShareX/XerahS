#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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
using XerahS.Platform.MacOS.Services;
using XerahS.ScreenCapture.ScreenRecording;

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
                systemService: new MacOSSystemService()
            );
        }

        /// <summary>
        /// Initialize screen recording for macOS using FFmpeg-based recording
        /// Stage 7: Cross-platform recording support
        ///
        /// Note: This uses FFmpegRecordingService as the primary recording method.
        /// Future enhancement: Implement native ScreenCaptureKit capture source
        /// with AVFoundation encoder for hardware-accelerated recording.
        /// </summary>
        public static void InitializeRecording()
        {
            try
            {
                // macOS uses FFmpegRecordingService as the primary recording method
                // FFmpeg supports avfoundation input for screen capture on macOS
                DebugHelper.WriteLine("macOS: Initializing screen recording with FFmpeg backend");

                // Register FFmpegRecordingService factory
                // Note: FFmpegRecordingService is a complete recording service (not just capture/encoder)
                // so we don't use CaptureSourceFactory/EncoderFactory pattern here
                ScreenRecorderService.FallbackServiceFactory = () => new FFmpegRecordingService();

                DebugHelper.WriteLine("macOS: Screen recording initialized successfully");
                DebugHelper.WriteLine("  - Recording backend: FFmpeg (avfoundation)");
                DebugHelper.WriteLine("  - Supported modes: Screen, Window, Region");
                DebugHelper.WriteLine("  - Codecs: H.264, HEVC, VP9, AV1 (depends on FFmpeg build)");
                DebugHelper.WriteLine("  - Note: Native ScreenCaptureKit integration planned for future release");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to initialize macOS screen recording");
            }
        }
    }
}
