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
using System.IO;
using System.Runtime.InteropServices;
using XerahS.Common;
using XerahS.Platform.Windows.Recording.Native;
using XerahS.RegionCapture.ScreenRecording;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace XerahS.Platform.Windows.Recording;

/// <summary>
/// GPU-resident native Windows recorder: Windows.Graphics.Capture frames stay on the GPU through cropping,
/// multi-monitor stitching and BT.709 NV12 conversion, then go to the Media Foundation H.264 encoder through
/// a DXGI device manager. System audio (WASAPI loopback) and microphone are mixed and encoded as AAC on the
/// same QPC clock as the video. Supports true pause/resume without restarting the session.
/// The engine is ported from ShareX.ScreenRecordingLib.
/// </summary>
public sealed class WindowsNativeRecordingService : IRecordingService, IMovableRecordingService, IRecordingCompletionSource
{
    private const string LogCategory = "ScreenRecorder";
    private const string LogTag = "NATIVE_WIN";

    /// <summary>Free-threaded frame pools and cursor control need Windows 10 2004 (build 19041).</summary>
    public const int MinimumWindowsBuild = 19041;

    private readonly object _lock = new();
    private NativeScreenRecorder? _recorder;
    private RecordingStatus _status = RecordingStatus.Idle;
    private bool _stopRequested;
    private bool _disposed;
    private TimeSpan _lastElapsed;

    public event EventHandler<RecordingErrorEventArgs>? ErrorOccurred;
    public event EventHandler<RecordingStatusEventArgs>? StatusChanged;
    public event EventHandler? RecordingEnded;

    /// <summary>Whether this Windows build and session can run the GPU recorder.</summary>
    public static bool IsSupported
    {
        get
        {
            try
            {
                Version version = Environment.OSVersion.Version;
                if (version.Major < 10 || version.Build < MinimumWindowsBuild)
                {
                    TroubleshootingHelper.Log(LogCategory, LogTag, $"OS build {version.Build} < {MinimumWindowsBuild}; GPU recorder unavailable");
                    return false;
                }

                bool supported = global::Windows.Graphics.Capture.GraphicsCaptureSession.IsSupported();
                TroubleshootingHelper.Log(LogCategory, LogTag, $"GraphicsCaptureSession.IsSupported() = {supported}");
                return supported;
            }
            catch (Exception ex)
            {
                TroubleshootingHelper.Log(LogCategory, LogTag, $"Support check failed: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }
    }

    public RecordingRuntimeCapabilities GetCapabilities(RecordingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new RecordingRuntimeCapabilities(RecordingPauseBehavior.NativePauseResume,
            SupportsRegionMove: options.Mode == CaptureMode.Region);
    }

    public async Task StartRecordingAsync(RecordingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_status != RecordingStatus.Idle)
            {
                throw new InvalidOperationException("Recording already in progress");
            }

            _stopRequested = false;
            _lastElapsed = TimeSpan.Zero;
            SetStatus(RecordingStatus.Initializing);
        }

        NativeScreenRecorder? recorder = null;
        try
        {
            string outputPath = ResolveOutputPath(options);
            NativeRecordingOptions nativeOptions = WindowsNativeRecordingOptionsMapper.Map(
                options,
                outputPath,
                GetWindowBounds,
                GetPrimaryMonitorBounds,
                GetMonitorBounds,
                ResolveMicrophoneId,
                ResolveSystemAudioId,
                id => XerahS.Platform.Abstractions.PlatformServices.Screen.GetAllScreens()
                    .FirstOrDefault(screen => string.Equals(screen.DeviceName, id, StringComparison.OrdinalIgnoreCase))?.Bounds);

            TroubleshootingHelper.Log(LogCategory, LogTag,
                $"Starting: mode={options.Mode}, region={nativeOptions.Region}, hwnd=0x{nativeOptions.WindowHandle:X}, " +
                $"trackHwnd=0x{nativeOptions.TrackWindowHandle:X}, fps={nativeOptions.FramesPerSecond}, bitrate={nativeOptions.VideoBitrate}, " +
                $"systemAudio={nativeOptions.CaptureSystemAudio}, mic={nativeOptions.CaptureMicrophone} ({nativeOptions.MicrophoneDeviceId ?? "default"}), " +
                $"cursor={nativeOptions.IncludeCursor}, requireHardware={nativeOptions.RequireHardwareEncoder}, output={outputPath}");

            recorder = new NativeScreenRecorder(nativeOptions);
            recorder.Diagnostic += message => TroubleshootingHelper.Log(LogCategory, LogTag, message);
            recorder.AudioCaptureFailed += error => ErrorOccurred?.Invoke(this,
                new RecordingErrorEventArgs(new IOException("Audio capture stopped; video recording continues with silence.", error), false));
            lock (_lock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _recorder = recorder;
            }

            await recorder.StartAsync().ConfigureAwait(false);

            if (recorder.Encoder is VideoEncoderInfo encoder)
            {
                TroubleshootingHelper.Log(LogCategory, LogTag,
                    $"Encoder: {encoder.Name} (hardware={encoder.IsHardwareAccelerated}, d3d11={encoder.IsD3D11Aware})");
                if (!encoder.IsHardwareAccelerated)
                {
                    DebugHelper.WriteLine($"[WindowsNativeRecording] Using software H.264 encoder '{encoder.Name}'; CPU usage will be higher.");
                }
            }

            lock (_lock)
            {
                if (!_disposed && !_stopRequested) SetStatus(RecordingStatus.Recording);
            }

            _ = recorder.Completion.ContinueWith(OnRecorderCompleted, recorder, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            TroubleshootingHelper.Log(LogCategory, LogTag, $"Start failed: {ex.GetType().Name}: {ex.Message}");
            DebugHelper.WriteException(ex, "WindowsNativeRecordingService: start failed");

            if (recorder != null)
            {
                // Disposing joins the worker, which deletes the output file it created.
                try { await recorder.DisposeAsync().ConfigureAwait(false); }
                catch (Exception cleanupEx) { DebugHelper.WriteException(cleanupEx, "WindowsNativeRecordingService: cleanup failed"); }
            }

            lock (_lock)
            {
                _recorder = null;
                SetStatus(RecordingStatus.Idle);
            }

            // Invalid requests are the caller's problem; everything else is a native-path failure that
            // ScreenRecordingManager may retry with the FFmpeg fallback (it recognises these two types).
            if (ex is ArgumentException or PlatformNotSupportedException or COMException)
            {
                throw;
            }

            throw new PlatformNotSupportedException($"Native Windows recording could not start: {ex.Message}", ex);
        }
    }

    public async Task StopRecordingAsync()
    {
        NativeScreenRecorder? recorder;
        lock (_lock)
        {
            recorder = _recorder;
            if (recorder == null || _status == RecordingStatus.Idle || _stopRequested)
            {
                return;
            }

            _stopRequested = true;
            SetStatus(RecordingStatus.Finalizing);
        }

        try
        {
            NativeRecordingResult result = await recorder.StopAsync().ConfigureAwait(false);
            TroubleshootingHelper.Log(LogCategory, LogTag,
                $"Finalized {result.OutputPath}: duration={result.Duration.TotalSeconds:F2}s, frames={result.VideoFrames}, " +
                $"dropped={result.DroppedVideoFrames}, audioDiscontinuities={result.AudioDiscontinuities}, encoder={result.Encoder.Name}");
        }
        catch (Exception ex)
        {
            // The worker has already removed its incomplete output.
            TroubleshootingHelper.Log(LogCategory, LogTag, $"Stop failed: {ex.GetType().Name}: {ex.Message}");
            ErrorOccurred?.Invoke(this, new RecordingErrorEventArgs(ex, false));
            throw;
        }
        finally
        {
            recorder.Dispose();
            lock (_lock)
            {
                _lastElapsed = recorder.Elapsed;
                _recorder = null;
                SetStatus(RecordingStatus.Idle);
            }
        }
    }

    public Task PauseRecordingAsync()
    {
        lock (_lock)
        {
            if (_recorder == null || _status != RecordingStatus.Recording) return Task.CompletedTask;
            _recorder.Pause();
            SetStatus(RecordingStatus.Paused);
        }

        TroubleshootingHelper.Log(LogCategory, LogTag, "Paused");
        return Task.CompletedTask;
    }

    public Task ResumeRecordingAsync()
    {
        lock (_lock)
        {
            if (_recorder == null || _status != RecordingStatus.Paused) return Task.CompletedTask;
            _recorder.Resume();
            SetStatus(RecordingStatus.Recording);
        }

        TroubleshootingHelper.Log(LogCategory, LogTag, "Resumed");
        return Task.CompletedTask;
    }

    public Task ResumeRecordingAsync(Rectangle region)
    {
        lock (_lock)
        {
            if (_recorder == null || _status != RecordingStatus.Paused) return Task.CompletedTask;
            Rectangle normalized = RecordingRegionLayout.Normalize(region);
            if (RecordingRegionLayout.Compute(normalized, GraphicsCapture.EnumerateMonitors()).Count == 0)
                throw new ArgumentException("The moved recording region does not intersect a display.", nameof(region));
            _recorder.Resume(region);
            SetStatus(RecordingStatus.Recording);
        }
        return Task.CompletedTask;
    }

    private void OnRecorderCompleted(Task<NativeRecordingResult> completion, object? state)
    {
        bool expected;
        lock (_lock)
        {
            expected = _stopRequested || _disposed || !ReferenceEquals(state, _recorder);
        }

        if (expected) return;

        if (completion.IsFaulted)
        {
            Exception error = completion.Exception!.GetBaseException();
            TroubleshootingHelper.Log(LogCategory, LogTag, $"Recording failed: {error.GetType().Name}: {error.Message}");
            lock (_lock)
            {
                SetStatus(RecordingStatus.Error);
            }
            ErrorOccurred?.Invoke(this, new RecordingErrorEventArgs(error, true));
        }
        else if (completion.IsCompletedSuccessfully)
        {
            // The target window closed or changed size. The MP4 is already finalized and is kept; stopping returns it.
            TroubleshootingHelper.Log(LogCategory, LogTag, "Capture target ended; the recording was finalized early.");
            lock (_lock)
            {
                SetStatus(RecordingStatus.Finalizing);
            }
            RecordingEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetStatus(RecordingStatus status)
    {
        if (_status == status) return;
        _status = status;
        TimeSpan elapsed = _recorder?.Elapsed ?? _lastElapsed;
        StatusChanged?.Invoke(this, new RecordingStatusEventArgs(status, elapsed));
    }

    private static string ResolveOutputPath(RecordingOptions options)
    {
        string outputPath;
        if (!string.IsNullOrEmpty(options.OutputPath))
        {
            outputPath = Path.GetFullPath(options.OutputPath);
        }
        else
        {
            outputPath = Path.Combine(PathsManager.ScreencastsFolder, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4");
        }

        if (!Path.GetExtension(outputPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlatformNotSupportedException("Native Windows recording writes MP4 files only.");
        }

        return outputPath;
    }

    private static Rectangle? GetWindowBounds(IntPtr hwnd) =>
        RecordingInterop.TryGetWindowBounds(hwnd, out Rectangle bounds) ? bounds : null;

    private static Rectangle GetPrimaryMonitorBounds()
    {
        var monitors = GraphicsCapture.EnumerateMonitors();
        // The primary display always has its origin at (0, 0) in virtual-desktop coordinates.
        foreach ((_, Rectangle bounds) in monitors)
        {
            if (bounds.Contains(0, 0)) return bounds;
        }

        return monitors.Count > 0 ? monitors[0].Bounds : throw new PlatformNotSupportedException("No display is available for recording.");
    }

    private static Rectangle? GetMonitorBounds(Point point)
    {
        foreach ((_, Rectangle bounds) in GraphicsCapture.EnumerateMonitors())
        {
            if (bounds.Contains(point)) return bounds;
        }

        return null;
    }

    /// <summary>
    /// The stored microphone setting may be a WASAPI endpoint ID or a device name (the FFmpeg backend uses
    /// DirectShow names). An unavailable selection must not silently record a different microphone.
    /// </summary>
    internal static string? ResolveMicrophoneId(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;

        AudioCaptureDevice? match = MatchAudioDevice(AudioCaptureDevices.GetMicrophones(), stored);
        return match?.Id ?? throw new ArgumentException("The selected microphone is no longer available.", nameof(stored));
    }

    internal static string? ResolveSystemAudioId(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;
        AudioCaptureDevice? match = MatchAudioDevice(AudioCaptureDevices.GetSystemAudioDevices(), stored);
        return match?.Id ?? throw new ArgumentException("The selected system audio device is no longer available.", nameof(stored));
    }

    internal static AudioCaptureDevice? MatchAudioDevice(IEnumerable<AudioCaptureDevice> devices, string stored)
    {
        List<AudioCaptureDevice> list = devices.ToList();
        return list.FirstOrDefault(d => string.Equals(d.Id, stored, StringComparison.OrdinalIgnoreCase))
            ?? list.FirstOrDefault(d => string.Equals(d.Name, stored, StringComparison.OrdinalIgnoreCase))
            ?? list.FirstOrDefault(d => d.Name.Contains(stored, StringComparison.OrdinalIgnoreCase) ||
                                        stored.Contains(d.Name, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        NativeScreenRecorder? recorder;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            recorder = _recorder;
            _recorder = null;
        }

        // Stops and joins the worker. An unfinished session's partial file is deleted by the worker;
        // a session that was recording is finalized so the user does not lose it.
        recorder?.Dispose();
    }
}
