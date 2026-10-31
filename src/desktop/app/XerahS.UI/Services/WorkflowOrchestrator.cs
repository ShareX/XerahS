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
using ShareX.ImageEditor.Presentation.ViewModels;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.Assistant;
using XerahS.UI.CaptureCommandPalette;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public sealed class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private readonly object _uploadTitleLock = new();
    private readonly IDesktopTaskManager _taskManager;
    private readonly IScreenRecordingCoordinator _screenRecordingCoordinator;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private Core.Hotkeys.WorkflowManager? _workflowManager;
    private AssistantOverlayCoordinator? _assistantOverlayCoordinator;
    private CaptureCommandPaletteCoordinator? _captureCommandPaletteCoordinator;
    private int _activeUploadCount;
    private string _baseTitle = AppResources.ProductNameWithVersion;

    public WorkflowOrchestrator(IDesktopTaskManager taskManager, IScreenRecordingCoordinator screenRecordingCoordinator)
    {
        _taskManager = taskManager;
        _screenRecordingCoordinator = screenRecordingCoordinator;
    }

    public Core.Hotkeys.WorkflowManager? WorkflowManager => _workflowManager;

    public void Start(IClassicDesktopStyleApplicationLifetime desktop, string baseTitle)
    {
        _desktop = desktop;
        _baseTitle = string.IsNullOrWhiteSpace(baseTitle) ? AppResources.ProductNameWithVersion : baseTitle;

        ConfigureWorkerTaskCallbacks();
        InitializeHotkeys();
        _assistantOverlayCoordinator ??= new AssistantOverlayCoordinator(_taskManager);
        _assistantOverlayCoordinator.Start();
        if (_workflowManager != null)
        {
            _captureCommandPaletteCoordinator ??= new CaptureCommandPaletteCoordinator(_workflowManager, ExecuteWorkflowFromPaletteAsync);
            _captureCommandPaletteCoordinator.Start();
        }

        _taskManager.TaskCompleted -= OnWorkflowTaskCompleted;
        _taskManager.TaskStarted -= OnWorkflowTaskStarted;
        _taskManager.TaskCompleted += OnWorkflowTaskCompleted;
        _taskManager.TaskStarted += OnWorkflowTaskStarted;
    }

    private void ConfigureWorkerTaskCallbacks()
    {
        Core.Tasks.WorkerTask.ShowWindowSelectorCallback = ShowWindowSelectorAsync;
        Core.Tasks.WorkerTask.ShowOpenFileDialogCallback = ShowOpenFileDialogAsync;
        Core.Tasks.WorkerTask.HandleToolWorkflowCallback = HandleToolWorkflowAsync;
        Core.Tasks.Processors.CaptureJobProcessor.PinToScreenCallback = async (bitmap, location, options) =>
        {
            // Pin windows outlive the worker completion callback; give them their own native pixels.
            var pinnedImage = bitmap.Copy();
            if (pinnedImage == null)
            {
                DebugHelper.WriteLine("PinToScreen skipped: failed to clone image for pinned window.");
                return;
            }
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PinToScreenManager.PinImage(pinnedImage, location == null ? null : (Avalonia.PixelPoint?)location, options);
                });
            }
            catch
            {
                pinnedImage.Dispose();
                throw;
            }
        };
        Core.Tasks.Processors.CaptureJobProcessor.ShowAnalyzerCallback = async bitmap =>
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var vm = new ImageAnalyzerViewModel();
                vm.SetInputImage(bitmap);

                var w = new ImageAnalyzerWindow();
                w.Initialize(vm);
                w.Show();
            });
        };

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
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var viewModel = new WindowSelectorViewModel();
                XerahS.Platform.Abstractions.WindowInfo? selected = null;

                await ModalDialogHost.ShowAsync(
                    viewModel,
                    set =>
                    {
                        viewModel.OnWindowSelected = window =>
                        {
                            selected = window;
                            set(true);
                        };
                        viewModel.OnCancelled = () => set(false);
                    },
                    dismissResult: false,
                    debugSource: nameof(WindowSelectorViewModel));

                return selected;
            });
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to show window selector");
            return null;
        }
    }

    private async Task<string?> ShowOpenFileDialogAsync()
    {
        if (OperatingSystem.IsMacOS())
        {
            return await MacOSUploadFilePicker.PickFileAsync().ConfigureAwait(false);
        }

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

    private async Task HandleToolWorkflowAsync(WorkflowType workflowType, TaskSettings taskSettings)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = _desktop?.MainWindow;

            if (ToolWorkflowDispatcher.TryDispatch(workflowType, owner, taskSettings, _taskManager, out var dispatchTask))
            {
                await dispatchTask;
                return;
            }

            DebugHelper.WriteLine($"Unhandled tool workflow callback: {workflowType}");
        });
    }

    private void OpenMainWindow()
    {
        if (_desktop?.MainWindow is not MainWindow mainWindow)
        {
            return;
        }

        mainWindow.ShowInTaskbar = !OperatingSystem.IsMacOS() || !SettingsManager.Settings.SilentRun;

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
            _workflowManager.WorkflowsChanged += (_, _) => RefreshHyprlandKeybindings();

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

    /// <summary>Keeps ~/.config/hypr/xerahs.lua in sync with hotkeys when Hyprland keybindings are on (XIP0088).</summary>
    private void RefreshHyprlandKeybindings()
    {
        var workflows = _workflowManager?.Workflows.ToList();
        if (workflows == null || SettingsManager.Settings?.LinuxHyprlandKeybindings != true)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await new Core.Hotkeys.HyprlandKeybindingCoordinator().RefreshAsync(workflows).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Hyprland keybindings: refresh failed ({ex.Message}).");
            }
        });
    }

    private void OnTaskCompleted(object? sender, EventArgs e)
    {
        if (sender is not Core.Tasks.WorkerTask task ||
            task.Info?.Metadata?.Image is not { } image ||
            _desktop?.MainWindow?.DataContext is not MainViewModel viewModel)
        {
            return;
        }

        int width = image.Width;
        int height = image.Height;
        SkiaSharp.SKBitmap? previewCopy = image.Copy();
        if (previewCopy == null || previewCopy.Handle == IntPtr.Zero)
        {
            previewCopy?.Dispose();
            DebugHelper.WriteLine("Skipped preview update from task completion: failed to clone bitmap.");
            return;
        }

        // UpdatePreview takes ownership and can dispose the supplied bitmap during property-change handling.
        viewModel.UpdatePreview(previewCopy);
        DebugHelper.WriteLine($"Updated preview from task completion: {width}x{height}");
    }

    private async void HotkeyManager_HotkeyTriggered(object? sender, Core.Hotkeys.WorkflowSettings settings)
    {
        DebugHelper.WriteLine($"Hotkey triggered: {settings} (ID: {settings?.Id ?? "null"})");

        if (settings == null)
        {
            return;
        }

        await ExecuteWorkflowFromTriggerAsync(settings);
    }

    public Task RunWorkflowAsync(Core.Hotkeys.WorkflowSettings workflow)
    {
        DebugHelper.WriteLine($"Workflow run requested by an automation client: {workflow} (ID: {workflow.Id})");
        return ExecuteWorkflowFromTriggerAsync(workflow);
    }

    private async Task ExecuteWorkflowFromPaletteAsync(Core.Hotkeys.WorkflowSettings settings)
    {
        DebugHelper.WriteLine($"Capture command palette selected: {settings} (ID: {settings?.Id ?? "null"})");

        if (settings == null)
        {
            return;
        }

        await ExecuteWorkflowFromTriggerAsync(settings);
    }

    private async Task ExecuteWorkflowFromTriggerAsync(Core.Hotkeys.WorkflowSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        string category = settings.Job.GetHotkeyCategory();
        bool isCaptureJob = category == EnumExtensions.WorkflowType_Category_ScreenCapture ||
                            category == EnumExtensions.WorkflowType_Category_ScreenRecord;

        // Hotkey/palette-triggered captures deliberately do NOT hide the main window.
        // The caller pressed a hotkey because they wanted to grab what was on screen,
        // which frequently includes the XerahS window itself. Navbar/toolbar clicks
        // still hide via TaskHelpers.ExecuteJob(hideMainWindow: true); tray left/double/
        // middle click still hides via TrayIconHelper -> ExecuteWorkflow(hideMainWindow: true).
        // SilentRun callers already have the window hidden, and HideMainWindowAsync
        // no-ops on a hidden window.
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
            _taskManager.TaskCompleted -= HandleTaskCompleted;
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

        _taskManager.TaskCompleted += HandleTaskCompleted;

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
            (_screenRecordingCoordinator.IsRecording || _screenRecordingCoordinator.IsPaused))
        {
            if (!_screenRecordingCoordinator.CurrentCapabilities.SupportsPauseResume)
            {
                DebugHelper.WriteLine("Pause/Resume hotkey ignored because the active recording backend does not support pause/resume safely.");
                return;
            }

            DebugHelper.WriteLine("Pause/Resume hotkey triggered - toggling recording pause state...");
            await _screenRecordingCoordinator.TogglePauseResumeAsync();
            return;
        }

        if (settings.Job == Core.WorkflowType.AbortScreenRecording &&
            (_screenRecordingCoordinator.IsRecording || _screenRecordingCoordinator.IsPaused))
        {
            DebugHelper.WriteLine("Abort hotkey triggered - aborting recording...");
            await _screenRecordingCoordinator.AbortRecordingAsync();
            return;
        }

        if (isRecordingHotkey && (_screenRecordingCoordinator.IsRecording || _screenRecordingCoordinator.IsPaused))
        {
            DebugHelper.WriteLine("Screen Recording active - flagging Stop Signal to existing task...");
            _screenRecordingCoordinator.SignalStop();
            return;
        }

        // Scrolling capture: same hotkey stops an in-progress capture
        if (settings.Job == Core.WorkflowType.ScrollingCapture &&
            ScrollingCaptureToolService.CurrentCapture?.IsCapturing == true)
        {
            DebugHelper.WriteLine("Scrolling Capture active - stopping capture.");
            ScrollingCaptureToolService.StopCurrentCapture();
            return;
        }

        await Core.Helpers.TaskHelpers.ExecuteWorkflow(settings, settings.Id);
    }

    private void OnWorkflowTaskCompleted(object? sender, Core.Tasks.WorkerTask task)
    {
        if (!task.IsSuccessful)
        {
            return;
        }

        var taskSettings = task.Info?.TaskSettings ?? new TaskSettings();
        if (!ShouldShowCompletionNotification(task.Info))
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

                var afterCapture = taskSettings.AfterCaptureJob;
                var outputKind = ToastOutputClassifier.Resolve(
                    isError: task.Info?.Result?.IsError == true,
                    url: url,
                    fileExists: !string.IsNullOrEmpty(filePath) && File.Exists(filePath),
                    uploadAttempted: afterCapture.HasFlag(AfterCaptureTasks.UploadImageToHost),
                    copiedToClipboard: afterCapture.HasFlag(AfterCaptureTasks.CopyImageToClipboard));

                var toastConfig = new ToastConfig
                {
                    OutputKind = outputKind,
                    Title = title,
                    Text = text,
                    ErrorDetails = errorDetails,
                    ImagePath = imagePath,
                    FilePath = filePath,
                    URL = url,
                    HistoryItemId = task.Info?.HistoryItemId,
                    Duration = generalSettings.ToastWindowDuration,
                    FadeDuration = generalSettings.ToastWindowFadeDuration,
                    Placement = generalSettings.ToastWindowPlacement,
                    Size = generalSettings.ToastWindowSize,
                    LeftClickAction = generalSettings.ToastWindowLeftClickAction,
                    RightClickAction = generalSettings.ToastWindowRightClickAction,
                    MiddleClickAction = generalSettings.ToastWindowMiddleClickAction,
                    ActionButtons = generalSettings.ToastWindowButtons?.ToList() ?? [],
                    ActionButtonSize = generalSettings.ToastWindowButtonSize,
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

    internal static bool ShouldShowCompletionNotification(TaskInfo? info) =>
        info?.SuppressCompletionNotification != true &&
        info?.TaskSettings?.GeneralSettings?.ShowToastNotificationAfterTaskCompleted == true;

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

}
