using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.Views;
using ShareX.Editor.ViewModels;

namespace XerahS.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = new MainViewModel();
            mainViewModel.ApplicationName = ShareXResources.AppName;

            desktop.MainWindow = new Views.MainWindow
            {
                DataContext = mainViewModel,
            };

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
                XerahS.Core.SettingManager.SaveAllSettings();
            };

            // Subscribe to workflow completion for notification
            Core.Managers.TaskManager.Instance.TaskCompleted += OnWorkflowTaskCompleted;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnWorkflowTaskCompleted(object? sender, Core.Tasks.WorkerTask task)
    {
        // Check if notification should be shown
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
                        LeftClickAction = MapToastClickAction(generalSettings.ToastWindowLeftClickAction),
                        RightClickAction = MapToastClickAction(generalSettings.ToastWindowRightClickAction),
                        MiddleClickAction = MapToastClickAction(generalSettings.ToastWindowMiddleClickAction),
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

    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        // The TrayIconHelper handles the action based on settings
        // This is triggered on left-click via the Command binding
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
            var hotkeys = Core.SettingManager.WorkflowsConfig.Hotkeys;

            // If configuration is empty/null, fallback to defaults
            if (hotkeys == null || hotkeys.Count == 0)
            {
                hotkeys = Core.Hotkeys.WorkflowManager.GetDefaultWorkflowList();
                // Update config with defaults so they get saved
                Core.SettingManager.WorkflowsConfig.Hotkeys = hotkeys;
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

            // Execute workflow with its ID for troubleshooting
            await Core.Helpers.TaskHelpers.ExecuteWorkflow(settings, settings.Id);
        }
    }

    /// <summary>
    /// Maps ToastClickAction from Core namespace to Platform.Abstractions namespace
    /// </summary>
    private static Platform.Abstractions.ToastClickAction MapToastClickAction(Core.ToastClickAction coreAction)
    {
        return coreAction switch
        {
            Core.ToastClickAction.CloseNotification => Platform.Abstractions.ToastClickAction.CloseNotification,
            Core.ToastClickAction.AnnotateImage => Platform.Abstractions.ToastClickAction.AnnotateImage,
            Core.ToastClickAction.CopyImageToClipboard => Platform.Abstractions.ToastClickAction.CopyImageToClipboard,
            Core.ToastClickAction.CopyFile => Platform.Abstractions.ToastClickAction.CopyFile,
            Core.ToastClickAction.CopyFilePath => Platform.Abstractions.ToastClickAction.CopyFilePath,
            Core.ToastClickAction.CopyUrl => Platform.Abstractions.ToastClickAction.CopyUrl,
            Core.ToastClickAction.OpenFile => Platform.Abstractions.ToastClickAction.OpenFile,
            Core.ToastClickAction.OpenFolder => Platform.Abstractions.ToastClickAction.OpenFolder,
            Core.ToastClickAction.OpenUrl => Platform.Abstractions.ToastClickAction.OpenUrl,
            Core.ToastClickAction.Upload => Platform.Abstractions.ToastClickAction.Upload,
            Core.ToastClickAction.PinToScreen => Platform.Abstractions.ToastClickAction.PinToScreen,
            Core.ToastClickAction.DeleteFile => Platform.Abstractions.ToastClickAction.DeleteFile,
            _ => Platform.Abstractions.ToastClickAction.CloseNotification
        };
    }
}
