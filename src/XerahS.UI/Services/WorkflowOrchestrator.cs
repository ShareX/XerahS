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

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ShareX.ImageEditor.ViewModels;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public sealed class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private readonly object _uploadTitleLock = new();
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private Core.Hotkeys.WorkflowManager? _workflowManager;
    private int _activeUploadCount;
    private string _baseTitle = AppResources.ProductNameWithVersion;

    public Core.Hotkeys.WorkflowManager? WorkflowManager => _workflowManager;

    public void Start(IClassicDesktopStyleApplicationLifetime desktop, string baseTitle)
    {
        _desktop = desktop;
        _baseTitle = string.IsNullOrWhiteSpace(baseTitle) ? AppResources.ProductNameWithVersion : baseTitle;

        ConfigureWorkerTaskCallbacks();
        InitializeHotkeys();

        Core.Managers.TaskManager.Instance.TaskCompleted -= OnWorkflowTaskCompleted;
        Core.Managers.TaskManager.Instance.TaskStarted -= OnWorkflowTaskStarted;
        Core.Managers.TaskManager.Instance.TaskCompleted += OnWorkflowTaskCompleted;
        Core.Managers.TaskManager.Instance.TaskStarted += OnWorkflowTaskStarted;
    }

    private void ConfigureWorkerTaskCallbacks()
    {
        Core.Tasks.WorkerTask.ShowWindowSelectorCallback = ShowWindowSelectorAsync;
        Core.Tasks.WorkerTask.ShowOpenFileDialogCallback = ShowOpenFileDialogAsync;
        Core.Tasks.WorkerTask.HandleToolWorkflowCallback = HandleToolWorkflowAsync;

        Core.Tasks.WorkerTask.OpenMainWindowCallback = () =>
        {
            Dispatcher.UIThread.InvokeAsync(OpenMainWindow);
        };

        Core.Tasks.WorkerTask.OpenHistoryCallback = _ =>
        {
            Dispatcher.UIThread.InvokeAsync(OpenHistory);
        };

        Core.Tasks.WorkerTask.ExitApplicationCallback = () =>
        {
            Dispatcher.UIThread.InvokeAsync(() => _desktop?.Shutdown());
        };

        Core.Tasks.WorkerTask.ToggleHotkeysCallback = ToggleHotkeys;
    }

    private async Task<XerahS.Platform.Abstractions.WindowInfo?> ShowWindowSelectorAsync()
    {
        var tcs = new TaskCompletionSource<XerahS.Platform.Abstractions.WindowInfo?>();

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var viewModel = new WindowSelectorViewModel();
                var dialog = new Window
                {
                    Title = "Select Window to Capture",
                    Width = 400,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new WindowSelectorDialog { DataContext = viewModel }
                };

                viewModel.OnWindowSelected = window =>
                {
                    tcs.TrySetResult(window);
                    dialog.Close();
                };

                viewModel.OnCancelled = () =>
                {
                    tcs.TrySetResult(null);
                    dialog.Close();
                };

                if (_desktop?.MainWindow != null)
                {
                    dialog.ShowDialog(_desktop.MainWindow);
                }
                else
                {
                    dialog.Show();
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to show window selector");
                tcs.TrySetResult(null);
            }
        });

        return await tcs.Task;
    }

    private async Task<string?> ShowOpenFileDialogAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(_desktop?.MainWindow);
                if (topLevel == null)
                {
                    tcs.TrySetResult(null);
                    return;
                }

                var options = new FilePickerOpenOptions
                {
                    Title = "Select File to Upload",
                    AllowMultiple = false,
                    SuggestedStartLocation = await topLevel.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Desktop)
                };

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                tcs.TrySetResult(files.Count >= 1 ? files[0].TryGetLocalPath() : null);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to show open file dialog");
                tcs.TrySetResult(null);
            }
        });

        return await tcs.Task;
    }

    private async Task HandleToolWorkflowAsync(WorkflowType workflowType)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = _desktop?.MainWindow;

            if (workflowType is WorkflowType.ColorPicker or WorkflowType.ScreenColorPicker)
            {
                await ColorPickerToolService.HandleWorkflowAsync(workflowType, owner);
            }
            else if (workflowType == WorkflowType.OCR)
            {
                await OcrToolService.HandleWorkflowAsync(workflowType, owner);
            }
            else if (workflowType == WorkflowType.ScrollingCapture)
            {
                await ScrollingCaptureToolService.HandleWorkflowAsync(workflowType, owner);
            }
            else if (workflowType == WorkflowType.ImageEditor)
            {
                await OpenImageEditorAsync(owner);
            }
            else if (workflowType == WorkflowType.HashCheck)
            {
                await HashCheckToolService.HandleWorkflowAsync(workflowType, owner);
            }
            else if (workflowType is WorkflowType.PinToScreen
                or WorkflowType.PinToScreenFromScreen
                or WorkflowType.PinToScreenFromClipboard
                or WorkflowType.PinToScreenFromFile
                or WorkflowType.PinToScreenCloseAll)
            {
                await PinToScreenToolService.HandleWorkflowAsync(workflowType, owner);
            }
            else if (workflowType == WorkflowType.MonitorTest)
            {
                await MonitorTestToolService.HandleWorkflowAsync(workflowType, owner);
            }
            else if (workflowType == WorkflowType.Ruler)
            {
                await RulerToolService.HandleWorkflowAsync(workflowType, owner);
            }
            else if (workflowType is WorkflowType.AutoCapture
                or WorkflowType.StartAutoCapture
                or WorkflowType.StopAutoCapture)
            {
                await AutoCaptureToolService.HandleWorkflowAsync(workflowType, owner);
            }
            else if (workflowType is WorkflowType.ClipboardUploadWithContentViewer
                or WorkflowType.ClipboardViewer)
            {
                await UploadContentToolService.HandleWorkflowAsync(workflowType, owner);
            }
            else if (workflowType is WorkflowType.ImageCombiner
                or WorkflowType.ImageSplitter
                or WorkflowType.ImageThumbnailer
                or WorkflowType.VideoConverter
                or WorkflowType.VideoThumbnailer
                or WorkflowType.AnalyzeImage)
            {
                await MediaToolsToolService.HandleWorkflowAsync(workflowType, owner);
            }
            else if (workflowType is WorkflowType.QRCode
                or WorkflowType.QRCodeDecodeFromScreen
                or WorkflowType.QRCodeScanRegion)
            {
                await QrCodeToolService.HandleWorkflowAsync(workflowType, owner);
            }
            else
            {
                DebugHelper.WriteLine($"Unhandled tool workflow callback: {workflowType}");
            }
        });
    }

    private void OpenMainWindow()
    {
        if (_desktop?.MainWindow is not MainWindow mainWindow)
        {
            return;
        }

        mainWindow.ShowInTaskbar = true;

        if (!mainWindow.IsVisible)
        {
            mainWindow.Show();
        }

        if (mainWindow.WindowState == Avalonia.Controls.WindowState.Minimized)
        {
            mainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
        }

        mainWindow.Activate();
        mainWindow.Focus();
    }

    private void OpenHistory()
    {
        if (_desktop?.MainWindow is MainWindow mainWindow)
        {
            mainWindow.NavigateToHistory();
        }
    }

    private void ToggleHotkeys()
    {
        var config = Core.SettingsManager.Settings;
        if (config == null)
        {
            return;
        }

        config.DisableHotkeys = !config.DisableHotkeys;
        _workflowManager?.ToggleHotkeys(config.DisableHotkeys);
        DebugHelper.WriteLine($"Hotkeys {(config.DisableHotkeys ? "disabled" : "enabled")}");
    }

    private void InitializeHotkeys()
    {
        if (!PlatformServices.IsInitialized)
        {
            return;
        }

        try
        {
            var hotkeyService = PlatformServices.Hotkey;
            _workflowManager = new Core.Hotkeys.WorkflowManager(hotkeyService);
            _workflowManager.HotkeyTriggered += HotkeyManager_HotkeyTriggered;

            var hotkeys = Core.SettingsManager.WorkflowsConfig.Hotkeys;

            if (hotkeys == null || hotkeys.Count == 0)
            {
                hotkeys = Core.Hotkeys.WorkflowManager.GetDefaultWorkflowList();
                Core.SettingsManager.WorkflowsConfig.Hotkeys = hotkeys;
            }

            _workflowManager.UpdateHotkeys(hotkeys);
            DebugHelper.WriteLine($"Initialized hotkey manager with {hotkeys.Count} hotkeys from configuration");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to initialize hotkeys");
        }
    }

    private void OnTaskCompleted(object? sender, EventArgs e)
    {
        if (sender is Core.Tasks.WorkerTask task &&
            task.Info?.Metadata?.Image != null &&
            _desktop?.MainWindow?.DataContext is MainViewModel viewModel)
        {
            viewModel.UpdatePreview(task.Info.Metadata.Image);
            DebugHelper.WriteLine($"Updated preview from task completion: {task.Info.Metadata.Image.Width}x{task.Info.Metadata.Image.Height}");
        }
    }

    private async void HotkeyManager_HotkeyTriggered(object? sender, Core.Hotkeys.WorkflowSettings settings)
    {
        DebugHelper.WriteLine($"Hotkey triggered: {settings} (ID: {settings?.Id ?? "null"})");

        if (settings == null)
        {
            return;
        }

        string category = settings.Job.GetHotkeyCategory();
        bool isCaptureJob = category == EnumExtensions.WorkflowType_Category_ScreenCapture ||
                            category == EnumExtensions.WorkflowType_Category_ScreenRecord;

        if (!isCaptureJob && _desktop?.MainWindow is MainWindow immediateMainWindow)
        {
            bool isWindowVisible = immediateMainWindow.IsVisible &&
                                   immediateMainWindow.WindowState != Avalonia.Controls.WindowState.Minimized &&
                                   immediateMainWindow.ShowInTaskbar &&
                                   !SettingsManager.Settings.SilentRun;

            if (isWindowVisible)
            {
                immediateMainWindow.NavigateToEditor();
            }
        }

        void HandleTaskCompleted(object? s, Core.Tasks.WorkerTask task)
        {
            Core.Managers.TaskManager.Instance.TaskCompleted -= HandleTaskCompleted;
            OnTaskCompleted(task, EventArgs.Empty);

            bool isScreenRecord = category == EnumExtensions.WorkflowType_Category_ScreenRecord;

            if (isCaptureJob && !isScreenRecord && task.IsSuccessful && _desktop?.MainWindow is MainWindow mainWindowAfterCapture)
            {
                bool isWindowVisible = mainWindowAfterCapture.IsVisible &&
                                       mainWindowAfterCapture.WindowState != Avalonia.Controls.WindowState.Minimized &&
                                       mainWindowAfterCapture.ShowInTaskbar &&
                                       !SettingsManager.Settings.SilentRun;

                if (isWindowVisible)
                {
                    mainWindowAfterCapture.NavigateToEditor();
                }
            }
        }

        Core.Managers.TaskManager.Instance.TaskCompleted += HandleTaskCompleted;

        if (settings.Job == Core.WorkflowType.CustomWindow)
        {
            DebugHelper.WriteLine($"[DEBUG] Hotkey triggered for CustomWindow. Configured title: '{settings.TaskSettings?.CaptureSettings?.CaptureCustomWindow}'");
        }

        bool isRecordingHotkey = settings.Job == Core.WorkflowType.ScreenRecorder ||
                                 settings.Job == Core.WorkflowType.ScreenRecorderActiveWindow ||
                                 settings.Job == Core.WorkflowType.ScreenRecorderCustomRegion ||
                                 settings.Job == Core.WorkflowType.StopScreenRecording ||
                                 settings.Job == Core.WorkflowType.StartScreenRecorder ||
                                 settings.Job == Core.WorkflowType.ScreenRecorderGIF ||
                                 settings.Job == Core.WorkflowType.ScreenRecorderGIFActiveWindow ||
                                 settings.Job == Core.WorkflowType.ScreenRecorderGIFCustomRegion ||
                                 settings.Job == Core.WorkflowType.StartScreenRecorderGIF;

        if (settings.Job == Core.WorkflowType.PauseScreenRecording &&
            (Core.Managers.ScreenRecordingManager.Instance.IsRecording || Core.Managers.ScreenRecordingManager.Instance.IsPaused))
        {
            DebugHelper.WriteLine("Pause/Resume hotkey triggered - toggling recording pause state...");
            await Core.Managers.ScreenRecordingManager.Instance.TogglePauseResumeAsync();
            return;
        }

        if (settings.Job == Core.WorkflowType.AbortScreenRecording &&
            (Core.Managers.ScreenRecordingManager.Instance.IsRecording || Core.Managers.ScreenRecordingManager.Instance.IsPaused))
        {
            DebugHelper.WriteLine("Abort hotkey triggered - aborting recording...");
            await Core.Managers.ScreenRecordingManager.Instance.AbortRecordingAsync();
            return;
        }

        if (isRecordingHotkey && (Core.Managers.ScreenRecordingManager.Instance.IsRecording || Core.Managers.ScreenRecordingManager.Instance.IsPaused))
        {
            DebugHelper.WriteLine("Screen Recording active - flagging Stop Signal to existing task...");
            Core.Managers.ScreenRecordingManager.Instance.SignalStop();
        }
        else
        {
            await Core.Helpers.TaskHelpers.ExecuteWorkflow(settings, settings.Id);
        }
    }

    private void OnWorkflowTaskCompleted(object? sender, Core.Tasks.WorkerTask task)
    {
        if (!task.IsSuccessful)
        {
            return;
        }

        var taskSettings = task.Info?.TaskSettings ?? new TaskSettings();
        if (taskSettings.GeneralSettings?.ShowToastNotificationAfterTaskCompleted != true)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var generalSettings = taskSettings.GeneralSettings;
                var filePath = task.Info?.FilePath;
                var url = task.Info?.Result?.URL ?? task.Info?.Result?.ShortenedURL;
                var errorDetails = task.Error?.ToString();

                string? title;
                string? text;

                if (task.Info?.Result?.IsError == true)
                {
                    title = "Task Failed";
                    text = task.Info.Result.ToString();
                    var uploaderErrors = task.Info.Result.ErrorsToString();
                    if (!string.IsNullOrWhiteSpace(uploaderErrors))
                    {
                        errorDetails = uploaderErrors;
                    }
                    else if (!string.IsNullOrWhiteSpace(task.Info.Result.Response))
                    {
                        errorDetails = task.Info.Result.Response;
                    }
                }
                else if (!string.IsNullOrEmpty(url))
                {
                    title = "Upload Completed";
                    text = url;
                }
                else
                {
                    title = "Task Completed";
                    text = task.Info?.FileName ?? "Operation completed successfully.";
                }

                string? imagePath = null;
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath) && FileHelpers.IsImageFile(filePath))
                {
                    imagePath = filePath;
                }

                var toastConfig = new ToastConfig
                {
                    Title = title,
                    Text = text,
                    ErrorDetails = errorDetails,
                    ImagePath = imagePath,
                    FilePath = filePath,
                    URL = url,
                    Duration = generalSettings.ToastWindowDuration,
                    FadeDuration = generalSettings.ToastWindowFadeDuration,
                    Placement = generalSettings.ToastWindowPlacement,
                    Size = generalSettings.ToastWindowSize,
                    LeftClickAction = generalSettings.ToastWindowLeftClickAction,
                    RightClickAction = generalSettings.ToastWindowRightClickAction,
                    MiddleClickAction = generalSettings.ToastWindowMiddleClickAction,
                    AutoHide = generalSettings.ToastWindowAutoHide
                };

                DebugHelper.WriteLine($"Showing toast: {title} - {text}");

                if (PlatformServices.IsToastServiceInitialized)
                {
                    PlatformServices.Toast.ShowToast(toastConfig);
                }
                else
                {
                    try
                    {
                        PlatformServices.Notification.ShowNotification(title ?? "ShareX", text ?? "Task completed");
                    }
                    catch (InvalidOperationException)
                    {
                        DebugHelper.WriteLine("Toast and notification services not available.");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to show workflow notification");
            }
        });
    }

    private void OnWorkflowTaskStarted(object? sender, Core.Tasks.WorkerTask task)
    {
        if (!task.Info.IsUploadJob)
        {
            return;
        }

        void HandleProgress(XerahS.Uploaders.ProgressManager progress)
        {
            UpdateMainWindowTitle(progress.Percentage);
        }

        void HandleCompleted(object? s, EventArgs e)
        {
            task.Info.UploadProgressChanged -= HandleProgress;
            task.TaskCompleted -= HandleCompleted;
            DecrementActiveUploads();
        }

        task.Info.UploadProgressChanged += HandleProgress;
        task.TaskCompleted += HandleCompleted;

        IncrementActiveUploads();
    }

    private void IncrementActiveUploads()
    {
        lock (_uploadTitleLock)
        {
            _activeUploadCount++;
        }
    }

    private void DecrementActiveUploads()
    {
        bool resetTitle;
        lock (_uploadTitleLock)
        {
            _activeUploadCount = Math.Max(0, _activeUploadCount - 1);
            resetTitle = _activeUploadCount == 0;
        }

        if (resetTitle)
        {
            ResetMainWindowTitle();
        }
    }

    private void UpdateMainWindowTitle(double percentage)
    {
        if (_desktop?.MainWindow == null)
        {
            return;
        }

        if (double.IsNaN(percentage) || double.IsInfinity(percentage))
        {
            percentage = 0;
        }

        var clamped = Math.Clamp(percentage, 0, 100);
        var title = $"{_baseTitle} - Upload {clamped:0}%";

        Dispatcher.UIThread.Post(() =>
        {
            if (_desktop?.MainWindow != null)
            {
                _desktop.MainWindow.Title = title;
            }
        });
    }

    private void ResetMainWindowTitle()
    {
        if (_desktop?.MainWindow == null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_desktop?.MainWindow != null)
            {
                _desktop.MainWindow.Title = _baseTitle;
            }
        });
    }

    private static async Task OpenImageEditorAsync(Window? owner)
    {
        try
        {
            var topLevel = owner != null ? TopLevel.GetTopLevel(owner) : null;
            if (topLevel == null)
            {
                return;
            }

            var options = new FilePickerOpenOptions
            {
                Title = "Open Image in Editor",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp", "*.tiff", "*.tif" }
                    },
                    FilePickerFileTypes.All
                }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (files.Count < 1)
            {
                return;
            }

            var path = files[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var skBitmap = SkiaSharp.SKBitmap.Decode(fs);
            if (skBitmap == null)
            {
                return;
            }

            await PlatformServices.UI.ShowEditorAsync(skBitmap);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to open image in editor");
        }
    }
}
