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
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Hosting.Diagnostics;
using ShareX.ImageEditor.Presentation.ViewModels;
using ShareX.ImageEditor.Presentation.Views;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Cloud;
using XerahS.Media.Encoders;
using XerahS.Platform.Abstractions;
#if WINDOWS
using XerahS.Platform.Windows;
#endif
using SkiaSharp;
using XerahS.UI.Services;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.UI;

public partial class App : Application
{
    public static bool IsExiting { get; set; } = false;
    public IServiceProvider? ServiceProvider { get; private set; }
    private static readonly TimeSpan ClipboardViewerAutoOpenCooldown = TimeSpan.FromSeconds(2);
    private IWorkflowOrchestrator? _workflowOrchestrator;
    private ITrayIconController? _trayIconController;
    private RecordingEnterStopHotkey? _recordingEnterStopHotkey;
    private string _baseTitle = AppResources.ProductNameWithVersion;
    private EventHandler? _clipboardChangedHandler;
    private DateTime _lastClipboardViewerAutoOpenUtc = DateTime.MinValue;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Set the Wayland xdg_toplevel app_id to match the installed xerahs.desktop filename.
        // Without this, Avalonia defaults to the process name ("XerahS" with capital X), which
        // does not match "xerahs.desktop", so xdg-desktop-portal cannot identify the app and
        // GNOME's GlobalShortcuts portal backend returns response=2 (Failed) immediately,
        // forcing an X11 fallback that does not work under XWayland.
        Name = "xerahs";

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
        // Suppress benign Avalonia desktop-integration exceptions on Linux (DBus tray icon,
        // IME, portal glitches) so they cannot take down the whole app. Avalonia's dispatcher
        // only swallows an exception when the filter stage requests a catch AND an
        // UnhandledException handler marks it as handled, so both stages are wired here.
        // Without this, a sandboxed Flatpak install crashes ~1 second after startup on KDE:
        // Avalonia's DBusTrayIconImpl requests the org.kde.StatusNotifierItem-{pid}-{tid} bus
        // name, the Flatpak session-bus proxy denies it, and the resulting
        // Tmds.DBus.Protocol.DBusErrorReplyException escapes on the UI thread (issue #270).
        Avalonia.Threading.Dispatcher.UIThread.UnhandledExceptionFilter += (sender, e) =>
        {
            if (IsNonFatalDispatcherException(e.Exception))
            {
                e.RequestCatch = true;
            }
        };

        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (sender, e) =>
        {
            if (IsNonFatalDispatcherException(e.Exception))
            {
                e.Handled = true;
                try
                {
                    DebugHelper.WriteException(e.Exception, "Suppressed non-fatal UI thread exception (desktop integration)");
                }
                catch
                {
                    // Never throw from the dispatcher exception handler.
                }
            }
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var uiService = new Services.AvaloniaUIService();

            // Register UI Service
            Platform.Abstractions.PlatformServices.RegisterUIService(uiService);

            // Register Toast Service
            var toastService = new Services.AvaloniaToastService();
            Platform.Abstractions.PlatformServices.RegisterToastService(toastService);

            // Register Image Encoder Service (supports PNG, JPEG, BMP, GIF, WEBP, TIFF via Skia; AVIF via FFmpeg)
            var imageEncoderService = ImageEncoderService.CreateDefault(() => PathsManager.GetFFmpegPath());
            PlatformServices.RegisterImageEncoderService(imageEncoderService);

            // Build DI container from platform and app services (single composition root)
            ServiceProvider = Services.CompositionRoot.BuildServiceProvider(uiService, toastService, imageEncoderService);

            if (ApplicationLifetime is IActivatableLifetime activatableLifetime)
            {
                activatableLifetime.Activated += OnApplicationActivated;
            }

            var taskManager = ServiceProvider.GetRequiredService<IDesktopTaskManager>();
            var screenRecordingCoordinator = ServiceProvider.GetRequiredService<IScreenRecordingCoordinator>();
            var uiViewModelFactory = ServiceProvider.GetRequiredService<IUiViewModelFactory>();
            Services.UiViewModelFactoryAccessor.Configure(uiViewModelFactory);
            toastService.Configure(taskManager);

            uiService.Configure(taskManager);

            // Register host-level editor services before creating any editor view models.
            EditorServices.Diagnostics = new DelegateEditorDiagnosticsSink(diagnosticEvent =>
            {
                string prefix = $"[ImageEditor:{diagnosticEvent.Level}:{diagnosticEvent.Source}] {diagnosticEvent.Message}";
                Common.DebugHelper.WriteLine(prefix);

                if (!string.IsNullOrWhiteSpace(diagnosticEvent.ExceptionText))
                {
                    Common.DebugHelper.WriteLine(diagnosticEvent.ExceptionText!);
                }
            });
            EditorServices.EnsureDefaultDesktopWallpaperService();

            var mainViewModel = new MainViewModel(Services.ThemeService.CreateImageEditorOptions());
            mainViewModel.ApplicationName = AppResources.AppName;
            mainViewModel.ShowTaskButtons = false;
            mainViewModel.ShowStartScreen = false;

            // Pre-load default image so annotation toolbar is usable before first capture
            // Load asynchronously to avoid blocking the UI thread during startup
            Task.Run(async () =>
            {
                try
                {
                    var sampleUri = new Uri("avares://ShareX.ImageEditor/Assets/Sample.png");
                    using var sampleStream = Avalonia.Platform.AssetLoader.Open(sampleUri);
                    if (sampleStream == null)
                    {
                        DebugHelper.WriteLine($"Sample.png stream is null - asset not found at {sampleUri}");
                        return;
                    }
                    using var ms = new MemoryStream();
                    sampleStream.CopyTo(ms);
                    ms.Position = 0;
                    SKBitmap? sampleBitmap = SKBitmap.Decode(ms);
                    if (sampleBitmap == null)
                    {
                        DebugHelper.WriteLine($"SKBitmap.Decode returned null for Sample.png");
                        return;
                    }

                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        mainViewModel.UpdatePreview(sampleBitmap, clearAnnotations: true);
                        sampleBitmap = null;
                    });

                    sampleBitmap?.Dispose();
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"Failed to pre-load default editor image (Sample.png): {ex}");
                }
            });

            // Wire up CopyRequested for embedded editor in MainWindow (use edited snapshot when on Editor tab)
            Services.MainViewModelHelper.WireCopyRequested(mainViewModel, () =>
            {
                if (desktop.MainWindow is Views.MainWindow mw)
                {
                    var contentFrame = mw.FindControl<ContentControl>("ContentFrame");
                    if (contentFrame?.Content is EditorView ev)
                        return ev.GetSnapshot();
                }
                return null;
            });

            // Use the flattened editor image for upload, save and pin actions.
            Func<SkiaSharp.SKBitmap?> getEmbeddedSnapshot = () =>
            {
                if (desktop.MainWindow is Views.MainWindow mw)
                {
                    var contentFrame = mw.FindControl<ContentControl>("ContentFrame");
                    if (contentFrame?.Content is EditorView ev)
                        return ev.GetSnapshot();
                }
                return null;
            };
            Services.MainViewModelHelper.WireUploadRequested(mainViewModel, taskManager, getEmbeddedSnapshot);
            Services.MainViewModelHelper.WireSaveRequested(mainViewModel, getEmbeddedSnapshot, () => desktop.MainWindow);
            Services.MainViewModelHelper.WireSaveAsRequested(mainViewModel, getEmbeddedSnapshot, () => desktop.MainWindow);
            Services.MainViewModelHelper.WirePinRequested(mainViewModel, getEmbeddedSnapshot);

            // Prepare for Silent Run ("Start minimized to tray"). Honor the setting in
            // Debug and Release — Debug used to force the main window visible, which made
            // the Application Settings checkbox look broken when running from the IDE.
            bool silentRun = Helpers.SilentRunStartupPolicy.ShouldHideMainWindowToTray(
                XerahS.Core.SettingsManager.Settings.SilentRun, IsExiting, alreadyApplied: false);

            if (silentRun)
            {
                ApplyMenuBarOnlyModeFromSettings();

                // If starting silently, we don't want the last window closing to shut down the app
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            desktop.MainWindow = new Views.MainWindow(taskManager)
            {
                DataContext = mainViewModel,
            };
            _baseTitle = desktop.MainWindow.Title ?? AppResources.ProductNameWithVersion;

            XerahS.Platform.Abstractions.IClipboardService runtimeClipboardService;

            // Use native Win32 clipboard on Windows so image formats are published explicitly.
#if WINDOWS
            runtimeClipboardService = new WindowsClipboardService();
            PlatformServices.ClipboardMonitor = new WindowsClipboardMonitorService(runtimeClipboardService);
#else
            runtimeClipboardService = new Services.AvaloniaClipboardService(
                desktop.MainWindow.Clipboard!,
                desktop.MainWindow.StorageProvider);
#endif

            PlatformServices.Clipboard = new ClipboardMonitorAwareClipboardService(
                runtimeClipboardService,
                PlatformServices.ClipboardMonitor);

            // Apply window state based on SilentRun.
            // We avoid starting minimized because some Windows setups can leave a minimized
            // thumbnail/button at the bottom-left instead of staying tray-only.
            // Hide synchronously on Opened and again at Send priority: Avalonia's lifetime
            // Show() can complete after Opened on some Windows setups, so one Hide() is
            // not always enough. MainWindow.OnWindowOpened also applies this once.
            if (silentRun)
            {
                desktop.MainWindow.ShowActivated = false;
                desktop.MainWindow.ShowInTaskbar = false;

                EventHandler? hideOnFirstOpen = null;
                hideOnFirstOpen = (_, _) =>
                {
                    if (desktop.MainWindow != null)
                    {
                        desktop.MainWindow.Opened -= hideOnFirstOpen;
                    }

                    HideMainWindowToTray(desktop.MainWindow);

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        HideMainWindowToTray(desktop.MainWindow);
                    }, Avalonia.Threading.DispatcherPriority.Send);
                };

                desktop.MainWindow.Opened += hideOnFirstOpen;
            }

            // Wire up Editor clipboard to platform implementation
            EditorServices.Clipboard = new Services.EditorClipboardAdapter();

            _workflowOrchestrator = new WorkflowOrchestrator(taskManager, screenRecordingCoordinator);
            _trayIconController = new TrayIconController();
            _workflowOrchestrator.Start(desktop, _baseTitle);
            TrayIconHelper.Instance.Initialize(screenRecordingCoordinator);
            _recordingEnterStopHotkey = new RecordingEnterStopHotkey(screenRecordingCoordinator, PlatformServices.Hotkey);
            _trayIconController.Initialize();
            InitializeClipboardMonitor(desktop.MainWindow);
            IdleMemoryTrimmer.Initialize(desktop, taskManager);

            desktop.Exit += (sender, args) =>
            {
                if (_clipboardChangedHandler != null)
                {
                    PlatformServices.ClipboardMonitor.ClipboardChanged -= _clipboardChangedHandler;
                    _clipboardChangedHandler = null;
                }
                PlatformServices.ClipboardMonitor.Stop();
                _recordingEnterStopHotkey?.Dispose();
                _recordingEnterStopHotkey = null;
                // OOBE/first-run planning:
                // Keep `IsFirstTimeRun=true` during the first session so UI (e.g. migration buttons) can show,
                // then persist it as completed when the app exits.
                if (XerahS.Core.SettingsManager.Settings.IsFirstTimeRun)
                {
                    XerahS.Core.SettingsManager.Settings.MarkFirstTimeRunCompleted(persist: false);
                }
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

    private void OnApplicationActivated(object? sender, ActivatedEventArgs eventArgs)
    {
        if (eventArgs is not ProtocolActivatedEventArgs protocolActivation ||
            !XerahSCloudOAuthCallbackParser.IsCallbackArgument(protocolActivation.Uri.AbsoluteUri))
        {
            return;
        }

        _ = CompleteCloudProtocolActivationAsync(protocolActivation.Uri);
    }

    private async Task CompleteCloudProtocolActivationAsync(Uri callbackUri)
    {
        try
        {
            IXerahSCloudOAuthCoordinator? coordinator =
                ServiceProvider?.GetService<IXerahSCloudOAuthCoordinator>();
            if (coordinator == null)
            {
                DebugHelper.WriteLine("XerahS Cloud OAuth protocol activation rejected: coordinator unavailable.");
                return;
            }

            XerahSCloudOAuthCompletion result = await coordinator
                .CompleteAsync(callbackUri)
                .ConfigureAwait(false);
            DebugHelper.WriteLine($"XerahS Cloud OAuth protocol activation result: {result}.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "XerahS Cloud OAuth protocol activation failed");
        }
    }

    /// <summary>
    /// Determines whether an exception that reached the Avalonia dispatcher unhandled should be
    /// suppressed instead of crashing the process. Only Linux desktop-integration failures are
    /// considered non-fatal: DBus errors (tray icon / StatusNotifier / dbusmenu / IME) and
    /// exceptions raised inside Avalonia.FreeDesktop. These features are optional; losing them
    /// (for example inside a Flatpak sandbox with restricted session-bus access) must not
    /// terminate the application.
    /// </summary>
    internal static bool IsNonFatalDispatcherException(Exception? ex)
    {
        if (ex is null)
        {
            return false;
        }

        if (ex is System.Threading.Tasks.TaskCanceledException)
        {
            return true;
        }

        if (ex is AggregateException aggregate)
        {
            return aggregate.InnerExceptions.Count > 0 &&
                   aggregate.InnerExceptions.All(IsNonFatalDispatcherException);
        }

        // Tmds.DBus.Protocol.DBusException / DBusErrorReplyException and friends. Matched by
        // namespace to avoid a hard dependency on the transitive Tmds.DBus.Protocol package.
        string typeName = ex.GetType().FullName ?? string.Empty;
        if (typeName.StartsWith("Tmds.DBus.", StringComparison.Ordinal))
        {
            return true;
        }

        // Failures inside Avalonia's FreeDesktop integration layer (tray icon, dbusmenu, IME).
        string stackTrace = ex.StackTrace ?? string.Empty;
        if (stackTrace.Contains("Avalonia.FreeDesktop.", StringComparison.Ordinal))
        {
            return true;
        }

        return ex.InnerException != null && IsNonFatalDispatcherException(ex.InnerException);
    }

    private static async Task ShowOnboardingWizardAsync(Window owner)
    {
        try
        {
            var wizard = new XerahS.UI.Onboarding.OnboardingWizardWindow();
            var result = await wizard.ShowDialogAsync(owner);

            if (result.Completed)
            {
                DebugHelper.WriteLine("[Onboarding] Wizard completed, marking first-time run complete.");
                SettingsManager.Settings.MarkFirstTimeRunCompleted(persist: false);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[Onboarding] Error showing wizard");
        }
    }

    /// <summary>
    /// Callback invoked after UI initialization completes.
    /// Set by Program.cs to perform platform-specific async initialization.
    /// </summary>
    public static Action? PostUIInitializationCallback { get; set; }
    public Core.Hotkeys.WorkflowManager? WorkflowManager => _workflowOrchestrator?.WorkflowManager;

    /// <summary>Runs a workflow as its hotkey would; false when the orchestrator is not ready.</summary>
    public bool TryRunWorkflow(Core.Hotkeys.WorkflowSettings workflow)
    {
        if (_workflowOrchestrator == null)
        {
            return false;
        }

        _ = _workflowOrchestrator.RunWorkflowAsync(workflow);
        return true;
    }

    private static void HideMainWindowToTray(Window? window)
    {
        if (window == null || IsExiting || !SettingsManager.Settings.SilentRun)
        {
            return;
        }

        bool wasVisible = window.IsVisible;
        Helpers.SilentRunStartupPolicy.ApplyHiddenToTray(window);
        if (wasVisible)
        {
            Common.DebugHelper.WriteLine("SilentRun startup: main window hidden to tray.");
        }
    }

    public static void ApplyMenuBarOnlyModeFromSettings()
    {
        try
        {
            var systemService = PlatformServices.System;
            if (!systemService.IsMenuBarOnlyModeSupported)
            {
                return;
            }

            bool enabled = SettingsManager.Settings.SilentRun;
            if (!systemService.SetMenuBarOnlyMode(enabled))
            {
                DebugHelper.WriteLine($"Menu-bar-only mode could not be {(enabled ? "enabled" : "disabled")}.");
            }

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                desktop.MainWindow.ShowInTaskbar = !enabled;
            }
        }
        catch (InvalidOperationException)
        {
            // Platform services are not available during a few settings-unit-test paths.
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to apply menu-bar-only mode.");
        }
    }

    /// <summary>
    /// Allows the settings UI to start or stop the clipboard monitor at runtime.
    /// </summary>
    public static void SetClipboardMonitorEnabled(bool enabled)
    {
        if (Application.Current is not App app)
            return;

        var monitor = PlatformServices.ClipboardMonitor;
        if (!monitor.IsSupported)
            return;

        if (enabled)
        {
            if (!monitor.IsMonitoring)
            {
                app.EnsureClipboardChangedHandler();
                monitor.Start();
                DebugHelper.WriteLine("Clipboard monitor started via settings.");
            }
        }
        else
        {
            monitor.Stop();
            DebugHelper.WriteLine("Clipboard monitor stopped via settings.");
        }
    }

    private void InitializeClipboardMonitor(Window owner)
    {
        try
        {
            if (!SettingsManager.Settings.ShowClipboardContentViewer)
            {
                return;
            }

            var monitor = PlatformServices.ClipboardMonitor;
            if (!monitor.IsSupported)
            {
                return;
            }

            EnsureClipboardChangedHandler();
            monitor.Start();
            DebugHelper.WriteLine("Clipboard monitor started: auto-open Clipboard Viewer on clipboard changes.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to initialize clipboard monitor.");
        }
    }

    private void EnsureClipboardChangedHandler()
    {
        if (_clipboardChangedHandler != null)
            return;

        var monitor = PlatformServices.ClipboardMonitor;
        Window? owner = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            owner = desktop.MainWindow;

        _clipboardChangedHandler = (_, _) =>
        {
            if (!SettingsManager.Settings.ShowClipboardContentViewer)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (now - _lastClipboardViewerAutoOpenUtc < ClipboardViewerAutoOpenCooldown)
            {
                return;
            }

            _lastClipboardViewerAutoOpenUtc = now;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (IsMainWindowMenuOpen())
                {
                    return;
                }

                _ = UploadContentToolService.HandleWorkflowAsync(WorkflowType.ClipboardViewer, owner, background: true);
            }, Avalonia.Threading.DispatcherPriority.Background);
        };

        monitor.ClipboardChanged += _clipboardChangedHandler;
    }

    private bool IsMainWindowMenuOpen()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return false;

        if (desktop.MainWindow is not Window mainWindow || !mainWindow.IsActive)
            return false;

        return HasOpenSubMenu(mainWindow);
    }

    private static bool HasOpenSubMenu(Visual root)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is MenuItem { IsSubMenuOpen: true })
                return true;

            if (child is Popup { IsOpen: true })
                return true;

            if (HasOpenSubMenu(child))
                return true;
        }

        return false;
    }

    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        _trayIconController?.HandleClicked();
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

    private void OnHistoryItemMenuFlyoutOpened(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout menuFlyout)
        {
            return;
        }

        if (menuFlyout.Target is not Control target || target.Tag is not IHistoryItemMenuContext context)
        {
            return;
        }

        ApplyMenuContext(menuFlyout.Items, context);
    }

    private static void ApplyMenuContext(IEnumerable<object?> items, IHistoryItemMenuContext context)
    {
        foreach (object? item in items)
        {
            if (item is MenuItem menuItem)
            {
                menuItem.DataContext = context;

                if (menuItem.Items.Count > 0)
                {
                    ApplyMenuContext(menuItem.Items, context);
                }
            }
        }
    }

}
