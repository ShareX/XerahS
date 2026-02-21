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

using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Ava.ViewModels;
using Ava.Views;

namespace Ava;

public partial class MobileApp : Avalonia.Application
{
    private const string PlatformTagKey = "PlatformTag";
    private static readonly Uri AppUri = new("avares://XerahS.Mobile.Ava/");

    /// <summary>
    /// Callback for platform heads to push shared file paths into the Avalonia UI.
    /// Set after the framework initialization completes.
    /// </summary>
    public static Action<string[]>? OnFilesReceived { get; set; }

    /// <summary>
    /// Providers registered directly by platform heads (no reflection scanning).
    /// Call RegisterBundledProvider() from MainActivity before the app initializes.
    /// </summary>
    private static readonly List<XerahS.Uploaders.PluginSystem.IUploaderProvider> _registeredProviders = new();

    public static void RegisterBundledProvider(XerahS.Uploaders.PluginSystem.IUploaderProvider provider)
    {
        if (provider != null) _registeredProviders.Add(provider);
    }

    private MobileUploadViewModel? _uploadViewModel;
    private ISingleViewApplicationLifetime? _singleView;
    private string _platformTag = "desktop";

    // A single, persistent ContentControl that acts as the app's navigation host.
    // We set ISingleViewApplicationLifetime.MainView to this ONCE and never touch MainView again.
    // Navigation is done by swapping _navigationRoot.Content, which reliably triggers re-render.
    private readonly TransitioningContentControl _navigationRoot = new()
    {
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        PageTransition = new CrossFade(TimeSpan.FromMilliseconds(200))
    };

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        LoadThemeStyles();
    }

    private void LoadThemeStyles()
    {
        LoadStyle("Themes/AdaptiveControls.axaml");

        string? platformThemePath = null;

        if (OperatingSystem.IsIOS())
        {
            platformThemePath = "Themes/iOS.axaml";
            _platformTag = "ios";
        }
        else if (OperatingSystem.IsAndroid())
        {
            platformThemePath = "Themes/Android.axaml";
            _platformTag = "android";
        }

        Resources[PlatformTagKey] = _platformTag;

        if (!string.IsNullOrEmpty(platformThemePath))
        {
            LoadStyle(platformThemePath);
        }
    }

    private void LoadStyle(string relativePath)
    {
        var uri = new Uri($"avares://XerahS.Mobile.Ava/{relativePath}");

        if (Styles.OfType<StyleInclude>().Any(x => x.Source == uri))
        {
            return;
        }

        Styles.Add(new StyleInclude(AppUri)
        {
            Source = uri
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            _singleView = singleView;
            // Set the navigation root once and never swap MainView again.
            _singleView.MainView = _navigationRoot;
            
            // Start initialization async
            _ = InitializeCoreAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitializeCoreAsync()
    {
#if __ANDROID__
        global::Android.Util.Log.Info("XerahS", "[Init] Showing loading view.");
#endif
        Navigate(new MobileLoadingView());

        // Allow the loading view to render one frame before blocking a thread on init.
        await Task.Delay(100);

        try
        {
#if __ANDROID__
            global::Android.Util.Log.Info("XerahS", "[Init] Starting background init (PathsManager, Settings, Providers).");
#endif
            await Task.Run(() =>
            {
#if __ANDROID__
                global::Android.Util.Log.Debug("XerahS", "[Init] EnsureDirectoriesExist...");
#endif
                XerahS.Common.PathsManager.EnsureDirectoriesExist();
#if __ANDROID__
                global::Android.Util.Log.Debug("XerahS", "[Init] LoadInitialSettings...");
#endif
                XerahS.Core.SettingsManager.LoadInitialSettings();

                // Directly register bundled providers - NO reflection scanning.
                foreach (var provider in _registeredProviders)
                {
                    XerahS.Uploaders.PluginSystem.ProviderCatalog.RegisterProvider(provider);
                }
#if __ANDROID__
                global::Android.Util.Log.Debug("XerahS", "[Init] EnsureProviderContext...");
#endif
                XerahS.Core.Uploaders.ProviderContextManager.EnsureProviderContext();

#if __ANDROID__
                global::Android.Util.Log.Info("XerahS", "[Init] Background init completed.");
#endif
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Init] Error: {ex}");
#if __ANDROID__
            global::Android.Util.Log.Error("XerahS", "[Init] Exception: " + ex.ToString());
#endif
        }

#if __ANDROID__
        global::Android.Util.Log.Info("XerahS", "[Init] Posting ShowUploadView to UI thread.");
#endif
        // After initialization, navigate to the main view
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
#if __ANDROID__
                global::Android.Util.Log.Info("XerahS", "[Init] ShowUploadView starting.");
#endif
                ShowUploadView();
#if __ANDROID__
                global::Android.Util.Log.Info("XerahS", "[Init] ShowUploadView done.");
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Init] ShowUploadView error: {ex}");
#if __ANDROID__
                global::Android.Util.Log.Error("XerahS", "[Init] ShowUploadView crash: " + ex.ToString());
#endif
            }
        });
    }

    /// <summary>Navigates to a view by swapping the navigation root's Content.</summary>
    private void Navigate(Avalonia.Controls.Control view)
    {
        Debug.WriteLine($"[MobileApp] Navigate to {view.GetType().Name}");
        _navigationRoot.Content = view;
        Debug.WriteLine($"[MobileApp] Navigation complete → {view.GetType().Name}");
    }

    private void ShowUploadView()
    {
        _uploadViewModel = new MobileUploadViewModel();
        var uploadView = new MobileUploadView
        {
            DataContext = _uploadViewModel,
            Tag = _platformTag
        };

        // Set up platform callbacks
        OnFilesReceived = (paths) =>
        {
            Dispatcher.UIThread.Post(() => _uploadViewModel.ProcessFiles(paths));
        };

        MobileUploadViewModel.OnOpenSettings = () =>
        {
            Debug.WriteLine("[MobileApp] OnOpenSettings invoked");
            ShowSettingsView();
        };
        MobileUploadViewModel.OnOpenHistory = () =>
        {
            Debug.WriteLine("[MobileApp] OnOpenHistory invoked");
            ShowHistoryView();
        };

        Navigate(uploadView);
    }

    private void ShowSettingsView()
    {
        Debug.WriteLine("[MobileApp] ShowSettingsView called");

        var settingsView = new MobileSettingsView
        {
            Tag = _platformTag
        };

        // Hook into the back/close request from the settings view model
        if (settingsView.DataContext is MobileSettingsViewModel vm)
        {
            vm.RequestCloseSettings += ShowUploadView;
        }

        Navigate(settingsView);
        Debug.WriteLine("[MobileApp] ShowSettingsView complete");
    }

    private void ShowHistoryView()
    {
        Debug.WriteLine("[MobileApp] ShowHistoryView called");

        var historyViewModel = new MobileHistoryViewModel();
        var historyView = new MobileHistoryView
        {
            DataContext = historyViewModel,
            Tag = _platformTag
        };

        MobileHistoryViewModel.OnCloseRequested = ShowUploadView;
        Navigate(historyView);
        Debug.WriteLine("[MobileApp] ShowHistoryView complete");
    }
}
