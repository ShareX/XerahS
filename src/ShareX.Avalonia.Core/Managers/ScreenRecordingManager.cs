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

using XerahS.Common;
using XerahS.Core.Helpers;
using XerahS.ScreenCapture.ScreenRecording;
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
    private IRecordingService? _currentRecording;
    private RecordingOptions? _currentOptions;

    private ScreenRecordingManager()
    {
    }

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
    /// Starts a new recording session
    /// </summary>
    /// <param name="options">Recording configuration</param>
    /// <exception cref="InvalidOperationException">Thrown if a recording is already in progress</exception>
    public async Task StartRecordingAsync(RecordingOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        Exception? lastError = null;
        bool preferFallback = ShouldForceFallback(options);

        lock (_lock)
        {
            if (_currentRecording != null)
            {
                throw new InvalidOperationException("A recording is already in progress. Stop the current recording before starting a new one.");
            }

            _currentOptions = options;
        }

        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool useFallback = preferFallback || attempt == 1;
            var recordingService = CreateRecordingService(useFallback);

            lock (_lock)
            {
                _currentRecording = recordingService;
            }

        try
        {
            TroubleshootingHelper.Log("ScreenRecorder", "MANAGER", $"Attempt {attempt + 1}: useFallback={useFallback}");
            WireRecordingEvents(recordingService);
            DebugHelper.WriteLine($"ScreenRecordingManager: Starting {(useFallback ? "fallback (FFmpeg)" : "native")} recording - Mode={options.Mode}, Codec={options.Settings?.Codec}, FPS={options.Settings?.FPS}");
            TroubleshootingHelper.Log("ScreenRecorder", "MANAGER", $"Calling recordingService.StartRecordingAsync");
            await recordingService.StartRecordingAsync(options);
            TroubleshootingHelper.Log("ScreenRecorder", "MANAGER", $"Recording started successfully");
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
                CleanupCurrentRecording(recordingService);
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

    /// <summary>
    /// Stops the current recording session
    /// </summary>
    /// <returns>Output file path if recording completed successfully, null otherwise</returns>
    public async Task<string?> StopRecordingAsync()
    {
        IRecordingService? recordingService;
        string? outputPath;

        lock (_lock)
        {
            if (_currentRecording == null)
            {
                DebugHelper.WriteLine("ScreenRecordingManager: No recording in progress to stop");
                return null;
            }

            recordingService = _currentRecording;
            outputPath = _currentOptions?.OutputPath;
        }

        try
        {
            DebugHelper.WriteLine("ScreenRecordingManager: Stopping recording...");
            await recordingService.StopRecordingAsync();

            // Notify completion
            if (!string.IsNullOrEmpty(outputPath))
            {
                RecordingCompleted?.Invoke(this, outputPath);
            }

            return outputPath;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "ScreenRecordingManager: Error stopping recording");
            throw;
        }
        finally
        {
            // Clean up
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

    /// <summary>
    /// Aborts the current recording session without saving
    /// </summary>
    public async Task AbortRecordingAsync()
    {
        IRecordingService? recordingService;

        lock (_lock)
        {
            if (_currentRecording == null)
            {
                DebugHelper.WriteLine("ScreenRecordingManager: No recording in progress to abort");
                return;
            }

            recordingService = _currentRecording;
        }

        try
        {
            DebugHelper.WriteLine("ScreenRecordingManager: Aborting recording...");

            // Stop recording (this will finalize the file, but we can delete it afterwards if needed)
            await recordingService.StopRecordingAsync();

            // TODO: Delete output file if abort should not save
            // For now, abort behaves the same as stop
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "ScreenRecordingManager: Error aborting recording");
        }
        finally
        {
            // Clean up
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

    private static IRecordingService CreateRecordingService(bool useFallback)
    {
        if (useFallback)
        {
            if (ScreenRecorderService.FallbackServiceFactory != null)
            {
                return ScreenRecorderService.FallbackServiceFactory();
            }

            return new FFmpegRecordingService();
        }

        return new ScreenRecorderService();
    }

    private static bool ShouldForceFallback(RecordingOptions options)
    {
        var settings = options.Settings;

        if (settings?.ForceFFmpeg == true)
        {
            return true;
        }

        // Audio capture currently routes through FFmpeg fallback
        if (settings is not null && (settings.CaptureSystemAudio || settings.CaptureMicrophone))
        {
            return true;
        }

        return ScreenRecorderService.CaptureSourceFactory == null || ScreenRecorderService.EncoderFactory == null;
    }

    private static bool CanFallbackFrom(Exception ex)
    {
        return ex is PlatformNotSupportedException || ex is COMException;
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
        DebugHelper.WriteLine($"ScreenRecordingManager: Status changed to {e.Status}, Duration={e.Duration}");
        StatusChanged?.Invoke(this, e);
    }

    private void OnRecordingErrorOccurred(object? sender, RecordingErrorEventArgs e)
    {
        DebugHelper.WriteException(e.Error, $"ScreenRecordingManager: Recording error (Fatal={e.IsFatal})");
        ErrorOccurred?.Invoke(this, e);

        // Clean up on fatal error
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
        }
    }
}
