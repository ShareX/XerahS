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

namespace XerahS.Platform.Windows.Recording.Native;

/// <summary>Native H.264/AAC MP4 recording. Coordinates are physical desktop pixels.</summary>
public sealed record NativeRecordingOptions
{
    public required string OutputPath { get; init; }
    public Rectangle Region { get; init; }
    /// <summary>When nonzero, capture this window (including when occluded) instead of Region.</summary>
    public nint WindowHandle { get; init; }
    /// <summary>
    /// When nonzero (and WindowHandle is zero), Region follows this window's position on the desktop.
    /// Desktop capture of the window area includes its menus, tooltips and other popup windows.
    /// </summary>
    public nint TrackWindowHandle { get; init; }
    public int FramesPerSecond { get; init; } = 30;
    public int VideoBitrate { get; init; } = 8_000_000;
    public int AudioBitrate { get; init; } = 192_000;
    public bool IncludeCursor { get; init; } = true;
    public bool CaptureSystemAudio { get; init; } = true;
    public bool CaptureMicrophone { get; init; }
    /// <summary>WASAPI endpoint ID; null selects the default multimedia render endpoint.</summary>
    public string? SystemAudioDeviceId { get; init; }
    /// <summary>WASAPI endpoint ID; null selects the default communications capture endpoint.</summary>
    public string? MicrophoneDeviceId { get; init; }
    public float SystemAudioGain { get; init; } = 1;
    public float MicrophoneGain { get; init; } = 1;
    /// <summary>Reject a software video encoder rather than silently increasing CPU usage.</summary>
    public bool RequireHardwareEncoder { get; init; } = true;
    public bool LowLatency { get; init; } = true;
    public bool HideCaptureBorder { get; init; } = true;
    public TimeSpan Duration { get; init; }

    internal bool HasAudio => CaptureSystemAudio || CaptureMicrophone;

    internal void ValidateEncoder(VideoEncoderInfo encoder)
    {
        if (RequireHardwareEncoder && (!encoder.IsHardwareAccelerated || !encoder.IsD3D11Aware))
            throw new NotSupportedException($"Windows selected '{encoder.Name}', which does not provide D3D11 hardware video encoding on this GPU. Disable RequireHardwareEncoder to allow Windows software encoding.");
    }

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(OutputPath);
        if (!Path.GetExtension(OutputPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Native recording writes H.264/AAC MP4 files.", nameof(OutputPath));
        if (WindowHandle == 0 && (Region.Width < 2 || Region.Height < 2))
            throw new ArgumentException("Select a window or a region of at least 2 × 2 pixels.", nameof(Region));
        if (FramesPerSecond is < 1 or > 240) throw new ArgumentOutOfRangeException(nameof(FramesPerSecond));
        if (VideoBitrate is < 100_000 or > 200_000_000) throw new ArgumentOutOfRangeException(nameof(VideoBitrate));
        if (AudioBitrate is not (96_000 or 128_000 or 160_000 or 192_000)) throw new ArgumentOutOfRangeException(nameof(AudioBitrate));
        if (Duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(Duration));
        if (!float.IsFinite(SystemAudioGain) || SystemAudioGain is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(SystemAudioGain));
        if (!float.IsFinite(MicrophoneGain) || MicrophoneGain is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(MicrophoneGain));
    }
}

public sealed record VideoEncoderInfo(string Name, bool IsHardwareAccelerated, bool IsD3D11Aware);

public sealed record NativeRecordingResult(string OutputPath, TimeSpan Duration, long VideoFrames,
    long DroppedVideoFrames, long AudioDiscontinuities, VideoEncoderInfo Encoder);
