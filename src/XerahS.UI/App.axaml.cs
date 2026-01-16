using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ShareX.Editor.ViewModels;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.Views;

namespace XerahS.UI;

public partial class App : Application
{
    public static bool IsExiting { get; set; } = false;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

#if DEBUG
        this.AttachDeveloperTools();
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

            // Wire up Editor clipboard to platform implementation
            ShareX.Editor.Services.EditorServices.Clipboard = new Services.EditorClipboardAdapter();

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

            desktop.Exit += (sender, args) =>
            {
                XerahS.Core.SettingsManager.SaveAllSettings();
            };

            // Subscribe to workflow completion for notification
            Core.Managers.TaskManager.Instance.TaskCompleted += OnWorkflowTaskCompleted;

            // Trigger async recording initialization via callback
            // This prevents blocking the main window from showing quickly
            PostUIInitializationCallback?.Invoke();
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

        bool isCaptureJob = settings.Job is Core.HotkeyType.PrintScreen
                                          or Core.HotkeyType.ActiveWindow
                                          or Core.HotkeyType.CustomWindow
                                          or Core.HotkeyType.RectangleRegion
                                          or Core.HotkeyType.CustomRegion
                                          or Core.HotkeyType.LastRegion;

        // For capture jobs, avoid bringing the main window forward until the capture completes.
        if (!isCaptureJob && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is MainWindow immediateMainWindow)
        {
            immediateMainWindow.NavigateToEditor();
        }

        // Subscribe once to task completion so we can update preview (and show the window for capture jobs).
        void HandleTaskCompleted(object? s, Core.Tasks.WorkerTask task)
        {
            Core.Managers.TaskManager.Instance.TaskCompleted -= HandleTaskCompleted;
            OnTaskCompleted(task, EventArgs.Empty);

            if (isCaptureJob && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow is MainWindow mainWindowAfterCapture)
            {
                mainWindowAfterCapture.NavigateToEditor();
            }
        }

        Core.Managers.TaskManager.Instance.TaskCompleted += HandleTaskCompleted;

        if (settings != null)
        {
            if (settings.Job == Core.HotkeyType.CustomWindow)
            {
                DebugHelper.WriteLine($"[DEBUG] Hotkey triggered for CustomWindow. Configured title: '{settings.TaskSettings?.CaptureSettings?.CaptureCustomWindow}'");
            }

            // Screen Recorder Toggle Logic (Unified Pipeline)
            // If we are recording and get a recording-related hotkey, we Signal the existing task to stop.
            // We do NOT start a new workflow.
            bool isRecordingHotkey = settings.Job == Core.HotkeyType.ScreenRecorder ||
                                     settings.Job == Core.HotkeyType.ScreenRecorderActiveWindow ||
                                     settings.Job == Core.HotkeyType.StopScreenRecording ||
                                     settings.Job == Core.HotkeyType.StartScreenRecorder;

            if (isRecordingHotkey && Core.Managers.ScreenRecordingManager.Instance.IsRecording)
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
}
