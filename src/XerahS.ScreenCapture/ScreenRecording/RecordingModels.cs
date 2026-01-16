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

using System.Drawing;

namespace XerahS.ScreenCapture.ScreenRecording;

/// <summary>
/// Configuration options for starting a recording session
/// </summary>
public class RecordingOptions
{
    /// <summary>
    /// The capture mode (Screen, Window, or Region)
    /// </summary>
    public CaptureMode Mode { get; set; } = CaptureMode.Screen;

    /// <summary>
    /// Target window handle for Window mode
    /// Platform-specific: Windows (HWND), Linux (XID), macOS (WindowID cast to IntPtr)
    /// Future refactor may introduce a typed WindowId struct if needed.
    /// </summary>
    public IntPtr TargetWindowHandle { get; set; }

    /// <summary>
    /// Capture region for Region mode
    /// Uses System.Drawing.Rectangle for cross-platform compatibility
    /// </summary>
    public Rectangle Region { get; set; }

    /// <summary>
    /// Output file path for the recorded video
    /// If null/empty, PlatformManager.GetDefaultOutputPath() acts as fallback
    /// Default Pattern: "ShareX/Screenshots/yyyy-MM/Date_Time.mp4"
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Reference to recording settings from configuration
    /// </summary>
    public ScreenRecordingSettings? Settings { get; set; }
}

/// <summary>
/// Persistent screen recording settings
/// Serialized in WorkflowsConfig.json and ApplicationConfig.json
/// </summary>
public class ScreenRecordingSettings
{
    /// <summary>Video codec to use for encoding</summary>
    public VideoCodec Codec { get; set; } = VideoCodec.H264;

    /// <summary>Frames per second</summary>
    public int FPS { get; set; } = 30;

    /// <summary>Video bitrate in kilobits per second</summary>
    public int BitrateKbps { get; set; } = 4000;

    /// <summary>Capture system audio output</summary>
    public bool CaptureSystemAudio { get; set; } = false;

    /// <summary>Capture microphone input</summary>
    public bool CaptureMicrophone { get; set; } = false;

    /// <summary>Microphone device ID for audio capture</summary>
    public string? MicrophoneDeviceId { get; set; }

    /// <summary>
    /// Capture mouse cursor in recording
    /// Stage 2: Window & Region Parity
    /// </summary>
    public bool ShowCursor { get; set; } = true;

    /// <summary>
    /// Force FFmpeg mode instead of native recording
    /// Trigger condition for Stage 4 FFmpeg fallback
    /// </summary>
    public bool ForceFFmpeg { get; set; } = false;

    /// <summary>
    /// Intent of the recording
    /// </summary>
    public RecordingIntent RecordingIntent { get; set; } = RecordingIntent.Default;

    /// <summary>
    /// Backend technology for screen recording
    /// </summary>
    public RecordingBackend RecordingBackend { get; set; } = RecordingBackend.Default;
}

/// <summary>
/// Represents raw frame data from capture source
/// </summary>
public readonly struct FrameData
{
    /// <summary>Pointer to raw pixel data</summary>
    public IntPtr DataPtr { get; init; }

    /// <summary>Stride (bytes per row) of the frame</summary>
    public int Stride { get; init; }

    /// <summary>Frame width in pixels</summary>
    public int Width { get; init; }

    /// <summary>Frame height in pixels</summary>
    public int Height { get; init; }

    /// <summary>
    /// Frame timestamp in 100-nanosecond units (compatible with Media Foundation)
    /// </summary>
    public long Timestamp { get; init; }

    /// <summary>Pixel format of the frame data</summary>
    public PixelFormat Format { get; init; }
}

/// <summary>
/// Video format configuration for encoder
/// </summary>
public class VideoFormat
{
    /// <summary>Video width in pixels</summary>
    public int Width { get; set; }

    /// <summary>Video height in pixels</summary>
    public int Height { get; set; }

    /// <summary>Bitrate in bits per second (not kbps)</summary>
    public int Bitrate { get; set; }

    /// <summary>Frames per second</summary>
    public int FPS { get; set; }

    /// <summary>Video codec</summary>
    public VideoCodec Codec { get; set; } = VideoCodec.H264;
}

/// <summary>
/// Event arguments for recording errors
/// </summary>
public class RecordingErrorEventArgs : EventArgs
{
    /// <summary>The error that occurred</summary>
    public Exception Error { get; }

    /// <summary>
    /// Indicates if the error is fatal (recording must stop)
    /// Fatal errors: encoding failure, driver crash
    /// Non-fatal: dropped frames, performance warnings
    /// </summary>
    public bool IsFatal { get; }

    public RecordingErrorEventArgs(Exception error, bool isFatal)
    {
        Error = error;
        IsFatal = isFatal;
    }
}

/// <summary>
/// Event arguments for recording status changes
/// </summary>
public class RecordingStatusEventArgs : EventArgs
{
    /// <summary>Current recording status</summary>
    public RecordingStatus Status { get; }

    /// <summary>Current recording duration</summary>
    public TimeSpan Duration { get; }

    public RecordingStatusEventArgs(RecordingStatus status, TimeSpan duration)
    {
        Status = status;
        Duration = duration;
    }
}

/// <summary>
/// Event arguments for captured frame notifications
/// </summary>
public class FrameArrivedEventArgs : EventArgs
{
    /// <summary>The captured frame data</summary>
    public FrameData Frame { get; }

    public FrameArrivedEventArgs(FrameData frame)
    {
        Frame = frame;
    }
}

/// <summary>
/// Event arguments for audio buffer notifications (Stage 6)
/// </summary>
public class AudioBufferEventArgs : EventArgs
{
    /// <summary>Audio data buffer</summary>
    public byte[] Buffer { get; }

    /// <summary>Number of bytes recorded in this buffer</summary>
    public int BytesRecorded { get; }

    /// <summary>Timestamp in 100-nanosecond units</summary>
    public long Timestamp { get; }

    public AudioBufferEventArgs(byte[] buffer, int bytesRecorded, long timestamp)
    {
        Buffer = buffer;
        BytesRecorded = bytesRecorded;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Event arguments for recording started notification
/// Includes information about the recording method being used
/// </summary>
public class RecordingStartedEventArgs : EventArgs
{
    /// <summary>True if using FFmpeg fallback, false if using native Modern Capture</summary>
    public bool IsUsingFallback { get; }

    /// <summary>Recording options being used</summary>
    public RecordingOptions Options { get; }

    public RecordingStartedEventArgs(bool isUsingFallback, RecordingOptions options)
    {
        IsUsingFallback = isUsingFallback;
        Options = options;
    }
}
