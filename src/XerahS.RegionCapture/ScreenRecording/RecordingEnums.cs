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

namespace XerahS.RegionCapture.ScreenRecording;

/// <summary>
/// Defines the capture mode for screen recording
/// </summary>
public enum CaptureMode
{
    /// <summary>Capture the entire screen</summary>
    Screen,

    /// <summary>Capture a specific window</summary>
    Window,

    /// <summary>Capture a specific region</summary>
    Region
}

/// <summary>
/// Represents the current state of the recording process
/// </summary>
public enum RecordingStatus
{
    /// <summary>No recording in progress</summary>
    Idle,

    /// <summary>Initializing capture source and encoder</summary>
    Initializing,

    /// <summary>Actively recording frames</summary>
    Recording,

    /// <summary>Recording paused (Stage 6+)</summary>
    Paused,

    /// <summary>Finalizing and encoding the video</summary>
    Finalizing,

    /// <summary>Error state - recording failed</summary>
    Error
}

/// <summary>
/// Supported video codec types
/// Note: Stage 1 only implements H264
/// </summary>
public enum VideoCodec
{
    /// <summary>H.264 (AVC) - Stage 1 MVP</summary>
    H264,

    /// <summary>H.265 (HEVC) - Future</summary>
    HEVC,

    /// <summary>VP9 - Future</summary>
    VP9,

    /// <summary>AV1 - Future</summary>
    AV1
}

/// <summary>
/// Pixel format for frame data
/// </summary>
public enum PixelFormat
{
    /// <summary>32-bit BGRA format (8 bits per channel)</summary>
    Bgra32,

    /// <summary>NV12 format (YUV 4:2:0)</summary>
    Nv12,

    /// <summary>32-bit RGBA format (8 bits per channel)</summary>
    Rgba32,

    /// <summary>Unknown or unsupported format</summary>
    Unknown
}

/// <summary>
/// Intent of the recording (Game vs Default)
/// </summary>
public enum RecordingIntent
{
    Default,
    Game
}

/// <summary>
/// Backend technology for screen recording
/// </summary>
public enum RecordingBackend
{
    Default,
    GDI,
    Modern
}
