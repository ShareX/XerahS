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
using XerahS.ScreenCapture.ScreenRecording;

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

        IRecordingService recordingService;

        lock (_lock)
        {
            if (_currentRecording != null)
            {
                throw new InvalidOperationException("A recording is already in progress. Stop the current recording before starting a new one.");
            }

            // Create recording service
            recordingService = new ScreenRecorderService();

            // Wire up events
            recordingService.StatusChanged += OnRecordingStatusChanged;
            recordingService.ErrorOccurred += OnRecordingErrorOccurred;

            _currentRecording = recordingService;
            _currentOptions = options;
        }

        try
        {
            DebugHelper.WriteLine($"ScreenRecordingManager: Starting recording - Mode={options.Mode}, Codec={options.Settings?.Codec}, FPS={options.Settings?.FPS}");
            await recordingService.StartRecordingAsync(options);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "ScreenRecordingManager: Failed to start recording");

            // Clean up on failure
            lock (_lock)
            {
                if (recordingService != null)
                {
                    recordingService.StatusChanged -= OnRecordingStatusChanged;
                    recordingService.ErrorOccurred -= OnRecordingErrorOccurred;
                    recordingService.Dispose();
                }

                _currentRecording = null;
                _currentOptions = null;
            }

            throw;
        }
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
                    recordingService.StatusChanged -= OnRecordingStatusChanged;
                    recordingService.ErrorOccurred -= OnRecordingErrorOccurred;
                    recordingService.Dispose();
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
                    recordingService.StatusChanged -= OnRecordingStatusChanged;
                    recordingService.ErrorOccurred -= OnRecordingErrorOccurred;
                    recordingService.Dispose();
                }

                _currentRecording = null;
                _currentOptions = null;
            }
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
                    _currentRecording.StatusChanged -= OnRecordingStatusChanged;
                    _currentRecording.ErrorOccurred -= OnRecordingErrorOccurred;
                    _currentRecording.Dispose();
                }

                _currentRecording = null;
                _currentOptions = null;
            }
        }
    }
}
