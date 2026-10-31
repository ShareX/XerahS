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

        /// <summary>
        /// Starts recording and waits for the stop signal. A restart request (ShareX #7255) discards
        /// the current take and records again with the same options, so the surrounding workflow
        /// (save, upload, history) only ever sees the final take.
        /// </summary>
        internal static async Task RecordUntilStoppedAsync(
            ScreenRecordingWorkflowCoordinator recordingCoordinator,
            RecordingOptions recordingOptions,
            Action<string?> onStarted)
        {
            string? requestedOutputPath = recordingOptions.OutputPath;
            while (true)
            {
                recordingOptions.OutputPath = requestedOutputPath;
                await recordingCoordinator.StartRecordingAsync(recordingOptions);
                recordingOptions.OutputPath = recordingCoordinator.PlannedOutputPath ?? recordingOptions.OutputPath;
                onStarted(recordingOptions.OutputPath);

                await recordingCoordinator.WaitForStopSignalAsync();
                if (!recordingCoordinator.ConsumeRestartRequest())
                {
                    return;
                }

                DebugHelper.WriteLine("Restarting recording: discarding the current take.");
                await recordingCoordinator.AbortRecordingAsync();
            }
        }

        private static ScreenRecordingWorkflowCoordinator CreateRecordingCoordinator()
        {
            return new ScreenRecordingWorkflowCoordinator(RecordingManagerService);
        }

        internal async Task HandleStartRecordingAsync(CaptureMode mode, IntPtr windowHandle = default, Rectangle? region = null)
        {
            var taskSettings = Info.TaskSettings ?? new TaskSettings();
            var metadata = Info.Metadata ?? new TaskMetadata();

            XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", $"HandleStartRecordingAsync Entry: mode={mode}, region={region}");

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
                    FFmpegOverridePath = ResolveRecordingFFmpegOverridePath(captureSettings.FFmpegOptions),
                    TargetWindowHandle = windowHandle,
                    UseModernCapture = captureSettings.UseModernCapture,
                    LinuxRecordingBackendPreference = ResolveLinuxRecordingBackendPreference(captureSettings)
                };

                // Set region if provided (for Region mode)
                if (region.HasValue)
                {
                    recordingOptions.Region = region.Value;
                    XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", $"Recording region set: {region.Value}");
                }

                // [2026-01-10T14:40:00+08:00] Align screen recording output with screenshot naming/destination using TaskHelpers.
                var recordingMetadata = metadata;
                string recordingsFolder = TaskHelpers.GetScreenshotsFolder(taskSettings, recordingMetadata);
                string fileName = TaskHelpers.GetFileName(taskSettings, "mp4", recordingMetadata);
                Directory.CreateDirectory(recordingsFolder);
                var resolvedPath = TaskHelpers.HandleExistsFile(recordingsFolder, fileName, taskSettings);
                if (string.IsNullOrWhiteSpace(resolvedPath))
                {
                    DebugHelper.WriteLine($"[PathTrace {Info.CorrelationId}] ScreenRecorder: HandleExistsFile returned empty path (user cancelled or Ask in non-interactive context). Aborting recording.");
                    return;
                }
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

                XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "Calling ScreenRecordingManager.StartRecordingAsync");
                DebugHelper.WriteLine($"Starting recording: Mode={mode}, Codec={recordingOptions.Settings?.Codec}, FPS={recordingOptions.Settings?.FPS}");
                DebugHelper.WriteLine($"Output path: {recordingOptions.OutputPath}");

                var recordingCoordinator = CreateRecordingCoordinator();
                await RecordUntilStoppedAsync(recordingCoordinator, recordingOptions, plannedPath =>
                {
                    Info.FilePath = plannedPath ?? Info.FilePath;
                    XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "Recording started; waiting for stop signal...");
                });
                XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "Stop signal received. Resuming...");

                // 3. Stop recording
                DebugHelper.WriteLine("Stopping recording...");
                string? outputPath = await recordingCoordinator.StopRecordingAsync();
                DebugHelper.WriteLine($"[GIF] StopRecordingAsync returned: {(string.IsNullOrEmpty(outputPath) ? "(null)" : outputPath)} (exists={(!string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))})");
                string? expectedOutputPath = recordingOptions.OutputPath;
                bool expectedOutputExists = !string.IsNullOrEmpty(expectedOutputPath) && File.Exists(expectedOutputPath);
                DebugHelper.WriteLine($"[RecordingFinalize] Expected output path: {(string.IsNullOrEmpty(expectedOutputPath) ? "(null)" : expectedOutputPath)} (exists={expectedOutputExists})");

                if (string.IsNullOrEmpty(outputPath) && !string.IsNullOrEmpty(Info.FilePath) && File.Exists(Info.FilePath))
                {
                    DebugHelper.WriteLine($"[GIF] StopRecordingAsync returned null but Info.FilePath exists. Recovering path: {Info.FilePath}");
                    outputPath = Info.FilePath;
                }

                if (!string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))
                    RecordingOutputValidator.EnsureUsable(outputPath);

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
                         XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "Converting video to GIF...");
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
                             XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "FFmpeg not found. GIF conversion skipped.");
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
                             XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "Conversion successful. Switching result to GIF.");

                             // Delete original MP4 if conversion succeeded
                             try { File.Delete(outputPath); } catch { }

                             outputPath = gifPath;
                             Info.FilePath = outputPath;
                         }
                         else
                         {
                             XerahS.Common.TroubleshootingHelper.Log(taskSettings.Job.ToString(), "WORKER_TASK", "Conversion failed. Keeping MP4.");
                         }
                    }

                    // Open VideoEditor when AnnotateMedia is checked, mirroring how AnnotateMedia opens ImageEditor for images
                    if (taskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AnnotateMedia)
                        && PlatformServices.IsInitialized && PlatformServices.UI != null)
                    {
                        string? ffmpegPath = ResolveGifFFmpegPath(taskSettings.CaptureSettings?.FFmpegOptions);
                        string? editedPath = await PlatformServices.UI.ShowVideoEditorAsync(outputPath, ffmpegPath);
                        if (!string.IsNullOrEmpty(editedPath) && File.Exists(editedPath))
                        {
                            outputPath = editedPath;
                            Info.FilePath = outputPath;
                            DebugHelper.WriteLine($"VideoEditor produced: {outputPath}");
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
                            var historyItem = CreateRecordingHistoryItem(Info, outputPath);

                            DebugHelper.WriteLine($"[HistoryTrace] Preparing to add item. URL='{historyItem.URL}', File='{historyItem.FileName}'");

                            bool appended = await Task.Run(() => historyManager.AppendHistoryItem(historyItem));
                            if (!appended)
                            {
                                throw new InvalidOperationException("The recording history row could not be saved.");
                            }

                            Info.HistoryItemId = historyItem.Id;
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
                        "Screen recording is not available. On Linux Wayland, ensure xdg-desktop-portal with ScreenCast support is available, PipeWire is running, and either FFmpeg pipewire, GStreamer pipewiresrc, or wf-recorder is installed.",
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

        internal async Task HandleStopRecordingAsync()
        {
             CreateRecordingCoordinator().SignalStop();
             await Task.CompletedTask;
        }

        internal async Task HandleAbortRecordingAsync()
        {
             // Legacy handler
             await CreateRecordingCoordinator().AbortRecordingAsync();
        }

        internal async Task HandlePauseRecordingAsync()
        {
             await CreateRecordingCoordinator().TogglePauseResumeAsync();
        }

        private static string? ResolveGifFFmpegPath(FFmpegOptions? ffmpegOptions)
        {
            if (ffmpegOptions?.OverrideCLIPath == true && !string.IsNullOrWhiteSpace(ffmpegOptions.CLIPath))
            {
                string configuredPath = ffmpegOptions.CLIPath.Trim().Trim('"', '\'');
                if (!string.IsNullOrWhiteSpace(configuredPath))
                {
                    try
                    {
                        configuredPath = FileHelpers.GetAbsolutePath(configuredPath);
                    }
                    catch
                    {
                        // Keep the user-provided value for diagnostics if normalization fails.
                    }

                    return configuredPath;
                }
            }

            string detectedPath = PathsManager.GetFFmpegPath();
            return string.IsNullOrWhiteSpace(detectedPath) ? null : detectedPath;
        }

        private static string? ResolveRecordingFFmpegOverridePath(FFmpegOptions? ffmpegOptions)
        {
            if (ffmpegOptions?.OverrideCLIPath == true && !string.IsNullOrWhiteSpace(ffmpegOptions.CLIPath))
            {
                string configuredPath = ffmpegOptions.CLIPath.Trim().Trim('"', '\'');
                if (!string.IsNullOrWhiteSpace(configuredPath))
                {
                    try
                    {
                        configuredPath = FileHelpers.GetAbsolutePath(configuredPath);
                    }
                    catch
                    {
                        // Keep the user-provided value if normalization fails.
                    }

                    return configuredPath;
                }
            }

            return null;
        }

        internal static HistoryItem CreateRecordingHistoryItem(TaskInfo info, string outputPath)
        {
            var historyItem = new HistoryItem
            {
                FilePath = outputPath,
                FileName = Path.GetFileName(outputPath),
                DateTime = DateTime.Now,
                Type = "Video",
                Host = info.UploaderHost ?? string.Empty,
                URL = info.Metadata?.UploadURL ?? string.Empty
            };

            var tags = info.GetTags();
            if (tags != null)
            {
                historyItem.Tags = new Dictionary<string, string?>(tags.Count);
                foreach (var pair in tags)
                {
                    historyItem.Tags[pair.Key] = pair.Value;
                }
            }

            return historyItem;
        }

        private static LinuxRecordingBackendPreference ResolveLinuxRecordingBackendPreference(TaskSettingsCapture captureSettings)
        {
            return captureSettings.LinuxRecordingBackendPreference ??
                (captureSettings.UseModernCapture
                    ? LinuxRecordingBackendPreference.Automatic
                    : LinuxRecordingBackendPreference.FFmpeg);
        }

        #endregion
    }
}
