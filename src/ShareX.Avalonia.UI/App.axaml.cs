using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using ShareX.Ava.Common;
using ShareX.Ava.UI.Views;
using ShareX.Ava.UI.ViewModels;
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
                DataContext = new ViewModels.MainViewModel(),
            };
            
            InitializeHotkeys();
            
            // Register UI Service
            Platform.Abstractions.PlatformServices.RegisterUIService(new Services.AvaloniaUIService());

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

    private async void HotkeyManager_HotkeyTriggered(object? sender, Core.Hotkeys.HotkeySettings settings)
    {
        DebugHelper.WriteLine($"Hotkey triggered: {settings}");
        
        // Execute the job associated with the hotkey
        await Core.Helpers.TaskHelpers.ExecuteJob(settings.Job, settings.TaskSettings);
    }
}
