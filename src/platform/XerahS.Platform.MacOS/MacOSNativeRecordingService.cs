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

using System;
using System.Diagnostics;
using System.IO;
using XerahS.Common;
using XerahS.Platform.MacOS.Native;
using XerahS.RegionCapture.ScreenRecording;

namespace XerahS.Platform.MacOS;

/// <summary>
/// Native macOS screen recording service using ScreenCaptureKit.
/// Uses SCStream + AVAssetWriter for hardware-accelerated video recording.
/// Requires macOS 12.3+
/// </summary>
public class MacOSNativeRecordingService : IRecordingService
{
    private IntPtr _sessionHandle;
    private RecordingStatus _status = RecordingStatus.Idle;
    private RecordingOptions? _currentOptions;
    private readonly Stopwatch _stopwatch = new();
    private readonly object _lock = new();
    private bool _disposed;

    public event EventHandler<RecordingErrorEventArgs>? ErrorOccurred;
    public event EventHandler<RecordingStatusEventArgs>? StatusChanged;

    /// <summary>
    /// Check if native ScreenCaptureKit recording is available.
    /// </summary>
    public static bool IsAvailable()
    {
        try
        {
            return ScreenCaptureKitInterop.TryLoad() && ScreenCaptureKitInterop.IsAvailable() == 1;
        }
        catch
        {
            return false;
        }
    }

    public Task StartRecordingAsync(RecordingOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MacOSNativeRecordingService));
            if (_status != RecordingStatus.Idle)
            {
                throw new InvalidOperationException("Recording already in progress");
            }

            _currentOptions = options;
            UpdateStatus(RecordingStatus.Initializing);
        }

        try
        {
            // Determine output path
            string outputPath = options.OutputPath ?? GetDefaultOutputPath();
            
            // Ensure directory exists
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Determine region (0,0,0,0 for fullscreen)
            float x = 0, y = 0, w = 0, h = 0;
            if (options.Mode == CaptureMode.Region && options.Region.Width > 0 && options.Region.Height > 0)
            {
                x = options.Region.X;
                y = options.Region.Y;
                w = options.Region.Width;
                h = options.Region.Height;
            }

            int fps = options.Settings?.FPS ?? 30;
            int showCursor = (options.Settings?.ShowCursor ?? true) ? 1 : 0;

            Console.WriteLine($"[MacOSNativeRecordingService] Starting native recording");
            Console.WriteLine($"[MacOSNativeRecordingService] Output: {outputPath}");
            Console.WriteLine($"[MacOSNativeRecordingService] Region: x={x}, y={y}, w={w}, h={h}");
            Console.WriteLine($"[MacOSNativeRecordingService] FPS: {fps}, ShowCursor: {showCursor}");

            int result = ScreenCaptureKitInterop.StartRecording(
                outputPath, x, y, w, h, fps, showCursor, out _sessionHandle);

            if (result != ScreenCaptureKitInterop.SUCCESS)
            {
                string error = ScreenCaptureKitInterop.GetErrorMessage(result);
                Console.WriteLine($"[MacOSNativeRecordingService] Failed to start: {error}");
                throw new InvalidOperationException($"Failed to start native recording: {error}");
            }

            Console.WriteLine("[MacOSNativeRecordingService] Recording started successfully");

            lock (_lock)
            {
                _stopwatch.Restart();
                UpdateStatus(RecordingStatus.Recording);
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            HandleFatalError(ex, true);
            throw;
        }
    }

    public Task StopRecordingAsync()
    {
        lock (_lock)
        {
            if (_status != RecordingStatus.Recording)
            {
                return Task.CompletedTask; // Already stopped or never started
            }

            UpdateStatus(RecordingStatus.Finalizing);
            _stopwatch.Stop();
        }

        try
        {
            Console.WriteLine("[MacOSNativeRecordingService] Stopping recording...");

            int result = ScreenCaptureKitInterop.StopRecording(_sessionHandle);

            if (result != ScreenCaptureKitInterop.SUCCESS)
            {
                string error = ScreenCaptureKitInterop.GetErrorMessage(result);
                Console.WriteLine($"[MacOSNativeRecordingService] Error stopping: {error}");
            }
            else
            {
                Console.WriteLine("[MacOSNativeRecordingService] Recording stopped and saved successfully");
            }
        }
        catch (Exception ex)
        {
            HandleFatalError(ex, false);
        }
        finally
        {
            lock (_lock)
            {
                _sessionHandle = IntPtr.Zero;
                _currentOptions = null;
                UpdateStatus(RecordingStatus.Idle);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Abort recording without saving the file.
    /// </summary>
    public void AbortRecording()
    {
        lock (_lock)
        {
            if (_sessionHandle == IntPtr.Zero) return;
        }

        try
        {
            Console.WriteLine("[MacOSNativeRecordingService] Aborting recording...");
            ScreenCaptureKitInterop.AbortRecording(_sessionHandle);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex);
        }
        finally
        {
            lock (_lock)
            {
                _sessionHandle = IntPtr.Zero;
                _currentOptions = null;
                _status = RecordingStatus.Idle;
            }
        }
    }

    private string GetDefaultOutputPath()
    {
        string screencastsFolder = PathsManager.ScreencastsFolder;
        string dateFolderPath = Path.Combine(screencastsFolder, DateTime.Now.ToString("yyyy-MM"));
        Directory.CreateDirectory(dateFolderPath);

        string fileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4";
        return Path.Combine(dateFolderPath, fileName);
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
                if (_sessionHandle != IntPtr.Zero)
                {
                    ScreenCaptureKitInterop.AbortRecording(_sessionHandle);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            _sessionHandle = IntPtr.Zero;
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

            if (_sessionHandle != IntPtr.Zero)
            {
                ScreenCaptureKitInterop.AbortRecording(_sessionHandle);
                _sessionHandle = IntPtr.Zero;
            }
        }

        GC.SuppressFinalize(this);
    }
}
