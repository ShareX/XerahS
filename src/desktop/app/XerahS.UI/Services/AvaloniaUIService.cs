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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Media;
using System.IO;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Presentation.ViewModels;
using ShareX.ImageEditor.Presentation.Views;
using Omacut.Core;
using Omacut.Hosting;
using XerahS.Media;
using SkiaSharp;

namespace XerahS.UI.Services
{
    public class AvaloniaUIService : IUIService
    {
        private IDesktopTaskManager? _taskManager;
        private bool _wasMainWindowVisible;
        private Avalonia.Controls.WindowState _previousWindowState;

        public AvaloniaUIService()
        {
        }

        public AvaloniaUIService(IDesktopTaskManager taskManager)
        {
            _taskManager = taskManager;
        }

        public void Configure(IDesktopTaskManager taskManager)
        {
            _taskManager = taskManager;
        }

        public async Task HideMainWindowAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow;
                    if (mainWindow != null && mainWindow.IsVisible)
                    {
                        _wasMainWindowVisible = true;
                        _previousWindowState = mainWindow.WindowState;

                        // Minimize the window so it doesn't appear in screenshots
                        mainWindow.WindowState = Avalonia.Controls.WindowState.Minimized;
                        DebugHelper.WriteLine("AvaloniaUIService: Main window minimized before capture");
                    }
                    else
                    {
                        _wasMainWindowVisible = false;
                    }
                }
            });

            // Small delay to ensure window is fully minimized before capture starts
            await Task.Delay(150);
        }

        public async Task RestoreMainWindowAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_wasMainWindowVisible &&
                    Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow;
                    if (mainWindow != null)
                    {
                        // Restore to previous state
                        mainWindow.WindowState = _previousWindowState;
                        DebugHelper.WriteLine("AvaloniaUIService: Main window restored after capture");
                    }
                }
                _wasMainWindowVisible = false;
            });
        }

        public async Task<SKBitmap?> ShowEditorAsync(SKBitmap image, string? sourceFilePath = null, bool taskMode = false)
        {
            ImageEditorSessionResult? result = await ShowEditorSessionAsync(image, sourceFilePath, taskMode);
            result?.SourceImage?.Dispose();
            return result?.RenderedImage;
        }

        public async Task<ImageEditorSessionResult?> ShowEditorSessionAsync(
            SKBitmap image,
            string? sourceFilePath = null,
            bool taskMode = false,
            IReadOnlyList<Annotation>? annotations = null,
            bool restoredAnnotations = false)
        {
            EditorSessionOutcome outcome = await ShowEditorSessionCoreAsync(
                image, sourceFilePath, taskMode, annotations, restoredAnnotations);
            return outcome.Result;
        }

        public async Task<ImageEditorSessionResult?> ShowPrivacyEditorAsync(SKBitmap image)
        {
            EditorSessionOutcome outcome = await ShowEditorSessionCoreAsync(
                image, taskMode: true, privacyReview: true);
            return outcome.Result;
        }

        public Task<EditorSessionOutcome> ShowEditorSessionWithOutcomeAsync(SKBitmap image, bool taskMode = false)
        {
            return ShowEditorSessionCoreAsync(image, taskMode: taskMode);
        }

        private async Task<EditorSessionOutcome> ShowEditorSessionCoreAsync(
            SKBitmap image,
            string? sourceFilePath = null,
            bool taskMode = false,
            IReadOnlyList<Annotation>? annotations = null,
            bool restoredAnnotations = false,
            bool privacyReview = false)
        {
            if (_taskManager == null)
            {
                throw new InvalidOperationException("AvaloniaUIService requires an IDesktopTaskManager before showing the editor.");
            }

            var tcs = new TaskCompletionSource<EditorSessionOutcome>();
            var restoredAnnotationSnapshot = annotations?.Select(annotation => annotation.Clone()).ToList();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Create independent Editor Window
                var editorWindow = new Views.EditorWindow();

                // Create independent ViewModel for this editor instance
                var editorOptions = ThemeService.CreateImageEditorOptions(showExitConfirmation: !taskMode);
                var editorViewModel = new MainViewModel(editorOptions);
                editorViewModel.ShowFileMenu = !taskMode;
                editorViewModel.ShowTaskButtons = true;
                editorViewModel.UseContinueWorkflow = taskMode;
                editorViewModel.ShowBottomToolbar = true;
                editorViewModel.ShowStartScreen = !taskMode;
                editorViewModel.ApplicationName = AppResources.AppName;

                if (!privacyReview)
                {
                    // Wire up UploadRequested to trigger host app upload workflow
                    MainViewModelHelper.WireUploadRequested(editorViewModel, _taskManager, () =>
                    {
                        var editorView = editorWindow.FindControl<EditorView>("EditorViewControl");
                        return editorView?.GetSnapshot();
                    });

                    // Wire up CopyRequested to copy edited image (with annotations) to clipboard
                    MainViewModelHelper.WireCopyRequested(editorViewModel, () =>
                    {
                        var editorView = editorWindow.FindControl<EditorView>("EditorViewControl");
                        return editorView?.GetSnapshot();
                    });

                    // Wire up SaveRequested / SaveAsRequested for standalone editor window
                    Func<SkiaSharp.SKBitmap?> getSnapshot = () =>
                        editorWindow.FindControl<EditorView>("EditorViewControl")?.GetSnapshot();
                    MainViewModelHelper.WireSaveRequested(editorViewModel, getSnapshot, () => editorWindow);
                    MainViewModelHelper.WireSaveAsRequested(editorViewModel, getSnapshot, () => editorWindow);
                    MainViewModelHelper.WirePinRequested(editorViewModel, getSnapshot);

                }

                // Set DataContext BEFORE initializing preview so bindings update correctly
                editorWindow.DataContext = editorViewModel;

                // Initialize the preview image
                editorViewModel.UpdatePreview(image);
                if (!string.IsNullOrWhiteSpace(sourceFilePath))
                {
                    editorViewModel.ImageFilePath = sourceFilePath;
                    editorViewModel.IsDirty = false;
                }

                if (restoredAnnotationSnapshot?.Count > 0)
                {
                    editorWindow.Opened += (_, _) =>
                    {
                        var editorView = editorWindow.FindControl<EditorView>("EditorViewControl");
                        editorView?.RestoreAnnotations(restoredAnnotationSnapshot, resetHistory: true);
                        editorViewModel.IsDirty = false;
                    };
                }

                // Handle window closing to capture result
                editorWindow.Closing += (s, e) =>
                {
                    if (ShouldReturnNullForEditorClose(taskMode, editorWindow.IsCloseRequestedByViewModel, editorViewModel.TaskResult))
                    {
                        bool cancelled = IsTaskCancelRequested(taskMode, editorWindow.IsCloseRequestedByViewModel, editorViewModel.TaskResult);
                        tcs.TrySetResult(new EditorSessionOutcome(null, cancelled));
                        return;
                    }

                    try
                    {
                        var editorView = editorWindow.FindControl<EditorView>("EditorViewControl");

                        if (editorView != null)
                        {
                            var snapshot = editorView.GetSnapshot();
                            if (snapshot == null)
                            {
                                tcs.TrySetResult(new EditorSessionOutcome(null, Cancelled: false));
                            }
                            else
                            {
                                var source = editorView.GetSource();
                                var annotationSnapshot = editorView.GetAnnotationSnapshot().ToList();
                                tcs.TrySetResult(new EditorSessionOutcome(
                                    new ImageEditorSessionResult(snapshot, source, annotationSnapshot), Cancelled: false));
                            }
                        }
                        else
                        {
                            tcs.TrySetResult(new EditorSessionOutcome(null, Cancelled: false));
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, "Failed to get editor snapshot");
                        tcs.TrySetResult(new EditorSessionOutcome(null, Cancelled: false));
                    }
                };

                // Show the window
                editorWindow.Show();
            });

            return await tcs.Task;
        }

        internal static bool ShouldReturnNullForEditorClose(
            bool taskMode,
            bool closeRequestedByViewModel,
            MainViewModel.EditorTaskResult taskResult)
        {
            if (!closeRequestedByViewModel)
            {
                return true;
            }

            bool continueWithoutSave = taskResult == MainViewModel.EditorTaskResult.ContinueNoSave
                || taskResult == MainViewModel.EditorTaskResult.Cancel;

            return taskMode && continueWithoutSave;
        }

        internal static bool IsTaskCancelRequested(
            bool taskMode,
            bool closeRequestedByViewModel,
            MainViewModel.EditorTaskResult taskResult)
        {
            return taskMode
                && closeRequestedByViewModel
                && taskResult == MainViewModel.EditorTaskResult.Cancel;
        }

        public async Task<string?> ShowVideoEditorAsync(string videoPath, string? ffmpegPath)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                string? watermarkImage = null;
                try
                {
                    string detectedFfmpegPath = PathsManager.GetFFmpegPath();
                    var ffmpegResolution = VideoEditorFfmpegResolver.Resolve(ffmpegPath, detectedFfmpegPath);
                    LogVideoEditorFfmpegResolution(ffmpegPath, detectedFfmpegPath, ffmpegResolution);

                    string ffprobePath = string.Empty;
                    if (ffmpegResolution.IsAvailable)
                    {
                        try
                        {
                            ffprobePath = await VideoEditorFfprobeResolver.EnsureAvailableAsync(
                                ffmpegResolution.ConfiguredPath,
                                message => DebugHelper.WriteLine($"[VideoEditor] {message}"));

                            if (!string.IsNullOrWhiteSpace(ffprobePath))
                            {
                                DebugHelper.WriteLine($"[VideoEditor] Using FFprobe at: {ffprobePath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugHelper.WriteException(ex, "Failed to resolve FFprobe for video editor");
                        }
                    }

                    VideoWatermarkSettings? watermark = VideoEditorWatermarkMapper.FromDefaultTaskSettings();
                    watermarkImage = VideoWatermarkRenderer.ResolveImage(watermark, VideoWatermarkWorkDirectory);

                    var options = new OmacutEditorOptions
                    {
                        VideoPath = videoPath,
                        FfmpegPath = ffmpegResolution.IsAvailable ? ffmpegResolution.ConfiguredPath : null,
                        FfprobePath = string.IsNullOrWhiteSpace(ffprobePath) ? null : ffprobePath,
                        AccentColor = ResolveAccentColor(),
                        WindowTitle = AppResources.AppName,
                        Watermark = watermarkImage == null
                            ? null
                            : new WatermarkOverlay(watermarkImage, watermark!.Opacity, watermark.PositionX, watermark.PositionY),
                        // Hand the edited file straight back to the workflow (upload, copy, ...).
                        CloseAfterExport = true,
                        AllowOpeningOtherFiles = false,
                        ConfigureWindow = window => window.Icon = TryGetDialogOwner()?.Icon,
                        Log = message => DebugHelper.WriteLine(message),
                    };

                    string? exported = await OmacutEditor.ShowAsync(options);
                    if (!string.IsNullOrEmpty(exported))
                    {
                        DebugHelper.WriteLine($"[VideoEditor] Exported: {exported}");
                    }

                    return exported;
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "Failed to open video editor");
                    await ShowVideoEditorStartupErrorAsync(ex.Message);
                    return null;
                }
                finally
                {
                    if (watermarkImage != null && watermarkImage.StartsWith(VideoWatermarkWorkDirectory, StringComparison.Ordinal))
                    {
                        try
                        {
                            File.Delete(watermarkImage);
                        }
                        catch (IOException)
                        {
                        }
                        catch (UnauthorizedAccessException)
                        {
                        }
                    }
                }
            });
        }

        private static string VideoWatermarkWorkDirectory =>
            Path.Combine(Path.GetTempPath(), "XerahS", "video-watermarks");

        private static void LogVideoEditorFfmpegResolution(
            string? hostPath,
            string? detectedPath,
            (string ConfiguredPath, bool IsAvailable, string Source) resolution)
        {
            string hostCandidate = string.IsNullOrWhiteSpace(hostPath) ? "(empty)" : hostPath;
            string detectedCandidate = string.IsNullOrWhiteSpace(detectedPath) ? "(empty)" : detectedPath;
            string configuredPath = string.IsNullOrWhiteSpace(resolution.ConfiguredPath)
                ? "(not set)"
                : resolution.ConfiguredPath;

            if (resolution.IsAvailable)
            {
                DebugHelper.WriteLine(
                    $"[VideoEditor] Using FFmpeg at: {configuredPath} (source: {resolution.Source}, hostCandidate: {hostCandidate}, detectedCandidate: {detectedCandidate})");
            }
            else
            {
                DebugHelper.WriteLine(
                    $"[VideoEditor] FFmpeg unavailable. Source={resolution.Source}, hostCandidate={hostCandidate}, detectedCandidate={detectedCandidate}, configuredPath={configuredPath}");
            }
        }

        private static string? ResolveAccentColor()
        {
            // Match the XerahS accent; on Omarchy the editor follows the desktop theme instead.
            var app = Avalonia.Application.Current;
            if (app != null && app.TryGetResource("SystemAccentColor", app.ActualThemeVariant, out object? value) && value is Color color)
            {
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }

            return null;
        }

        private static async Task ShowVideoEditorStartupErrorAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var viewModel = new SimplePromptViewModel
                {
                    Title = "Video editor",
                    Message = "The video editor could not start." + Environment.NewLine + Environment.NewLine + message,
                    ShowCancel = false,
                    PrimaryButtonText = "Close",
                    IsError = true
                };

                await ModalDialogHost.ShowAsync(
                    viewModel,
                    set => viewModel.CloseRequested = set,
                    dismissResult: false,
                    debugSource: "VideoEditorStartupError");
            });
        }

        private static Window? TryGetDialogOwner()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }

            return null;
        }

        private static bool CanUseDialogOwner(Window? owner) =>
            owner != null &&
            owner.IsVisible &&
            owner.WindowState != Avalonia.Controls.WindowState.Minimized &&
            owner.ShowInTaskbar;

        public async Task<(AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel, AfterCaptureQuickAction QuickAction)> ShowAfterCaptureWindowAsync(
            SKBitmap image, AfterCaptureTasks afterCapture, AfterUploadTasks afterUpload)
            => (await ShowAfterCaptureWindowWithPrivacyAsync(image, afterCapture, afterUpload, false)).Tasks;

        public async Task<((AfterCaptureTasks Capture, AfterUploadTasks Upload, bool Cancel, AfterCaptureQuickAction QuickAction) Tasks, bool PrivacyEnabled)>
            ShowAfterCaptureWindowWithPrivacyAsync(SKBitmap image, AfterCaptureTasks afterCapture, AfterUploadTasks afterUpload, bool privacyEnabled)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var viewModel = new AfterCaptureViewModel(image, afterCapture, afterUpload) { PrivacyMode = privacyEnabled };

                // Standalone, unowned window: hosting this in the main window's modal overlay
                // (or giving it an owner) raised the XerahS main window after every capture.
                var window = new Views.SurfaceWindow
                {
                    Title = "After Capture Tasks",
                    Icon = TryGetDialogOwner()?.Icon,
                    Width = 960,
                    Height = 640,
                    MinWidth = 760,
                    MinHeight = 520,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = new Views.AfterCaptureWindow { DataContext = viewModel }
                };

                var closedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                viewModel.RequestClose += () => window.Close();
                window.Closed += (_, _) => closedTcs.TrySetResult(true);
                window.Opened += (_, _) => window.Activate();
                window.KeyDown += (_, e) =>
                {
                    if (e.Key == Avalonia.Input.Key.Escape)
                    {
                        window.Close();
                    }
                };
                window.Show();
                DebugHelper.WriteLine($"[{nameof(AfterCaptureViewModel)}] Window opened");
                await closedTcs.Task;

                return ((viewModel.AfterCaptureTasks, viewModel.AfterUploadTasks, viewModel.Cancelled, viewModel.QuickAction), viewModel.PrivacyMode);
            });
        }

        public async Task ShowAfterUploadWindowAsync(AfterUploadWindowInfo info)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var viewModel = new AfterUploadViewModel(info);
                var window = new Views.AfterUploadWindow
                {
                    DataContext = viewModel
                };

                viewModel.RequestClose += () => window.Close();
                window.Closed += (_, _) => viewModel.Dispose();

                Window? owner = null;
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    owner = desktop.MainWindow;
                }

                bool canUseOwner = owner != null && owner.IsVisible &&
                                   owner.WindowState != Avalonia.Controls.WindowState.Minimized &&
                                   owner.ShowInTaskbar;

                if (canUseOwner)
                {
                    window.Show(owner!);
                }
                else
                {
                    window.Show();
                }
            });
        }

        public async Task<SendToPromptResult> ShowSendToPromptAsync(SendToSelection selection)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var viewModel = new SendToPromptViewModel(selection, SettingsManager.Settings);

                await ModalDialogHost.ShowUntilClosedAsync(
                    viewModel,
                    set => viewModel.RequestClose += () => set(),
                    debugSource: nameof(SendToPromptViewModel));

                return viewModel.Result;
            });
        }

        public async Task ExecuteSendToActionAsync(SendToAction action, SendToSelection selection, SendToPromptResult? decision = null)
        {
            if (action is SendToAction.Cancel or SendToAction.UploadNow)
            {
                return;
            }

            Window? owner = TryGetDialogOwner();
            SendToPromptResult effectiveDecision = decision ?? new SendToPromptResult
            {
                Action = action,
                FolderPolicy = SettingsManager.Settings.SendToFolderPolicy,
                RememberScope = XerahS.Core.SendTo.SendToPolicyResolver.GetRememberScope(selection),
                BatchExecutionPolicy = SettingsManager.Settings.SendToBatchExecutionPolicy,
                BatchConfirmThreshold = SettingsManager.Settings.SendToBatchConfirmThreshold
            };

            switch (action)
            {
                case SendToAction.OpenUploadContent:
                    await UploadContentToolService.ShowSelectionAsync(
                        selection.FilePaths,
                        selection.FolderPaths,
                        owner,
                        effectiveDecision.FolderPolicy);
                    break;

                case SendToAction.OpenImageEditor:
                    await OpenSelectedImagesInEditorAsync(selection, effectiveDecision);
                    break;

                case SendToAction.PinToScreen:
                    await PinSelectedImagesAsync(selection, effectiveDecision);
                    break;

                case SendToAction.IndexFolders:
                    await OpenSelectedFoldersInIndexFolderAsync(selection, owner);
                    break;
            }
        }

        private async Task OpenSelectedImagesInEditorAsync(SendToSelection selection, SendToPromptResult decision)
        {
            if (!selection.CanOpenImageEditor)
            {
                return;
            }

            if (!await ShouldRunImageBatchAsync(selection, decision, "Image Editor"))
            {
                return;
            }

            if (decision.BatchExecutionPolicy == SendToBatchExecutionPolicy.OpenAllImmediately)
            {
                List<Task> editorTasks = [];
                foreach (var filePath in selection.FilePaths ?? Array.Empty<string>())
                {
                    editorTasks.Add(OpenImageFileInEditorAsync(filePath, (bitmap, path) => ShowEditorAsync(bitmap, path)));
                }

                await Task.WhenAll(editorTasks);
                return;
            }

            foreach (var filePath in selection.FilePaths ?? Array.Empty<string>())
            {
                await OpenImageFileInEditorAsync(filePath, (bitmap, path) => ShowEditorAsync(bitmap, path));
            }
        }

        private static async Task OpenImageFileInEditorAsync(
            string? filePath,
            Func<SKBitmap, string?, Task<SKBitmap?>> showEditorAsync)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            using SKBitmap? bitmap = SkiaSharp.SKBitmap.Decode(filePath);
            if (bitmap == null)
            {
                return;
            }

            using SKBitmap? renderedImage = await showEditorAsync(bitmap, filePath);
        }

        private static async Task PinSelectedImagesAsync(SendToSelection selection, SendToPromptResult decision)
        {
            if (!selection.CanPinToScreen)
            {
                return;
            }

            if (!await ShouldRunImageBatchAsync(selection, decision, "Pin to Screen"))
            {
                return;
            }

            PinToScreenToolService.PinFilesResult pinResult = await PinToScreenToolService.PinFilesAsync(selection.FilePaths, showToast: false);
            DebugHelper.WriteLine(
                $"Shell integration: Pin-to-screen Send-to batch requested={selection.FilePaths.Count}, " +
                $"pinned={pinResult.PinnedCount}, skipped={pinResult.SkippedCount}.");

            if (PlatformServices.IsInitialized && PlatformServices.IsToastServiceInitialized)
            {
                string text = pinResult.SkippedCount > 0
                    ? $"Pinned {pinResult.PinnedCount} image(s); skipped {pinResult.SkippedCount}."
                    : $"Pinned {pinResult.PinnedCount} image(s).";

                PlatformServices.Toast.ShowToast(new ToastConfig
                {
                    Title = "Send-to complete",
                    Text = text,
                    Duration = 3f,
                    AutoHide = true
                });
            }
        }

        private static async Task<bool> ShouldRunImageBatchAsync(SendToSelection selection, SendToPromptResult decision, string actionName)
        {
            int itemCount = selection.FilePaths.Count;
            if (!XerahS.Core.SendTo.SendToPolicyResolver.RequiresBatchConfirmation(decision, itemCount))
            {
                return true;
            }

            int threshold = XerahS.Core.SendTo.SendToPolicyResolver.NormalizeBatchThreshold(decision.BatchConfirmThreshold);
            bool confirmed = await new AvaloniaDialogServiceAdapter().ShowConfirmationAsync(
                $"Open {itemCount} images?",
                $"This remembered Send-to {actionName} action would open {itemCount} items, which exceeds the confirmation threshold of {threshold}. Open them now?");

            if (!confirmed)
            {
                DebugHelper.WriteLine(
                    $"Shell integration: Skipped remembered Send-to {actionName} batch because {itemCount} item(s) exceed threshold {threshold} and the user declined confirmation.");
                return false;
            }

            DebugHelper.WriteLine(
                $"Shell integration: Confirmed remembered Send-to {actionName} batch of {itemCount} item(s) over threshold {threshold}.");
            return true;
        }

        private async Task OpenSelectedFoldersInIndexFolderAsync(SendToSelection selection, Window? owner)
        {
            if (!selection.CanIndexFolders)
            {
                return;
            }

            foreach (var folderPath in selection.FolderPaths ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                {
                    continue;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var viewModel = UiViewModelFactoryAccessor.GetRequired().CreateIndexFolderViewModel();
                    viewModel.FolderPath = folderPath;

                    var window = new Views.IndexFolderView
                    {
                        DataContext = viewModel
                    };

                    if (CanUseDialogOwner(owner))
                    {
                        window.Show(owner!);
                    }
                    else
                    {
                        window.Show();
                    }

                    if (viewModel.CanStartIndexing)
                    {
                        _ = viewModel.IndexFolderCommand.ExecuteAsync(null);
                    }
                });
            }
        }

        public async Task ShowOcrWindowAsync(SKBitmap image)
        {
            SKBitmap? ocrImage = image.Copy();
            if (ocrImage == null)
            {
                DebugHelper.WriteLine("OCR window skipped: failed to clone image.");
                return;
            }

            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var viewModel = new OcrViewModel(ocrImage);

                // Wire the SelectRegion callback so users can re-capture inside the OCR window
                viewModel.SelectRegionRequested = async () =>
                {
                    try
                    {
                        await Task.Delay(300); // Allow window to minimize
                        var captureSettings = SettingsManager.DefaultTaskSettings?.CaptureSettings
                            ?? new TaskSettingsCapture();
                        var captureOptions = new CaptureOptions
                        {
                            UseModernCapture = captureSettings.UseModernCapture,
                            LinuxRegionSelectorPreference = captureSettings.LinuxRegionSelectorPreference,
                            MacOSRegionSelectorPreference = captureSettings.MacOSRegionSelectorPreference,
                            MacOSPlayCaptureSound = captureSettings.MacOSPlayCaptureSound,
                            ShowCursor = captureSettings.ShowCursor,
                            CaptureTransparent = captureSettings.CaptureTransparent,
                            CaptureShadow = captureSettings.CaptureShadow,
                            CaptureClientArea = captureSettings.CaptureClientArea
                        };
                        return await PlatformServices.ScreenCapture.CaptureRegionAsync(captureOptions);
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, "OCR region capture");
                        return null;
                    }
                };

                var window = new Views.OcrWindow
                {
                    DataContext = viewModel
                };

                Window? owner = null;
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    owner = desktop.MainWindow;
                }

                bool canUseOwner = owner != null && owner.IsVisible &&
                                   owner.WindowState != Avalonia.Controls.WindowState.Minimized &&
                                   owner.ShowInTaskbar;

                    if (canUseOwner)
                    {
                        window.Show(owner!);
                    }
                    else
                    {
                        window.Show();
                    }
                });
            }
            catch
            {
                ocrImage.Dispose();
                throw;
            }
        }

        public async Task ShowAnalyzerWindowAsync(SKBitmap image)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var viewModel = new ImageAnalyzerViewModel();
                viewModel.SetInputImage(image);

                var window = new Views.ImageAnalyzerWindow();
                window.Initialize(viewModel);

                Window? owner = null;
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    owner = desktop.MainWindow;
                }

                bool canUseOwner = owner != null && owner.IsVisible &&
                                   owner.WindowState != Avalonia.Controls.WindowState.Minimized &&
                                   owner.ShowInTaskbar;

                if (canUseOwner)
                {
                    window.Show(owner!);
                }
                else
                {
                    window.Show();
                }
            });
        }
    }
}
