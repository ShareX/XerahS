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

using CommunityToolkit.Maui;
using ShareX.AmazonS3.Plugin;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Uploaders;
using XerahS.Mobile.Maui.ViewModels;
using XerahS.Mobile.Maui.Views;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Mobile;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Mobile.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // === Platform initialization ===
        // Android: ALL init deferred to MainActivity.OnCreate (needs Activity context for FilesDir, clipboard)
        // iOS: Init here since no Activity context is needed
#if IOS
        MobilePlatform.Initialize(PlatformType.iOS);
        PlatformServices.Clipboard = new iOSClipboardService();
        PathsManager.PersonalFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        SettingsManager.LoadInitialSettings();
        ProviderContextManager.EnsureProviderContext();
        ProviderCatalog.InitializeBuiltInProviders(typeof(AmazonS3Provider).Assembly);
#endif

        // Register DI services
        RegisterServices(builder.Services);

        return builder.Build();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // ViewModels
        services.AddTransient<MobileUploadViewModel>();
        services.AddTransient<MobileSettingsViewModel>();
        services.AddTransient<MobileHistoryViewModel>();
        services.AddTransient<MobileAmazonS3ConfigViewModel>();
        services.AddTransient<MobileCustomUploaderConfigViewModel>();

        // Pages
        services.AddTransient<MobileUploadPage>();
        services.AddTransient<MobileSettingsPage>();
        services.AddTransient<MobileHistoryPage>();
        services.AddTransient<MobileAmazonS3ConfigPage>();
        services.AddTransient<MobileCustomUploaderConfigPage>();
    }
}
