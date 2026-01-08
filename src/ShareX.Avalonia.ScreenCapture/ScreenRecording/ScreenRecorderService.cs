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
using System.Runtime.InteropServices;

using System.Diagnostics;

namespace XerahS.ScreenCapture.ScreenRecording;

/// <summary>
/// Platform-agnostic screen recording orchestration service
/// Coordinates ICaptureSource and IVideoEncoder
/// Stage 1: Native recording with automatic FFmpeg fallback
/// </summary>
public class ScreenRecorderService : IRecordingService
{
    private ICaptureSource? _captureSource;
    private IVideoEncoder? _encoder;
    private RecordingOptions? _currentOptions;
    private RecordingStatus _status = RecordingStatus.Idle;
    private readonly Stopwatch _stopwatch = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Factory function for creating platform-specific capture sources
    /// Set by PlatformManager during initialization
    /// </summary>
    public static Func<ICaptureSource>? CaptureSourceFactory { get; set; }

    /// <summary>
    /// Factory function for creating platform-specific encoders
    /// Set by PlatformManager during initialization
    /// </summary>
    public static Func<IVideoEncoder>? EncoderFactory { get; set; }

    /// <summary>
    /// Fallback factory for FFmpeg-based recording
    /// Set during platform initialization (Stage 4)
    /// </summary>
    public static Func<IRecordingService>? FallbackServiceFactory { get; set; }

    public event EventHandler<RecordingErrorEventArgs>? ErrorOccurred;
    public event EventHandler<RecordingStatusEventArgs>? StatusChanged;

    public async Task StartRecordingAsync(RecordingOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ScreenRecorderService));
            if (_status != RecordingStatus.Idle)
            {
                throw new InvalidOperationException("Recording already in progress");
            }

            _currentOptions = options;
            UpdateStatus(RecordingStatus.Initializing);
        }

        try
        {
            // Create capture source
            if (CaptureSourceFactory == null)
            {
                throw new InvalidOperationException("CaptureSourceFactory not set. Platform initialization missing.");
            }

            _captureSource = CaptureSourceFactory();

            // Initialize capture source based on mode
            await InitializeCaptureSource(options);

            // Create encoder
            if (EncoderFactory == null)
            {
                throw new InvalidOperationException("EncoderFactory not set. Platform initialization missing.");
            }

            _encoder = EncoderFactory();

            // Determine output path
            string outputPath = GetOutputPath(options);

            // Configure video format
            var videoFormat = new VideoFormat
            {
                Width = GetCaptureWidth(options),
                Height = GetCaptureHeight(options),
                FPS = options.Settings?.FPS ?? 30,
                Bitrate = (options.Settings?.BitrateKbps ?? 4000) * 1000, // Convert kbps to bps
                Codec = options.Settings?.Codec ?? VideoCodec.H264
            };

            // Initialize encoder
            _encoder.Initialize(videoFormat, outputPath);

            // Wire up frame capture
            _captureSource.FrameArrived += OnFrameCaptured;

            // Start capture
            await _captureSource.StartCaptureAsync();

            lock (_lock)
            {
                _stopwatch.Restart();
                UpdateStatus(RecordingStatus.Recording);
            }
        }
        catch (PlatformNotSupportedException ex)
        {
            // Stage 4: Trigger FFmpeg fallback for unsupported platforms
            HandleFatalError(ex, true);
            throw;
        }
        catch (COMException ex)
        {
            // Stage 4: Trigger FFmpeg fallback for driver issues
            HandleFatalError(ex, true);
            throw;
        }
        catch (Exception ex)
        {
            HandleFatalError(ex, true);
            throw;
        }
    }

    public async Task StopRecordingAsync()
    {
        ICaptureSource? captureSource;
        IVideoEncoder? encoder;

        lock (_lock)
        {
            if (_status != RecordingStatus.Recording)
            {
                return; // Already stopped or never started
            }

            UpdateStatus(RecordingStatus.Finalizing);
            _stopwatch.Stop();

            captureSource = _captureSource;
            encoder = _encoder;
        }

        try
        {
            // Stop capture
            if (captureSource != null)
            {
                captureSource.FrameArrived -= OnFrameCaptured;
                await captureSource.StopCaptureAsync();
                captureSource.Dispose();
            }

            // Finalize encoder
            encoder?.Finalize();
            encoder?.Dispose();
        }
        catch (Exception ex)
        {
            HandleFatalError(ex, false);
        }
        finally
        {
            lock (_lock)
            {
                _captureSource = null;
                _encoder = null;
                _currentOptions = null;
                UpdateStatus(RecordingStatus.Idle);
            }
        }
    }

    private async Task InitializeCaptureSource(RecordingOptions options)
    {
        if (_captureSource == null)
        {
            throw new InvalidOperationException("Capture source not created");
        }

        // Use dynamic dispatch to call platform-specific initialization
        // This allows us to stay platform-agnostic here
        dynamic source = _captureSource;

        // Stage 2: Configure cursor capture if supported
        try
        {
            source.ShowCursor = options.Settings?.ShowCursor ?? true;
        }
        catch
        {
            // Platform doesn't support ShowCursor property - ignore
        }

        switch (options.Mode)
        {
            case CaptureMode.Screen:
                source.InitializeForPrimaryMonitor();
                break;

            case CaptureMode.Window:
                if (options.TargetWindowHandle == IntPtr.Zero)
                {
                    throw new ArgumentException("TargetWindowHandle must be specified for Window mode");
                }
                source.InitializeForWindow(options.TargetWindowHandle);
                break;

            case CaptureMode.Region:
                // Stage 2: Region mode uses full screen capture + post-capture cropping
                // This is more efficient than trying to capture a specific region with WGC
                source.InitializeForPrimaryMonitor();
                break;

            default:
                throw new NotSupportedException($"Capture mode {options.Mode} not supported");
        }

        await Task.CompletedTask;
    }

    private void OnFrameCaptured(object? sender, FrameArrivedEventArgs e)
    {
        if (_disposed || _status != RecordingStatus.Recording) return;

        FrameData? croppedFrame = null;
        try
        {
            FrameData frameToEncode = e.Frame;

            // Stage 2: Crop frame if in Region mode
            if (_currentOptions?.Mode == CaptureMode.Region && _currentOptions.Region.Width > 0 && _currentOptions.Region.Height > 0)
            {
                try
                {
                    croppedFrame = RegionCropper.CropFrame(e.Frame, _currentOptions.Region);
                    frameToEncode = croppedFrame.Value;
                }
                catch (Exception cropEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to crop frame: {cropEx.Message}");
                    // Fall back to uncropped frame
                }
            }

            _encoder?.WriteFrame(frameToEncode);
        }
        catch (Exception ex)
        {
            HandleFatalError(ex, true);
        }
        finally
        {
            // Free cropped frame memory to prevent leak
            if (croppedFrame.HasValue)
            {
                RegionCropper.FreeCroppedFrame(croppedFrame.Value);
            }
        }
    }

    private string GetOutputPath(RecordingOptions options)
    {
        if (!string.IsNullOrEmpty(options.OutputPath))
        {
            return options.OutputPath;
        }

        // Default pattern: ShareX/Screenshots/yyyy-MM/Date_Time.mp4
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string shareXPath = Path.Combine(documentsPath, "ShareX", "Screenshots", DateTime.Now.ToString("yyyy-MM"));
        Directory.CreateDirectory(shareXPath);

        string fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4";
        return Path.Combine(shareXPath, fileName);
    }

    private int GetCaptureWidth(RecordingOptions options)
    {
        if (options.Mode == CaptureMode.Region && options.Region.Width > 0)
        {
            return options.Region.Width;
        }

        // Default to primary screen width
        return 1920;
    }

    private int GetCaptureHeight(RecordingOptions options)
    {
        if (options.Mode == CaptureMode.Region && options.Region.Height > 0)
        {
            return options.Region.Height;
        }

        // Default to primary screen height
        return 1080;
    }

    private void UpdateStatus(RecordingStatus newStatus)
    {
        lock (_lock)
        {
            if (_status == newStatus) return;

            _status = newStatus;
            var duration = _stopwatch.Elapsed;

            StatusChanged?.Invoke(this, new RecordingStatusEventArgs(newStatus, duration));
        }
    }

    private void HandleFatalError(Exception ex, bool isFatal)
    {
        lock (_lock)
        {
            if (_status != RecordingStatus.Error)
            {
                UpdateStatus(RecordingStatus.Error);
            }
        }

        ErrorOccurred?.Invoke(this, new RecordingErrorEventArgs(ex, isFatal));

        if (isFatal)
        {
            // Cleanup
            try
            {
                _captureSource?.Dispose();
                _encoder?.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }

            _captureSource = null;
            _encoder = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _disposed = true;

            try
            {
                if (_status == RecordingStatus.Recording)
                {
                    StopRecordingAsync().Wait();
                }
            }
            catch
            {
                // Best effort cleanup
            }

            _captureSource?.Dispose();
            _encoder?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
