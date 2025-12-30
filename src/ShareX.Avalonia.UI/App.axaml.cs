using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using ShareX.Avalonia.UI.Views;
using ShareX.Avalonia.UI.ViewModels;

namespace ShareX.Avalonia.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        // Initialize Platform Services
        InitializePlatformServices();
    }

    private void InitializePlatformServices()
    {
        var platformInfo = new Services.PlatformInfoService();
        var screenService = new Services.ScreenService();
        var clipboardService = new Services.ClipboardService();
        var windowService = new Services.WindowService();
        var screenCaptureService = new Services.ScreenCaptureService();

        ShareX.Avalonia.Platform.Abstractions.PlatformServices.Initialize(
            platformInfo,
            screenService,
            clipboardService,
            windowService,
            screenCaptureService
        );
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.MainWindow
            {
                DataContext = new ViewModels.MainViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
