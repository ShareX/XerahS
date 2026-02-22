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
using XerahS.Core;
using XerahS.Core.Helpers;
using XerahS.RegionCapture.ScreenRecording;
using System.Runtime.InteropServices;

namespace XerahS.Core.Managers;

/// <summary>
/// Global manager for screen recording sessions
/// Coordinates recording state across UI and workflow pipelines
/// Stage 5: Workflow Pipeline Integration
/// </summary>
public class ScreenRecordingManager
{
    private static readonly Lazy<ScreenRecordingManager> _lazy = new(() => new ScreenRecordingManager());
    public static ScreenRecordingManager Instance => _lazy.Value;

    private readonly object _lock = new();
    private readonly SemaphoreSlim _stopSemaphore = new(1, 1);
    private IRecordingService? _currentRecording;
    private RecordingOptions? _currentOptions;
    private TaskCompletionSource<bool>? _stopSignal;
    private readonly List<string> _segments = new();
    private RecordingOptions? _resumeOptions;
    private string? _finalOutputPath;
    private int _segmentIndex;
    private bool _isPaused;
    private bool _abortRequested;
    private bool _isFinalized;
    private string? _cachedFinalPath;
    private TimeSpan _lastDuration;

    /// <summary>
    /// Task representing platform-specific recording initialization.
    /// Set by the application startup code and awaited before starting recording.
    /// </summary>
    public static System.Threading.Tasks.Task? PlatformInitializationTask { get; set; }

    private ScreenRecordingManager()
    {
    }

    /// <summary>
    /// Factory function for creating the primary recording service.
    /// MUST be initialized by the application composition root.
    /// </summary>


    /// <summary>
    /// Event fired when recording status changes
    /// </summary>
    public event EventHandler<RecordingStatusEventArgs>? StatusChanged;

    /// <summary>
    /// Event fired when a recording error occurs
    /// </summary>
    public event EventHandler<RecordingErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Event fired when a recording completes successfully
    /// </summary>
    public event EventHandler<string>? RecordingCompleted;

    /// <summary>
    /// Event fired when recording starts, includes information about the recording method
    /// </summary>
    public event EventHandler<RecordingStartedEventArgs>? RecordingStarted;

    /// <summary>
    /// Indicates whether a recording is currently active
    /// </summary>
    public bool IsRecording
    {
        get
        {
            lock (_lock)
            {
                return _currentRecording != null;
            }
        }
    }

    public bool IsPaused
    {
        get
        {
            lock (_lock)
            {
                return _isPaused;
            }
        }
    }

    /// <summary>
    /// Indicates whether the current recording is using FFmpeg fallback
    /// </summary>
    public bool IsUsingFallback { get; private set; }

    /// <summary>
    /// Current recording options (null if not recording)
    /// </summary>
    public RecordingOptions? CurrentOptions
    {
        get
        {
            lock (_lock)
            {
                return _currentOptions;
            }
        }
    }

    /// <summary>
    /// Signals the current recording task to stop.
    /// Used by the hotkey handler to resume the waiting WorkerTask.
    /// </summary>
    public void SignalStop()
    {
        lock (_lock)
        {
            _stopSignal?.TrySetResult(true);
        }
    }

    /// <summary>
    /// Asynchronously waits for the Stop signal.
    /// Called by the WorkerTask to yield execution while recording.
    /// </summary>
    public Task WaitForStopSignalAsync()
    {
        lock (_lock)
        {
            if (_stopSignal == null || _stopSignal.Task.IsCompleted)
            {
                _stopSignal = new TaskCompletionSource<bool>();
            }
            return _stopSignal.Task;
        }
    }

    /// <summary>
    /// Starts a new recording session
    /// </summary>
    /// <param name="options">Recording configuration</param>
    /// <exception cref="InvalidOperationException">Thrown if a recording is already in progress</exception>
    public async Task StartRecordingAsync(RecordingOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        // Wait for platform recording initialization to complete if it's still running
        // This ensures factories are set up before we try to create recording services
        await EnsureRecordingInitialized();

        // Verify recording services are available after initialization
        if (ScreenRecorderService.NativeRecordingServiceFactory == null &&
            ScreenRecorderService.FallbackServiceFactory == null)
        {
            throw new InvalidOperationException(
                "Screen recording is not available. On Linux Wayland, ensure xdg-desktop-portal with ScreenCast support and PipeWire are installed and running.");
        }

        if (string.IsNullOrEmpty(options.OutputPath))
        {
            string screenCapturesFolder = SettingsManager.ScreencastsFolder;
            string dateFolderPath = Path.Combine(screenCapturesFolder, DateTime.Now.ToString("yyyy-MM"));
            Directory.CreateDirectory(dateFolderPath);

            string fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4";
            options.OutputPath = Path.Combine(dateFolderPath, fileName);
            DebugHelper.WriteLine($"ScreenRecordingManager: Generated default output path: {options.OutputPath}");
        }

        bool preferFallback = ShouldForceFallback(options);
        RecordingOptions optionsToStart = PrepareRecordingOptions(options, isResume: false);

        await StartRecordingCoreAsync(optionsToStart, preferFallback);
    }

    /// <summary>
    /// Stops the current recording session.
    /// Thread-safe: Uses a semaphore to prevent concurrent stop/finalization operations.
    /// If already finalized, returns the cached final path without re-processing.
    /// </summary>
    /// <returns>Output file path if recording completed successfully, null otherwise</returns>
    public async Task<string?> StopRecordingAsync()
    {
        // Fast path: if already finalized, return cached path immediately
        lock (_lock)
        {
            if (_isFinalized)
            {
                DebugHelper.WriteLine("ScreenRecordingManager: Already finalized, returning cached path");
                return _cachedFinalPath;
            }
        }

        // Acquire semaphore to ensure only one stop/finalization runs at a time
        await _stopSemaphore.WaitAsync();
        try
        {
            // Double-check after acquiring lock (another call may have finished)
            lock (_lock)
            {
                if (_isFinalized)
                {
                    DebugHelper.WriteLine("ScreenRecordingManager: Already finalized (after semaphore), returning cached path");
                    return _cachedFinalPath;
                }
            }

            bool wasPaused = IsPaused;
            if (wasPaused)
            {
                // Stop while paused: finalize segments without starting a new recording.
                _isPaused = false;
                StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecordingStatus.Finalizing, _lastDuration));
            }

            if (_abortRequested)
            {
                CleanupSegments(deleteFinalOutput: true);
                _abortRequested = false;
                return null;
            }

            await StopRecordingCoreAsync(signalStop: true);
            string? finalPath = await FinalizeSegmentsAsync();

            if (!string.IsNullOrEmpty(finalPath))
            {
                RecordingCompleted?.Invoke(this, finalPath);
            }

            if (string.IsNullOrEmpty(finalPath) && !string.IsNullOrEmpty(_finalOutputPath) && File.Exists(_finalOutputPath))
            {
                finalPath = _finalOutputPath;
            }

            if (wasPaused)
            {
                StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecordingStatus.Idle, _lastDuration));
            }

            // Mark as finalized and cache the path for subsequent calls
            lock (_lock)
            {
                _isFinalized = true;
                _cachedFinalPath = finalPath;
            }

            return finalPath;
        }
        finally
        {
            _stopSemaphore.Release();
        }
    }

    /// <summary>
    /// Aborts the current recording session without saving
    /// </summary>
    public async Task AbortRecordingAsync()
    {
        DebugHelper.WriteLine("ScreenRecordingManager: Aborting recording...");

        bool wasPaused = IsPaused;
        _abortRequested = true;
        _isPaused = false;

        await StopRecordingCoreAsync(signalStop: true);
        CleanupSegments(deleteFinalOutput: true);
        _abortRequested = false;

        if (wasPaused)
        {
            StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecordingStatus.Idle, _lastDuration));
        }
    }

    /// <summary>
    /// Factory function for creating the fallback recording service (e.g. FFmpeg).
    /// MUST be initialized by the application composition root.
    /// </summary>


    // ... (existing code) ...

    private static IRecordingService CreateRecordingService(bool useFallback)
    {
        if (useFallback)
        {
            // Direct instantiation of FFmpeg fallback service
            return new XerahS.RegionCapture.ScreenRecording.FFmpegRecordingService();
        }

        // Check for native recording service factory (e.g., MacOSNativeRecordingService)
        if (ScreenRecorderService.NativeRecordingServiceFactory != null)
        {
            TroubleshootingHelper.Log("ScreenRecorder", "NATIVE", "Using NativeRecordingServiceFactory");
            return ScreenRecorderService.NativeRecordingServiceFactory();
        }

        // Fallback to capture source + encoder pattern (Windows)
        return new XerahS.RegionCapture.ScreenRecording.ScreenRecorderService();
    }

    private static bool ShouldForceFallback(RecordingOptions options)
    {
        var settings = options.Settings;

        // Detect Wayland - FFmpeg x11grab doesn't work on Wayland
        bool isWayland = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.Equals("wayland", StringComparison.OrdinalIgnoreCase) == true;
        bool hasNativeFactory = ScreenRecorderService.NativeRecordingServiceFactory != null;

        DebugHelper.WriteLine($"ShouldForceFallback: isWayland={isWayland}, hasNativeFactory={hasNativeFactory}, UseModernCapture={options.UseModernCapture}");

        // Check UseModernCapture override: if false, force FFmpeg fallback
        // BUT on Wayland, FFmpeg x11grab doesn't work, so prefer native recording if available
        if (!options.UseModernCapture)
        {
            if (isWayland && hasNativeFactory)
            {
                TroubleshootingHelper.Log("ScreenRecorder", "NATIVE", "UseModernCapture is disabled but on Wayland with portal available -> using native (x11grab won't work)");
                return false; // Use native on Wayland even if UseModernCapture is false
            }

            if (isWayland && !hasNativeFactory)
            {
                DebugHelper.WriteLine("WARNING: On Wayland but NativeRecordingServiceFactory is null - portal recording not initialized!");
            }

            TroubleshootingHelper.Log("ScreenRecorder", "FALLBACK", "UseModernCapture is disabled -> forcing FFmpeg fallback");
            return true;
        }

        if (settings?.ForceFFmpeg == true)
        {
            // On Wayland, warn that ForceFFmpeg won't work and fall back to native if available
            if (isWayland && ScreenRecorderService.NativeRecordingServiceFactory != null)
            {
                TroubleshootingHelper.Log("ScreenRecorder", "NATIVE", "ForceFFmpeg requested but on Wayland -> using native (x11grab won't work)");
                return false;
            }

            TroubleshootingHelper.Log("ScreenRecorder", "FALLBACK", "ForceFFmpeg setting is enabled -> using FFmpeg");
            return true;
        }

        // Audio capture currently routes through FFmpeg fallback
        if (settings is not null && (settings.CaptureSystemAudio || settings.CaptureMicrophone))
        {
            if (isWayland && ScreenRecorderService.NativeRecordingServiceFactory != null)
            {
                TroubleshootingHelper.Log("ScreenRecorder", "NATIVE", "Audio capture requested on Wayland -> using native portal backend");
                return false;
            }

            TroubleshootingHelper.Log("ScreenRecorder", "FALLBACK", "Audio capture requested -> using FFmpeg");
            return true;
        }

        // Check if we have a native recording service factory (complete IRecordingService)
        if (ScreenRecorderService.NativeRecordingServiceFactory != null)
        {
            TroubleshootingHelper.Log("ScreenRecorder", "NATIVE", "NativeRecordingServiceFactory available -> using native");
            return false; // Use native, not fallback
        }

        // If native capture factory is not configured (e.g. macOS without native), must use fallback
        if (ScreenRecorderService.CaptureSourceFactory == null)
        {
            TroubleshootingHelper.Log("ScreenRecorder", "FALLBACK", "Native CaptureSourceFactory not set -> forcing FFmpeg fallback");
            return true;
        }

        return false;
    }

    private static bool CanFallbackFrom(Exception ex)
    {
        return ex is PlatformNotSupportedException || ex is COMException;
    }

    /// <summary>
    /// Core recording start logic shared by both initial start and resume-after-pause.
    /// Attempts native recording first, then falls back to FFmpeg if the native attempt
    /// throws a recoverable exception.
    /// </summary>
    private async Task StartRecordingCoreAsync(RecordingOptions optionsToStart, bool preferFallback)
    {
        Exception? lastError = null;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool useFallback = preferFallback || attempt == 1;
            IRecordingService recordingService;

            // Create service and assign state within single lock to prevent race condition
            lock (_lock)
            {
                if (_currentRecording != null)
                {
                    throw new InvalidOperationException("A recording is already in progress. Stop the current recording before starting a new one.");
                }

                try
                {
                    recordingService = CreateRecordingService(useFallback);
                    _currentRecording = recordingService;
                    _currentOptions = optionsToStart;
                    _stopSignal = new TaskCompletionSource<bool>();
                }
                catch
                {
                    // Rollback state on service creation failure
                    _currentRecording = null;
                    _currentOptions = null;
                    _stopSignal = null;
                    throw;
                }
            }

            try
            {
                TroubleshootingHelper.Log("ScreenRecorder", "MANAGER", $"Attempt {attempt + 1}: useFallback={useFallback}");
                WireRecordingEvents(recordingService);

                DebugHelper.WriteLine($"ScreenRecordingManager: Starting {(useFallback ? "fallback (FFmpeg)" : "native")} recording - Mode={optionsToStart.Mode}, Codec={optionsToStart.Settings?.Codec}, FPS={optionsToStart.Settings?.FPS}");

                TroubleshootingHelper.Log("ScreenRecorder", "MANAGER", "Calling recordingService.StartRecordingAsync");
                await recordingService.StartRecordingAsync(optionsToStart);

                TroubleshootingHelper.Log("ScreenRecorder", "MANAGER", "Recording started successfully");

                // Track fallback status and notify UI
                IsUsingFallback = useFallback;
                RecordingStarted?.Invoke(this, new RecordingStartedEventArgs(useFallback, optionsToStart));

                return;
            }
            catch (Exception ex) when (!useFallback && CanFallbackFrom(ex))
            {
                TroubleshootingHelper.Log("ScreenRecorder", "MANAGER", $"Native recording failed: {ex.Message}, attempting FFmpeg fallback");
                DebugHelper.WriteException(ex, "ScreenRecordingManager: Native recording failed, attempting FFmpeg fallback...");
                lastError = ex;
                CleanupCurrentRecording(recordingService);
                preferFallback = true;
            }
            catch (Exception ex)
            {
                TroubleshootingHelper.Log("ScreenRecorder", "MANAGER", $"Recording failed with unrecoverable error: {ex.Message}");

                try
                {
                    CleanupCurrentRecording(recordingService);
                }
                catch (Exception cleanupEx)
                {
                    DebugHelper.WriteException(cleanupEx, "ScreenRecordingManager: Error during recording cleanup");
                }

                lock (_lock)
                {
                    _currentOptions = null;
                    _currentRecording = null;
                }
                throw;
            }
        }

        lock (_lock)
        {
            _currentOptions = null;
            _currentRecording = null;
        }

        throw lastError ?? new InvalidOperationException("Recording failed and no fallback recording service is available.");
    }

    private void WireRecordingEvents(IRecordingService recordingService)
    {
        recordingService.StatusChanged += OnRecordingStatusChanged;
        recordingService.ErrorOccurred += OnRecordingErrorOccurred;
    }

    private void CleanupCurrentRecording(IRecordingService recordingService)
    {
        try
        {
            recordingService.StatusChanged -= OnRecordingStatusChanged;
            recordingService.ErrorOccurred -= OnRecordingErrorOccurred;
            recordingService.Dispose();
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private void OnRecordingStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        _lastDuration = e.Duration;
        DebugHelper.WriteLine($"ScreenRecordingManager: Status changed to {e.Status}, Duration={e.Duration}");
        StatusChanged?.Invoke(this, e);
    }

    private void OnRecordingErrorOccurred(object? sender, RecordingErrorEventArgs e)
    {
        DebugHelper.WriteException(e.Error, $"ScreenRecordingManager: Recording error (Fatal={e.IsFatal})");
        ErrorOccurred?.Invoke(this, e);

        // Clean up on fatal error and unblock the waiting WorkerTask
        if (e.IsFatal)
        {
            lock (_lock)
            {
                if (_currentRecording != null)
                {
                    CleanupCurrentRecording(_currentRecording);
                }

                _currentRecording = null;
                _currentOptions = null;
            }

            SignalStop();
        }
    }

    public async Task TogglePauseResumeAsync()
    {
        if (IsPaused)
        {
            await ResumeRecordingAsync();
        }
        else
        {
            await PauseRecordingAsync();
        }
    }

    public async Task PauseRecordingAsync()
    {
        if (!IsRecording || IsPaused)
        {
            return;
        }

        _isPaused = true;
        await StopRecordingCoreAsync(signalStop: false);
        StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecordingStatus.Paused, _lastDuration));
    }

    public async Task ResumeRecordingAsync()
    {
        RecordingOptions? options;
        lock (_lock)
        {
            if (!_isPaused || _resumeOptions == null)
            {
                return;
            }

            _isPaused = false;
            options = _resumeOptions;
        }

        await StartRecordingInternalAsync(options, isResume: true);
    }

    private RecordingOptions PrepareRecordingOptions(RecordingOptions options, bool isResume)
    {
        if (!isResume)
        {
            _segments.Clear();
            _segmentIndex = 0;
            _abortRequested = false;
            _isPaused = false;
            _isFinalized = false;
            _cachedFinalPath = null;
            _finalOutputPath = options.OutputPath;
            _resumeOptions = CloneOptions(options);
        }

        if (string.IsNullOrEmpty(_finalOutputPath))
        {
            _finalOutputPath = options.OutputPath;
        }

        var segmentPath = BuildSegmentPath(_finalOutputPath!, _segmentIndex++);
        return CloneOptions(options, segmentPath);
    }

    private async Task StartRecordingInternalAsync(RecordingOptions options, bool isResume)
    {
        await EnsureRecordingInitialized();

        bool preferFallback = ShouldForceFallback(options);
        RecordingOptions optionsToStart = PrepareRecordingOptions(options, isResume);

        await StartRecordingCoreAsync(optionsToStart, preferFallback);
    }

    private async Task StopRecordingCoreAsync(bool signalStop)
    {
        IRecordingService? recordingService;
        string? outputPath;
        string? fallbackSegmentPath;

        lock (_lock)
        {
            if (_currentRecording == null)
            {
                DebugHelper.WriteLine("ScreenRecordingManager: No recording in progress to stop");
                if (signalStop)
                {
                    SignalStop();
                }
                return;
            }

            recordingService = _currentRecording;
            outputPath = _currentOptions?.OutputPath;
            fallbackSegmentPath = GetLastSegmentPath();
        }

        try
        {
            DebugHelper.WriteLine("ScreenRecordingManager: Stopping recording...");
            await recordingService.StopRecordingAsync();

            if (signalStop)
            {
                SignalStop();
            }

            string? resolvedOutput = outputPath;
            if (string.IsNullOrEmpty(resolvedOutput) && !string.IsNullOrEmpty(fallbackSegmentPath))
            {
                resolvedOutput = fallbackSegmentPath;
            }

            if (!string.IsNullOrEmpty(resolvedOutput))
            {
                if (!File.Exists(resolvedOutput))
                {
                    DebugHelper.WriteLine($"ScreenRecordingManager: Output not found yet, waiting: {resolvedOutput}");
                    bool appeared = await WaitForFileAsync(resolvedOutput, TimeSpan.FromSeconds(5));
                    DebugHelper.WriteLine($"ScreenRecordingManager: Output wait completed. Found={appeared} Path={resolvedOutput}");

                    if (!appeared)
                    {
                        // List files in the directory to aid debugging
                        try
                        {
                            string? dir = Path.GetDirectoryName(resolvedOutput);
                            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                            {
                                var files = Directory.GetFiles(dir, "*.mp4");
                                DebugHelper.WriteLine($"ScreenRecordingManager: Files in {dir}: {string.Join(", ", files.Select(Path.GetFileName))}");
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugHelper.WriteException(ex, "ScreenRecordingManager: Error listing directory");
                        }
                    }
                }

                if (!File.Exists(resolvedOutput) && !string.IsNullOrEmpty(fallbackSegmentPath) && File.Exists(fallbackSegmentPath))
                {
                    DebugHelper.WriteLine($"ScreenRecordingManager: Using fallback segment path: {fallbackSegmentPath}");
                    resolvedOutput = fallbackSegmentPath;
                }

                if (File.Exists(resolvedOutput))
                {
                    _segments.Add(resolvedOutput);
                }
                else
                {
                    DebugHelper.WriteLine($"ScreenRecordingManager: Output missing after stop: {resolvedOutput}");
                }
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "ScreenRecordingManager: Error stopping recording");
            throw;
        }
        finally
        {
            lock (_lock)
            {
                if (recordingService != null)
                {
                    CleanupCurrentRecording(recordingService);
                }

                _currentRecording = null;
                _currentOptions = null;
            }
        }
    }

    private async Task<string?> FinalizeSegmentsAsync()
    {
        if (_segments.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrEmpty(_finalOutputPath))
        {
            return _segments.LastOrDefault();
        }

        if (_segments.Count == 1)
        {
            string single = _segments[0];
            if (!string.Equals(single, _finalOutputPath, StringComparison.OrdinalIgnoreCase))
            {
                FileHelpers.CreateDirectoryFromFilePath(_finalOutputPath);
                File.Move(single, _finalOutputPath, overwrite: true);
            }

            _segments.Clear();
            ResetSegmentState();
            return _finalOutputPath;
        }

        string ffmpegPath = PathsManager.GetFFmpegPath();
        if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
        {
            DebugHelper.WriteLine("ScreenRecordingManager: FFmpeg not found; cannot concatenate paused segments.");
            return _segments.LastOrDefault();
        }

        string tempOutput = FileHelpers.AppendTextToFileName(_finalOutputPath, "-concat");
        await Task.Run(() =>
        {
            var ffmpeg = new XerahS.Media.FFmpegCLIManager(ffmpegPath);
            ffmpeg.ShowError = true;
            ffmpeg.ConcatenateVideos(_segments.ToArray(), tempOutput, autoDeleteInputFiles: true);
        });

        if (File.Exists(tempOutput))
        {
            File.Move(tempOutput, _finalOutputPath, overwrite: true);
        }

        _segments.Clear();
        ResetSegmentState();
        return _finalOutputPath;
    }

    private void CleanupSegments(bool deleteFinalOutput)
    {
        foreach (var segment in _segments)
        {
            try
            {
                if (File.Exists(segment))
                {
                    File.Delete(segment);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }

        _segments.Clear();

        if (deleteFinalOutput && !string.IsNullOrEmpty(_finalOutputPath))
        {
            try
            {
                if (File.Exists(_finalOutputPath))
                {
                    File.Delete(_finalOutputPath);
                }
            }
            catch
            {
                // Ignore
            }
        }

        ResetSegmentState();
    }

    private static RecordingOptions CloneOptions(RecordingOptions source, string? outputPath = null)
    {
        return new RecordingOptions
        {
            Mode = source.Mode,
            TargetWindowHandle = source.TargetWindowHandle,
            Region = source.Region,
            OutputPath = outputPath ?? source.OutputPath,
            Settings = source.Settings,
            UseModernCapture = source.UseModernCapture
        };
    }

    private static string BuildSegmentPath(string outputPath, int index)
    {
        string directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
        string fileName = Path.GetFileNameWithoutExtension(outputPath);
        string extension = Path.GetExtension(outputPath);
        string segmentFileName = $"{fileName}.part{index:D3}{extension}";
        return Path.Combine(directory, segmentFileName);
    }

    private string? GetLastSegmentPath()
    {
        if (string.IsNullOrEmpty(_finalOutputPath))
        {
            return null;
        }

        int lastIndex = Math.Max(0, _segmentIndex - 1);
        return BuildSegmentPath(_finalOutputPath, lastIndex);
    }

    private void ResetSegmentState()
    {
        _resumeOptions = null;
        _finalOutputPath = null;
        _segmentIndex = 0;
        _isPaused = false;
        _isFinalized = false;
        _cachedFinalPath = null;
    }

    private static async Task<bool> WaitForFileAsync(string path, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            if (File.Exists(path))
            {
                return true;
            }

            await Task.Delay(100);
        }

        return File.Exists(path);
    }

    /// <summary>
    /// Ensures platform recording initialization has completed before starting recording.
    /// Waits for the async initialization task if it's still running.
    /// </summary>
    private static async Task EnsureRecordingInitialized()
    {
        try
        {
            var initTask = PlatformInitializationTask;

            if (initTask != null)
            {
                if (!initTask.IsCompleted)
                {
                    DebugHelper.WriteLine("ScreenRecordingManager: Waiting for recording initialization to complete...");
                    await initTask;
                }

                if (initTask.IsFaulted)
                {
                    throw new InvalidOperationException("Recording initialization failed", initTask.Exception);
                }

                DebugHelper.WriteLine("ScreenRecordingManager: Recording initialization completed successfully");
            }
        }
        catch (Exception ex)
        {
            // Initialization failed - re-throw so caller knows recording won't work
            DebugHelper.WriteException(ex, "ScreenRecordingManager: Error waiting for recording initialization");
            throw;
        }
    }
}
