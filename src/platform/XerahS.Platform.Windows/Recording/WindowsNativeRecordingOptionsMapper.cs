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

using System.Drawing;
using XerahS.Platform.Windows.Recording.Native;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Platform.Windows.Recording;

/// <summary>
/// Translates XerahS recording options into the native Windows recorder's options.
/// Coordinates are physical virtual-desktop pixels, the same space the region selector produces.
/// </summary>
internal static class WindowsNativeRecordingOptionsMapper
{
    internal const int AudioBitrate = 192_000;

    /// <param name="options">The XerahS request.</param>
    /// <param name="outputPath">Resolved absolute .mp4 path.</param>
    /// <param name="getWindowBounds">Returns the window's visible bounds, or null when unavailable.</param>
    /// <param name="getPrimaryMonitorBounds">Returns the primary display's bounds.</param>
    /// <param name="getMonitorBounds">Returns the bounds of the display that contains a point.</param>
    /// <param name="resolveMicrophone">Maps a stored microphone ID or name to a WASAPI endpoint ID (null = default device).</param>
    internal static NativeRecordingOptions Map(
        RecordingOptions options,
        string outputPath,
        Func<IntPtr, Rectangle?> getWindowBounds,
        Func<Rectangle> getPrimaryMonitorBounds,
        Func<Point, Rectangle?> getMonitorBounds,
        Func<string?, string?>? resolveMicrophone = null,
        Func<string?, string?>? resolveSystemAudio = null,
        Func<string, Rectangle?>? getDisplayBounds = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ScreenRecordingSettings settings = options.Settings ?? new ScreenRecordingSettings();

        if (settings.Codec != VideoCodec.H264)
        {
            throw new PlatformNotSupportedException(
                $"Native Windows recording writes H.264/AAC MP4 only. {settings.Codec} must use the FFmpeg recording backend.");
        }

        nint windowHandle = 0;
        nint trackWindowHandle = 0;
        Rectangle region;

        switch (options.Mode)
        {
            case CaptureMode.Screen:
                region = string.IsNullOrWhiteSpace(settings.DisplayId)
                    ? ResolveScreenBounds(options.Region, getPrimaryMonitorBounds, getMonitorBounds)
                    : getDisplayBounds?.Invoke(settings.DisplayId)
                        ?? throw new ArgumentException("The selected recording display is no longer available.", nameof(options));
                break;

            case CaptureMode.Region:
                if (options.Region.Width < 2 || options.Region.Height < 2)
                {
                    throw new ArgumentException("Region mode requires a region of at least 2 x 2 pixels.", nameof(options));
                }
                region = options.Region;
                break;

            case CaptureMode.Window:
                if (options.TargetWindowHandle == IntPtr.Zero)
                {
                    throw new ArgumentException("TargetWindowHandle must be specified for Window mode", nameof(options));
                }

                if (settings.WindowRecordingMode == WindowRecordingMode.WindowOnly)
                {
                    // Direct HWND capture: follows the window and works while it is covered, but excludes popups.
                    windowHandle = options.TargetWindowHandle;
                    region = Rectangle.Empty;
                }
                else
                {
                    // Desktop capture that follows the window, so menus, dialogs and tooltips (separate HWNDs) are included.
                    region = getWindowBounds(options.TargetWindowHandle)
                        ?? throw new ArgumentException("The target window's bounds could not be read.", nameof(options));
                    trackWindowHandle = options.TargetWindowHandle;
                }
                break;

            default:
                throw new NotSupportedException($"Capture mode {options.Mode} not supported");
        }

        string? microphoneId = settings.CaptureMicrophone
            ? resolveMicrophone != null ? resolveMicrophone(settings.MicrophoneDeviceId) : settings.MicrophoneDeviceId
            : null;

        return new NativeRecordingOptions
        {
            OutputPath = outputPath,
            Region = region,
            WindowHandle = windowHandle,
            TrackWindowHandle = trackWindowHandle,
            FramesPerSecond = Math.Clamp(settings.FPS, 1, 240),
            VideoBitrate = (int)Math.Clamp((long)settings.BitrateKbps * 1000, 100_000, 200_000_000),
            AudioBitrate = AudioBitrate,
            IncludeCursor = settings.ShowCursor,
            CaptureSystemAudio = settings.CaptureSystemAudio,
            CaptureMicrophone = settings.CaptureMicrophone,
            MicrophoneDeviceId = microphoneId,
            SystemAudioDeviceId = settings.CaptureSystemAudio
                ? resolveSystemAudio != null ? resolveSystemAudio(settings.SystemAudioDeviceId) : settings.SystemAudioDeviceId
                : null,
            SystemAudioGain = settings.SystemAudioGain,
            MicrophoneGain = settings.MicrophoneGain,
            RequireHardwareEncoder = settings.RequireHardwareEncoder,
            LowLatency = settings.LowLatency,
            HideCaptureBorder = settings.HideCaptureBorder
        };
    }

    /// <summary>
    /// Screen mode records one whole display: the display containing the requested area when one is given,
    /// otherwise the primary display (the previous behaviour).
    /// </summary>
    internal static Rectangle ResolveScreenBounds(Rectangle requested, Func<Rectangle> getPrimaryMonitorBounds, Func<Point, Rectangle?> getMonitorBounds)
    {
        if (requested.Width >= 2 && requested.Height >= 2)
        {
            Point center = new(requested.X + requested.Width / 2, requested.Y + requested.Height / 2);
            if (getMonitorBounds(center) is Rectangle monitor)
            {
                return monitor;
            }
        }

        return getPrimaryMonitorBounds();
    }
}
