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

namespace XerahS.ScreenCapture.ScreenRecording;

/// <summary>
/// Main interface for screen recording services
/// Implementations: ScreenRecorderService (native), FFmpegRecordingService (fallback)
/// Stage 5: Added IDisposable for proper resource cleanup
/// </summary>
public interface IRecordingService : IDisposable
{
    /// <summary>
    /// Start a new recording session
    /// Note: CancellationToken support deferred to future optimization
    /// </summary>
    /// <param name="options">Recording configuration</param>
    /// <returns>Task that completes when recording has started</returns>
    Task StartRecordingAsync(RecordingOptions options);

    /// <summary>
    /// Stop the current recording session
    /// </summary>
    /// <returns>Task that completes when recording has stopped and file is finalized</returns>
    Task StopRecordingAsync();

    /// <summary>
    /// Fired when an error occurs during recording
    /// UI should handle fatal errors by stopping recording
    /// </summary>
    event EventHandler<RecordingErrorEventArgs> ErrorOccurred;

    /// <summary>
    /// Fired when recording status changes (Idle -> Initializing -> Recording -> Finalizing -> Idle)
    /// </summary>
    event EventHandler<RecordingStatusEventArgs> StatusChanged;
}

/// <summary>
/// Platform-specific capture source interface
/// Windows: WindowsGraphicsCaptureSource (WGC)
/// Linux: PipeWireCaptureSource (XDG Portal)
/// macOS: ScreenCaptureKitSource
/// </summary>
public interface ICaptureSource : IDisposable
{
    /// <summary>
    /// Begin capturing frames
    /// </summary>
    Task StartCaptureAsync();

    /// <summary>
    /// Stop capturing frames
    /// </summary>
    Task StopCaptureAsync();

    /// <summary>
    /// Fired when a new frame is captured
    /// Threading: May be raised on capture thread - encoder must marshal if needed
    /// </summary>
    event EventHandler<FrameArrivedEventArgs> FrameArrived;
}

/// <summary>
/// Platform-specific video encoder interface
/// Windows: MediaFoundationEncoder (IMFSinkWriter)
/// Fallback: FFmpegPipeEncoder
/// </summary>
public interface IVideoEncoder : IDisposable
{
    /// <summary>
    /// Initialize the encoder with format and output path
    /// </summary>
    /// <param name="format">Video format configuration</param>
    /// <param name="outputPath">Output file path</param>
    void Initialize(VideoFormat format, string outputPath);

    /// <summary>
    /// Write a single frame to the encoder
    /// </summary>
    /// <param name="frame">Frame data to encode</param>
    void WriteFrame(FrameData frame);

    /// <summary>
    /// Finalize encoding and close the file
    /// </summary>
    void Finalize();
}

/// <summary>
/// Audio capture interface (Stage 6)
/// Windows: WasapiAudioCapture
/// Linux: PulseAudio
/// macOS: CoreAudio
/// </summary>
public interface IAudioCapture : IDisposable
{
    /// <summary>
    /// Start capturing audio
    /// </summary>
    void Start();

    /// <summary>
    /// Stop capturing audio
    /// </summary>
    void Stop();

    /// <summary>
    /// Fired when audio data is available
    /// </summary>
    event EventHandler<AudioBufferEventArgs> AudioDataAvailable;
}
