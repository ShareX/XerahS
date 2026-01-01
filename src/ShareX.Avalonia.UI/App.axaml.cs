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

            // Load default hotkeys
            var defaultHotkeys = Core.Hotkeys.HotkeyManager.GetDefaultHotkeyList();
            HotkeyManager.UpdateHotkeys(defaultHotkeys);
            
            DebugHelper.WriteLine($"Initialized hotkey manager with {defaultHotkeys.Count} default hotkeys");
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
