using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using ShareX.Ava.Common;
using ShareX.Ava.UI.Views;
using ShareX.Editor.ViewModels;
using ShareX.Ava.Uploaders.PluginSystem;

namespace ShareX.Ava.UI;

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
            desktop.MainWindow = new Views.MainWindow
            {
                DataContext = new MainViewModel(),
            };
            
            InitializeHotkeys();
            
            // Register UI Service
            Platform.Abstractions.PlatformServices.RegisterUIService(new Services.AvaloniaUIService());
            
            // Wire up Editor clipboard to platform implementation
            ShareX.Editor.Services.EditorServices.Clipboard = new Services.EditorClipboardAdapter();

            // Save settings on exit
            desktop.Exit += (sender, args) =>
            {
                ShareX.Ava.Core.SettingManager.SaveAllSettings();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public Core.Hotkeys.HotkeyManager? HotkeyManager { get; private set; }

    private void InitializeHotkeys()
    {
        if (!Platform.Abstractions.PlatformServices.IsInitialized) return;

        try
        {
            var hotkeyService = Platform.Abstractions.PlatformServices.Hotkey;
            HotkeyManager = new Core.Hotkeys.HotkeyManager(hotkeyService);
            
            // Subscribe to hotkey triggers
            HotkeyManager.HotkeyTriggered += HotkeyManager_HotkeyTriggered;

            // Load hotkeys from configuration
            var hotkeys = Core.SettingManager.HotkeysConfig.Hotkeys;
            
            // If configuration is empty/null, fallback to defaults
            if (hotkeys == null || hotkeys.Count == 0)
            {
                hotkeys = Core.Hotkeys.HotkeyManager.GetDefaultHotkeyList();
                // Update config with defaults so they get saved
                Core.SettingManager.HotkeysConfig.Hotkeys = hotkeys;
            }

            HotkeyManager.UpdateHotkeys(hotkeys);
            
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

    private async void HotkeyManager_HotkeyTriggered(object? sender, Core.Hotkeys.HotkeySettings settings)
    {
        DebugHelper.WriteLine($"Hotkey triggered: {settings}");
        
        bool isCaptureJob = settings.Job is Core.HotkeyType.PrintScreen
                                          or Core.HotkeyType.ActiveWindow
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
        
        // Execute the job associated with the hotkey
        await Core.Helpers.TaskHelpers.ExecuteJob(settings.Job, settings.TaskSettings);
    }
}
