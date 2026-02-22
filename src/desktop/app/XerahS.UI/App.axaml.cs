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
using ShareX.ImageEditor.ViewModels;
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
    private readonly IWorkflowOrchestrator _workflowOrchestrator = new WorkflowOrchestrator();
    private readonly ITrayIconController _trayIconController = new TrayIconController();
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

            // Wire up CopyRequested for embedded editor in MainWindow
            Services.MainViewModelHelper.WireCopyRequested(mainViewModel);

            // Prepare for Silent Run
            bool silentRun = XerahS.Core.SettingsManager.Settings.SilentRun;
#if DEBUG
            // In DEBUG builds always show main window at startup for easier development.
            silentRun = false;
#endif

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

            // Apply window state based on SilentRun.
            // We avoid starting minimized because some Windows setups can leave a minimized
            // thumbnail/button at the bottom-left instead of staying tray-only.
            if (silentRun)
            {
                desktop.MainWindow.ShowInTaskbar = false;

                EventHandler? hideOnFirstOpen = null;
                hideOnFirstOpen = (_, _) =>
                {
                    if (desktop.MainWindow != null)
                    {
                        desktop.MainWindow.Opened -= hideOnFirstOpen;
                    }

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (desktop.MainWindow != null && XerahS.Core.SettingsManager.Settings.SilentRun && !IsExiting)
                        {
                            desktop.MainWindow.Hide();
                            desktop.MainWindow.ShowInTaskbar = false;
                            Common.DebugHelper.WriteLine("SilentRun startup: main window hidden to tray.");
                        }
                    }, Avalonia.Threading.DispatcherPriority.Background);
                };

                desktop.MainWindow.Opened += hideOnFirstOpen;
            }
            else
            {
                desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Maximized;
            }

            // Register UI Service
            Platform.Abstractions.PlatformServices.RegisterUIService(new Services.AvaloniaUIService());

            // Register Toast Service
            Platform.Abstractions.PlatformServices.RegisterToastService(new Services.AvaloniaToastService());

            // Register Image Encoder Service (supports PNG, JPEG, BMP, GIF, WEBP, TIFF via Skia; AVIF via FFmpeg)
            PlatformServices.RegisterImageEncoderService(
                ImageEncoderService.CreateDefault(() => PathsManager.GetFFmpegPath()));

            // Wire up Editor clipboard to platform implementation
            ShareX.ImageEditor.Services.EditorServices.Clipboard = new Services.EditorClipboardAdapter();

            // Build DI container from platform and app services (single composition root)
            Services.CompositionRoot.BuildAndSetRootProvider();

            _workflowOrchestrator.Start(desktop, _baseTitle);
            _trayIconController.Initialize();

            desktop.Exit += (sender, args) =>
            {
                XerahS.Core.SettingsManager.SaveAllSettings();
                DebugHelper.Shutdown();
            };

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
    public Core.Hotkeys.WorkflowManager? WorkflowManager => _workflowOrchestrator.WorkflowManager;

    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        _trayIconController.HandleClicked();
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

}

