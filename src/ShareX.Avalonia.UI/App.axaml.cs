using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using ShareX.Avalonia.Common;
using ShareX.Avalonia.UI.Views;
using ShareX.Avalonia.UI.ViewModels;
using ShareX.Avalonia.Uploaders.PluginSystem;
using ShareX.Avalonia.Uploaders.Plugins.ImgurPlugin;
using ShareX.Avalonia.Uploaders.Plugins.AmazonS3Plugin;

namespace ShareX.Avalonia.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Register built-in providers at startup
        RegisterProviders();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.MainWindow
            {
                DataContext = new ViewModels.MainViewModel(),
            };
            
            InitializeHotkeys();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public Core.Hotkeys.HotkeyManager? HotkeyManager { get; private set; }

    private void RegisterProviders()
    {
        // Register Imgur provider
        var imgurProvider = new ImgurProvider();
        ProviderCatalog.RegisterProvider(imgurProvider);

        // Register Amazon S3 provider
        var s3Provider = new AmazonS3Provider();
        ProviderCatalog.RegisterProvider(s3Provider);

        DebugHelper.WriteLine("Registered built-in providers: Imgur, Amazon S3");
    }

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
