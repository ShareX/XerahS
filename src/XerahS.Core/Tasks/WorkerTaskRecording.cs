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
using XerahS.Core.Helpers;
using XerahS.Core.Managers;
using XerahS.Core.Tasks.Processors;
using XerahS.History;
using XerahS.Media;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.ScreenRecording;
using System.Diagnostics;
using System.Drawing;

namespace XerahS.Core.Tasks
{
    /// <summary>
    /// WorkerTask partial class for screen recording operations.
    /// </summary>
    public partial class WorkerTask
    {
        #region Recording Handlers (Stage 5)

        private async Task HandleStartRecordingAsync(CaptureMode mode, IntPtr windowHandle = default, Rectangle? region = null)
        {
            var taskSettings = Info.TaskSettings ?? new TaskSettings();
            var metadata = Info.Metadata ?? new TaskMetadata();

            TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", $"HandleStartRecordingAsync Entry: mode={mode}, region={region}");

            try
            {
                // Note: We don't check IsRecording here because App.axaml.cs ensures we only get here if NOT recording.

                // Build recording options from task settings
                taskSettings.CaptureSettings ??= new TaskSettingsCapture();
                var captureSettings = taskSettings.CaptureSettings;

                var recordingOptions = new RecordingOptions
                {
                    Mode = mode,
                    Settings = captureSettings.ScreenRecordingSettings,
                    TargetWindowHandle = windowHandle,
                    UseModernCapture = captureSettings.UseModernCapture
                };

                // Set region if provided (for Region mode)
                if (region.HasValue)
                {
                    recordingOptions.Region = region.Value;
                    TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", $"Recording region set: {region.Value}");
                }

                // [2026-01-10T14:40:00+08:00] Align screen recording output with screenshot naming/destination using TaskHelpers.
                var recordingMetadata = metadata;
                string recordingsFolder = TaskHelpers.GetScreenshotsFolder(taskSettings, recordingMetadata);
                string fileName = TaskHelpers.GetFileName(taskSettings, "mp4", recordingMetadata);
                Directory.CreateDirectory(recordingsFolder);
                var resolvedPath = TaskHelpers.HandleExistsFile(recordingsFolder, fileName, taskSettings);
                recordingOptions.OutputPath = resolvedPath;
                Info.FilePath = resolvedPath;
                Info.DataType = EDataType.File;
                DebugHelper.WriteLine($"[PathTrace {Info.CorrelationId}] ScreenRecorder resolved path: dir=\"{recordingsFolder}\", fileName=\"{fileName}\", fullPath=\"{resolvedPath}\"");

                if (recordingOptions.Settings != null &&
                    (recordingOptions.Settings.CaptureSystemAudio || recordingOptions.Settings.CaptureMicrophone))
                {
                    // Force FFmpeg path until native audio capture is implemented
                    recordingOptions.Settings.ForceFFmpeg = true;
                }

                TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "Calling ScreenRecordingManager.StartRecordingAsync");
                DebugHelper.WriteLine($"Starting recording: Mode={mode}, Codec={recordingOptions.Settings?.Codec}, FPS={recordingOptions.Settings?.FPS}");
                DebugHelper.WriteLine($"Output path: {recordingOptions.OutputPath}");

                // 1. Start recording
                await ScreenRecordingManager.Instance.StartRecordingAsync(recordingOptions);
                TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "ScreenRecordingManager.StartRecordingAsync completed");

                // 2. Wait for stop signal (ASYNC WAIT - Yields thread, keeps task alive)
                TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "Waiting for stop signal...");
                await ScreenRecordingManager.Instance.WaitForStopSignalAsync();
                TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "Stop signal received. Resuming...");

                // 3. Stop recording
                DebugHelper.WriteLine("Stopping recording...");
                string? outputPath = await ScreenRecordingManager.Instance.StopRecordingAsync();
                DebugHelper.WriteLine($"[GIF] StopRecordingAsync returned: {(string.IsNullOrEmpty(outputPath) ? "(null)" : outputPath)} (exists={(!string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))})");
                string? expectedOutputPath = recordingOptions.OutputPath;
                bool expectedOutputExists = !string.IsNullOrEmpty(expectedOutputPath) && File.Exists(expectedOutputPath);
                DebugHelper.WriteLine($"[RecordingFinalize] Expected output path: {(string.IsNullOrEmpty(expectedOutputPath) ? "(null)" : expectedOutputPath)} (exists={expectedOutputExists})");

                if (string.IsNullOrEmpty(outputPath) && !string.IsNullOrEmpty(Info.FilePath) && File.Exists(Info.FilePath))
                {
                    DebugHelper.WriteLine($"[GIF] StopRecordingAsync returned null but Info.FilePath exists. Recovering path: {Info.FilePath}");
                    outputPath = Info.FilePath;
                }

                bool hasRecoveredOutput = !string.IsNullOrEmpty(outputPath) && File.Exists(outputPath);
                if (!hasRecoveredOutput)
                {
                    string recoveredPath = string.IsNullOrEmpty(outputPath) ? "(null)" : outputPath;
                    bool recoveredExists = !string.IsNullOrEmpty(outputPath) && File.Exists(outputPath);
                    string infoPath = string.IsNullOrEmpty(Info.FilePath) ? "(null)" : Info.FilePath;
                    bool infoPathExists = !string.IsNullOrEmpty(Info.FilePath) && File.Exists(Info.FilePath);
                    DebugHelper.WriteLine(
                        $"[RecordingFinalize] Output file missing after stop. expectedPath={expectedOutputPath ?? "(null)"} " +
                        $"expectedExists={expectedOutputExists} recoveredPath={recoveredPath} recoveredExists={recoveredExists} " +
                        $"infoPath={infoPath} infoPathExists={infoPathExists}");
                    throw new InvalidOperationException("Recording stopped but no output file was produced. Check recording backend logs for details.");
                }

                if (!string.IsNullOrEmpty(outputPath))
                {
                    DebugHelper.WriteLine($"Recording saved to: {outputPath}");
                    Info.FilePath = outputPath;
                    Info.DataType = EDataType.File;

                    bool isGifJob = taskSettings.Job == WorkflowType.ScreenRecorderGIF ||
                                    taskSettings.Job == WorkflowType.ScreenRecorderGIFActiveWindow ||
                                    taskSettings.Job == WorkflowType.ScreenRecorderGIFCustomRegion ||
                                    taskSettings.Job == WorkflowType.StartScreenRecorderGIF;
                    DebugHelper.WriteLine($"[GIF] isGifJob={isGifJob}, Job={taskSettings.Job}");

                    if (isGifJob && !string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))
                    {
                         TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "Converting video to GIF...");
                         DebugHelper.WriteLine($"[GIF] Conversion requested. Job={taskSettings.Job}, Source={outputPath}");
                         string gifPath = Path.ChangeExtension(outputPath, ".gif");
                         int gifFps = taskSettings.CaptureSettings?.GIFFPS > 0
                             ? taskSettings.CaptureSettings.GIFFPS
                             : taskSettings.CaptureSettings?.ScreenRecordingSettings?.FPS ?? 15;
                         var ffmpegOptions = taskSettings.CaptureSettings?.FFmpegOptions;
                         string? ffmpegPath = ResolveGifFFmpegPath(ffmpegOptions);
                         DebugHelper.WriteLine($"[GIF] FFmpegPath={(string.IsNullOrWhiteSpace(ffmpegPath) ? "(missing)" : ffmpegPath)}");
                         if (string.IsNullOrWhiteSpace(ffmpegPath))
                         {
                             TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "FFmpeg not found. GIF conversion skipped.");
                             DebugHelper.WriteLine("FFmpeg not found. GIF conversion skipped.");
                             try
                             {
                                 PlatformServices.Toast?.ShowToast(new Platform.Abstractions.ToastConfig
                                 {
                                     Title = "GIF Conversion Skipped",
                                     Text = "FFmpeg not found. Configure or download FFmpeg to enable GIF output.",
                                     Duration = 5f,
                                     Size = new SizeI(420, 120),
                                     AutoHide = true,
                                     LeftClickAction = Platform.Abstractions.ToastClickAction.CloseNotification
                                 });
                             }
                             catch
                             {
                                 // Ignore toast errors
                             }
                         }
                         var videoHelpers = new VideoHelpers(ffmpegPath);
                         string? statsMode = ffmpegOptions?.GIFStatsMode.ToString();
                         string? dither = ffmpegOptions?.GIFDither.ToString();
                         int bayerScale = ffmpegOptions?.GIFBayerScale ?? 2;
                         int maxWidth = ffmpegOptions?.GIFMaxWidth > 0 ? ffmpegOptions.GIFMaxWidth : -1;
                         bool paletteNew = ffmpegOptions != null &&
                             ffmpegOptions.GIFStatsMode == XerahS.Core.FFmpegPaletteGenStatsMode.single;
                         DebugHelper.WriteLine($"[GIF] Settings: fps={gifFps}, maxWidth={maxWidth}, statsMode={statsMode}, dither={dither}, bayerScale={bayerScale}, paletteNew={paletteNew}");
                         bool success = await videoHelpers.ConvertToGifAsync(
                             outputPath,
                             gifPath,
                             gifFps,
                             maxWidth,
                             statsMode,
                             dither,
                             bayerScale,
                             paletteNew);
                         DebugHelper.WriteLine($"[GIF] Conversion result: success={success}, output={(File.Exists(gifPath) ? gifPath : "(missing)")}");

                         if (success)
                         {
                             TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "Conversion successful. Switching result to GIF.");

                             // Delete original MP4 if conversion succeeded
                             try { File.Delete(outputPath); } catch { }

                             outputPath = gifPath;
                             Info.FilePath = outputPath;
                         }
                         else
                         {
                             TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "Conversion failed. Keeping MP4.");
                         }
                    }

                    // Handle After Capture tasks for recordings (manual handling since CaptureJobProcessor is for images)
                    if (taskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyImageToClipboard))
                    {
                         if (PlatformServices.IsInitialized && !string.IsNullOrEmpty(outputPath))
                         {
                             try
                             {
                                 // For files/recordings, "Copy Image" implies copying file to clipboard
                                 PlatformServices.Clipboard.SetFileDropList(new[] { outputPath });
                                 DebugHelper.WriteLine($"[GIF] Copied recording to clipboard: {outputPath}");
                             }
                             catch (Exception ex)
                             {
                                 DebugHelper.WriteException(ex, "Failed to copy recording to clipboard");
                             }
                         }
                    }

                    // Reuse upload pipeline for recordings; flag upload when AfterUpload tasks exist.
                    if (taskSettings.AfterUploadJob != AfterUploadTasks.None)
                    {
                        taskSettings.AfterCaptureJob |= AfterCaptureTasks.UploadImageToHost;
                    }

                    var uploadProcessor = new UploadJobProcessor();
                    await uploadProcessor.ProcessAsync(Info, _cancellationTokenSource.Token);

                    // Add to History with retry logic for transient failures
                    const int MaxRetries = 3;
                    bool historySaved = false;

                    for (int retry = 0; retry < MaxRetries; retry++)
                    {
                        try
                        {
                            var historyPath = SettingsManager.GetHistoryFilePath();
                            using var historyManager = new HistoryManagerSQLite(historyPath);
                            var historyItem = new HistoryItem
                            {
                                FilePath = outputPath,
                                FileName = Path.GetFileName(outputPath),
                                DateTime = DateTime.Now,
                                Type = "Video",
                                URL = metadata.UploadURL ?? string.Empty // Will be populated if upload succeeded
                            };

                            DebugHelper.WriteLine($"[HistoryTrace] Preparing to add item. URL='{historyItem.URL}', File='{historyItem.FileName}'");

                            await Task.Run(() => historyManager.AppendHistoryItem(historyItem));
                            DebugHelper.WriteLine($"Added recording to history: {historyItem.FileName} (URL: {historyItem.URL})");
                            historySaved = true;
                            break; // Success - exit retry loop
                        }
                        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 5 && retry < MaxRetries - 1)
                        {
                            // SQLITE_BUSY - database locked, retry after delay
                            DebugHelper.WriteLine($"History database busy, retry {retry + 1}/{MaxRetries}");
                            await Task.Delay(100 * (retry + 1));
                        }
                        catch (Exception ex)
                        {
                            DebugHelper.WriteException(ex, "Failed to add recording to history");

                            // Notify user on final failure
                            if (retry == MaxRetries - 1)
                            {
                                try
                                {
                                    PlatformServices.Toast?.ShowToast(new Platform.Abstractions.ToastConfig
                                    {
                                        Title = "History Save Failed",
                                        Text = "Recording completed but could not be added to history. Check disk space and logs.",
                                        Duration = 5f,
                                        Size = new SizeI(400, 120),
                                        AutoHide = true,
                                        LeftClickAction = Platform.Abstractions.ToastClickAction.CloseNotification
                                    });
                                }
                                catch
                                {
                                    // Ignore toast errors
                                }
                            }
                            break;
                        }
                    }

                    if (!historySaved)
                    {
                        DebugHelper.WriteLine("WARNING: Recording completed successfully but history record was not saved.");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed during recording workflow");

                // Show user-facing error message
                string errorMessage = ex switch
                {
                    FileNotFoundException => "FFmpeg not found. Please install FFmpeg to enable screen recording.",
                    PlatformNotSupportedException => "Screen recording is not supported on this system.",
                    InvalidOperationException when ex.Message.Contains("not available") =>
                        "Screen recording is not available. On Linux Wayland, ensure xdg-desktop-portal with ScreenCast support and PipeWire are installed.",
                    InvalidOperationException when ex.Message.Contains("initialization") =>
                        "Screen recording initialization failed. Check that required services are running.",
                    _ => $"Failed to start recording: {ex.Message}"
                };

                try
                {
                    PlatformServices.Toast?.ShowToast(new Platform.Abstractions.ToastConfig
                    {
                        Title = "Recording Failed",
                        Text = errorMessage,
                        Duration = 8f,
                        Size = new SizeI(450, 140),
                        AutoHide = true,
                        LeftClickAction = Platform.Abstractions.ToastClickAction.CloseNotification
                    });
                }
                catch
                {
                    // Ignore toast errors
                }

                throw;
            }
        }

        private async Task HandleStopRecordingAsync()
        {
             // Legacy handler - mapped to SignalStop in UI now
             await Task.CompletedTask;
        }

        private async Task HandleAbortRecordingAsync()
        {
             // Legacy handler
             await ScreenRecordingManager.Instance.AbortRecordingAsync();
        }

        private async Task HandlePauseRecordingAsync()
        {
             await ScreenRecordingManager.Instance.TogglePauseResumeAsync();
        }

        private static string? ResolveGifFFmpegPath(FFmpegOptions? ffmpegOptions)
        {
            if (ffmpegOptions != null && !string.IsNullOrWhiteSpace(ffmpegOptions.CLIPath))
            {
                return ffmpegOptions.CLIPath;
            }

            string detectedPath = PathsManager.GetFFmpegPath();
            return string.IsNullOrWhiteSpace(detectedPath) ? null : detectedPath;
        }

        #endregion
    }
}
