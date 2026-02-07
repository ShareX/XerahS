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
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using XerahS.Editor.ViewModels;
using XerahS.Common;
using XerahS.Core;
using XerahS.Media.Encoders;
using XerahS.Platform.Abstractions;
using XerahS.UI.Services;
using XerahS.UI.Views;

namespace XerahS.UI;

public partial class App : Application
{
    public static bool IsExiting { get; set; } = false;
    private readonly object _uploadTitleLock = new();
    private int _activeUploadCount;
    private string _baseTitle = AppResources.ProductNameWithVersion;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Initialize theme based on user preference (System/Light/Dark)
        // This handles Linux properly where Avalonia's default detection doesn't work
        Services.ThemeService.Initialize();

#if DEBUG
        this.AttachDeveloperTools();

        // Load Audit Styles (Debug Only)
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://XerahS.UI/Themes/AuditStyles.axaml"))
        {
            Source = new Uri("avares://XerahS.UI/Themes/AuditStyles.axaml")
        });

        // Enable Runtime Wiring Checks
        Auditing.UiAudit.InitializeRuntimeChecks();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = new MainViewModel();
            mainViewModel.ApplicationName = AppResources.AppName;

            // Wire up UploadRequested for embedded editor in MainWindow
            Services.MainViewModelHelper.WireUploadRequested(mainViewModel);

            // Prepare for Silent Run
            bool silentRun = XerahS.Core.SettingsManager.Settings.SilentRun;

            if (silentRun)
            {
                // If starting silently, we don't want the last window closing to shut down the app
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            desktop.MainWindow = new Views.MainWindow
            {
                DataContext = mainViewModel,
            };
            _baseTitle = desktop.MainWindow.Title ?? AppResources.ProductNameWithVersion;

            // Apply window state based on SilentRun
            // Note: MainWindow is automatically shown by ApplicationLifetime after this method returns.
            // Setting it to minimized and hiding from taskbar is the best way to simulate "start hidden".
            if (silentRun)
            {
                desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Minimized;
                desktop.MainWindow.ShowInTaskbar = false;
            }
            else
            {
                desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Maximized;
            }

            InitializeHotkeys();

            // Register UI Service
            Platform.Abstractions.PlatformServices.RegisterUIService(new Services.AvaloniaUIService());

            // Register Toast Service
            Platform.Abstractions.PlatformServices.RegisterToastService(new Services.AvaloniaToastService());

            // Register Image Encoder Service (supports PNG, JPEG, BMP, GIF, WEBP, TIFF via Skia; AVIF via FFmpeg)
            PlatformServices.RegisterImageEncoderService(
                ImageEncoderService.CreateDefault(() => PathsManager.GetFFmpegPath()));

            // Wire up Editor clipboard to platform implementation
            XerahS.Editor.Services.EditorServices.Clipboard = new Services.EditorClipboardAdapter();

            // Setup window selector callback for CustomWindow hotkey
            Core.Tasks.WorkerTask.ShowWindowSelectorCallback = async () =>
            {
                var tcs = new TaskCompletionSource<Platform.Abstractions.WindowInfo?>();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        var viewModel = new ViewModels.WindowSelectorViewModel();
                        var dialog = new Window
                        {
                            Title = "Select Window to Capture",
                            Width = 400,
                            Height = 500,
                            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                            Content = new Views.WindowSelectorDialog { DataContext = viewModel }
                        };

                        viewModel.OnWindowSelected = (window) =>
                        {
                            tcs.TrySetResult(window);
                            dialog.Close();
                        };

                        viewModel.OnCancelled = () =>
                        {
                            tcs.TrySetResult(null);
                            dialog.Close();
                        };

                        if (desktop.MainWindow != null)
                        {
                            dialog.ShowDialog(desktop.MainWindow);
                        }
                        else
                        {
                            dialog.Show();
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.DebugHelper.WriteException(ex, "Failed to show window selector");
                        tcs.TrySetResult(null);
                    }
                });

                return await tcs.Task;
            };

            // Setup OpenFileDialog callback for FileUpload hotkey
            Core.Tasks.WorkerTask.ShowOpenFileDialogCallback = async () =>
            {
                var tcs = new TaskCompletionSource<string?>();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
                        if (topLevel == null)
                        {
                            tcs.TrySetResult(null);
                            return;
                        }

                        var options = new Avalonia.Platform.Storage.FilePickerOpenOptions
                        {
                            Title = "Select File to Upload",
                            AllowMultiple = false,
                            SuggestedStartLocation = await topLevel.StorageProvider.TryGetWellKnownFolderAsync(Avalonia.Platform.Storage.WellKnownFolder.Desktop)
                        };

                        var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);

                        if (files.Count >= 1)
                        {
                            // Get local path if possible
                            var path = files[0].TryGetLocalPath();
                            tcs.TrySetResult(path);
                        }
                        else
                        {
                            tcs.TrySetResult(null);
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.DebugHelper.WriteException(ex, "Failed to show open file dialog");
                        tcs.TrySetResult(null);
                    }
                });

                return await tcs.Task;
            };

            // Setup tool workflow callback for ColorPicker, QRCode, ScrollingCapture, OCR, etc.
            Core.Tasks.WorkerTask.HandleToolWorkflowCallback = async (workflowType) =>
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var owner = desktop.MainWindow;

                    if (workflowType == WorkflowType.OCR)
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
                    else
                    {
                        await QrCodeToolService.HandleWorkflowAsync(workflowType, owner);
                    }
                });
            };

            // Wire quick-win workflow callbacks
            Core.Tasks.WorkerTask.ExitApplicationCallback = () =>
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => desktop.Shutdown());
            };

            Core.Tasks.WorkerTask.ToggleHotkeysCallback = () =>
            {
                var config = Core.SettingsManager.Settings;
                if (config != null)
                {
                    config.DisableHotkeys = !config.DisableHotkeys;
                    Common.DebugHelper.WriteLine($"Hotkeys {(config.DisableHotkeys ? "disabled" : "enabled")}");
                }
            };

            desktop.Exit += (sender, args) =>
            {
                XerahS.Core.SettingsManager.SaveAllSettings();
                DebugHelper.Shutdown();
            };

            // Subscribe to workflow completion for notification
            Core.Managers.TaskManager.Instance.TaskCompleted += OnWorkflowTaskCompleted;
            Core.Managers.TaskManager.Instance.TaskStarted += OnWorkflowTaskStarted;

            // Trigger async recording initialization via callback
            // This prevents blocking the main window from showing quickly
            PostUIInitializationCallback?.Invoke();

            // Initialize auto-update service if enabled
            if (SettingsManager.Settings.AutoCheckUpdate)
            {
                Services.UpdateService.Instance.Initialize();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Callback invoked after UI initialization completes.
    /// Set by Program.cs to perform platform-specific async initialization.
    /// </summary>
    public static Action? PostUIInitializationCallback { get; set; }

    private void OnWorkflowTaskCompleted(object? sender, Core.Tasks.WorkerTask task)
    {
        // Check if notification should be shown
        // [2026-01-11] Fix: Only show toast if the task was actually successful (not cancelled/stopped) and produced a result.
        if (!task.IsSuccessful) return;

        var taskSettings = task.Info?.TaskSettings ?? new TaskSettings();
        if (taskSettings?.GeneralSettings?.ShowToastNotificationAfterTaskCompleted == true)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var generalSettings = taskSettings.GeneralSettings;
                    var filePath = task.Info?.FilePath;
                    var url = task.Info?.Result?.URL ?? task.Info?.Result?.ShortenedURL;

                    // Prepare toast title and text
                    string? title = null;
                    string? text = null;

                    if (task.Info?.Result?.IsError == true)
                    {
                        title = "Task Failed";
                        text = task.Info.Result.ToString();
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

                    // Determine image path for toast if file is an image
                    string? imagePath = null;
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath) && FileHelpers.IsImageFile(filePath))
                    {
                        imagePath = filePath;
                    }

                    // Build toast configuration from settings
                    var toastConfig = new ToastConfig
                    {
                        Title = title,
                        Text = text,
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

                    // Show toast using the toast service
                    if (PlatformServices.IsToastServiceInitialized)
                    {
                        PlatformServices.Toast.ShowToast(toastConfig);
                    }
                    else
                    {
                        // Fallback to native notification
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
    }

    private void OnWorkflowTaskStarted(object? sender, Core.Tasks.WorkerTask task)
    {
        if (!task.Info.IsUploadJob) return;

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
        UpdateMainWindowTitle(0);
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
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null) return;

        if (double.IsNaN(percentage) || double.IsInfinity(percentage))
        {
            percentage = 0;
        }

        double clamped = Math.Clamp(percentage, 0, 100);
        string title = $"{_baseTitle} - Upload {clamped:0}%";

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (desktop.MainWindow != null)
            {
                desktop.MainWindow.Title = title;
            }
        });
    }

    private void ResetMainWindowTitle()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (desktop.MainWindow != null)
            {
                desktop.MainWindow.Title = _baseTitle;
            }
        });
    }

    private Avalonia.Threading.DispatcherTimer? _trayClickTimer;
    private int _trayClickCount = 0;
    private const int DoubleClickDelayMs = 300;

    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        _trayClickCount++;

        if (_trayClickCount == 1)
        {
            // First click - start timer to wait for potential second click
            _trayClickTimer = new Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DoubleClickDelayMs)
            };
            _trayClickTimer.Tick += (s, args) =>
            {
                _trayClickTimer?.Stop();
                _trayClickTimer = null;

                if (_trayClickCount == 1)
                {
                    // Single click - execute TrayLeftClickAction
                    TrayIconHelper.Instance.OnTrayClick();
                }
                _trayClickCount = 0;
            };
            _trayClickTimer.Start();
        }
        else if (_trayClickCount >= 2)
        {
            // Double click detected
            _trayClickTimer?.Stop();
            _trayClickTimer = null;
            _trayClickCount = 0;

            // Execute double-click action
            TrayIconHelper.Instance.OnTrayDoubleClick();
        }
    }

    public Core.Hotkeys.WorkflowManager? WorkflowManager { get; private set; }

    private void InitializeHotkeys()
    {
        if (!Platform.Abstractions.PlatformServices.IsInitialized) return;

        try
        {
            var hotkeyService = Platform.Abstractions.PlatformServices.Hotkey;
            WorkflowManager = new Core.Hotkeys.WorkflowManager(hotkeyService);

            // Subscribe to hotkey triggers
            WorkflowManager.HotkeyTriggered += HotkeyManager_HotkeyTriggered;

            // Load hotkeys from configuration
            var hotkeys = Core.SettingsManager.WorkflowsConfig.Hotkeys;

            // If configuration is empty/null, fallback to defaults
            if (hotkeys == null || hotkeys.Count == 0)
            {
                hotkeys = Core.Hotkeys.WorkflowManager.GetDefaultWorkflowList();
                // Update config with defaults so they get saved
                Core.SettingsManager.WorkflowsConfig.Hotkeys = hotkeys;
            }

            WorkflowManager.UpdateHotkeys(hotkeys);

            DebugHelper.WriteLine($"Initialized hotkey manager with {hotkeys.Count} hotkeys from configuration");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to initialize hotkeys");
        }
    }

    private void OnTaskCompleted(object? sender, EventArgs e)
    {
        // When a task completes, update the preview image if it exists
        if (sender is Core.Tasks.WorkerTask task &&
            task.Info?.Metadata?.Image != null &&
            ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.DataContext is MainViewModel viewModel)
        {
            viewModel.UpdatePreview(task.Info.Metadata.Image);
            DebugHelper.WriteLine($"Updated preview from task completion: {task.Info.Metadata.Image.Width}x{task.Info.Metadata.Image.Height}");
        }
    }

    private async void HotkeyManager_HotkeyTriggered(object? sender, Core.Hotkeys.WorkflowSettings settings)
    {
        DebugHelper.WriteLine($"Hotkey triggered: {settings} (ID: {settings?.Id ?? "null"})");

        if (settings == null) return;


        // Determine request type by category
        string category = settings.Job.GetHotkeyCategory();
        bool isCaptureJob = category == EnumExtensions.WorkflowType_Category_ScreenCapture ||
                            category == EnumExtensions.WorkflowType_Category_ScreenRecord;

        // For capture jobs, avoid bringing the main window forward until the capture completes.
        // For non-capture jobs, only navigate if the window is already visible (not minimized to tray).
        if (!isCaptureJob && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is MainWindow immediateMainWindow)
        {
            // Only activate if the window is visible (not minimized or hidden)
            bool isWindowVisible = immediateMainWindow.IsVisible &&
                                   immediateMainWindow.WindowState != Avalonia.Controls.WindowState.Minimized &&
                                   immediateMainWindow.ShowInTaskbar &&
                                   !SettingsManager.Settings.SilentRun;
            
            if (isWindowVisible)
            {
                immediateMainWindow.NavigateToEditor();
            }
        }

        // Subscribe once to task completion so we can update preview (and show the window for capture jobs).
        void HandleTaskCompleted(object? s, Core.Tasks.WorkerTask task)
        {
            Core.Managers.TaskManager.Instance.TaskCompleted -= HandleTaskCompleted;
            OnTaskCompleted(task, EventArgs.Empty);

            // [2026-01-18] Fix: Do not open editor for screen recording jobs (video), only for image captures
            bool isScreenRecord = category == EnumExtensions.WorkflowType_Category_ScreenRecord;

            if (isCaptureJob && !isScreenRecord && task.IsSuccessful && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow is MainWindow mainWindowAfterCapture)
            {
                // Only navigate to editor if window is visible, not when minimized to tray
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

        if (settings != null)
        {
            if (settings.Job == Core.WorkflowType.CustomWindow)
            {
                DebugHelper.WriteLine($"[DEBUG] Hotkey triggered for CustomWindow. Configured title: '{settings.TaskSettings?.CaptureSettings?.CaptureCustomWindow}'");
            }

            // Screen Recorder Toggle Logic (Unified Pipeline)
            // If we are recording and get a recording-related hotkey, we Signal the existing task to stop.
            // We do NOT start a new workflow.
            bool isRecordingHotkey = settings.Job == Core.WorkflowType.ScreenRecorder ||
                                     settings.Job == Core.WorkflowType.ScreenRecorderActiveWindow ||
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
                // Normal workflow execution
                await Core.Helpers.TaskHelpers.ExecuteWorkflow(settings, settings.Id);
            }
        }
    }

    private void OnAboutClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is Views.MainWindow mainWindow)
        {
            mainWindow.NavigateToAbout();
        }
    }

    private void OnPreferencesClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is Views.MainWindow mainWindow)
        {
            mainWindow.NavigateToSettings();
        }
    }

    private static async Task OpenImageEditorAsync(Window? owner)
    {
        try
        {
            var topLevel = owner != null ? TopLevel.GetTopLevel(owner) : null;
            if (topLevel == null) return;

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
            if (files.Count < 1) return;

            var path = files[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var skBitmap = SkiaSharp.SKBitmap.Decode(fs);
            if (skBitmap == null) return;

            await PlatformServices.UI.ShowEditorAsync(skBitmap);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to open image in editor");
        }
    }

}
