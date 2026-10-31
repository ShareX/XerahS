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

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using XerahS.Bootstrap;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Core.Managers;
using XerahS.RegionCapture.ScreenRecording;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RecordingCaptureMode = XerahS.RegionCapture.ScreenRecording.CaptureMode;

namespace XerahS.UI;

/// <summary>
/// Helper class for TrayIcon commands and actions.
/// Implements INotifyPropertyChanged for dynamic XAML binding updates.
/// Provides a singleton instance accessible from XAML bindings.
/// Supports recording state by switching icons and adding context-sensitive menu items.
/// </summary>
public class TrayIconHelper : INotifyPropertyChanged
{
    private static TrayIconHelper? _instance;
    public static TrayIconHelper Instance => _instance ??= new TrayIconHelper();
    private IScreenRecordingCoordinator? _screenRecordingCoordinator;

    public event PropertyChangedEventHandler? PropertyChanged;

    public NativeMenu TrayMenu { get; }

    public ICommand OpenMainWindowCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand TrayClickCommand { get; }

    // Recording commands for tray menu
    public ICommand PauseResumeRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand AbortRecordingCommand { get; }
    public ICommand RestartRecordingCommand { get; }

    private bool _showTray;
    public bool ShowTray
    {
        get => _showTray;
        set
        {
            if (_showTray != value)
            {
                _showTray = value;
                OnPropertyChanged();
            }
        }
    }

    // Tray icon paths for different recording states
    private const string DefaultIconPath = "avares://XerahS.UI/Assets/ShareX.iconset/icon_16x16.png";
    // White/monochrome icon for dark panels (macOS menu bar, GNOME top bar via AppIndicator)
    private const string WhiteIconPath = "avares://XerahS.UI/Assets/tray-default-white.png";
    private const string RecordingIconPath = "avares://XerahS.UI/Assets/tray-recording.png";
    private const string PausedIconPath = "avares://XerahS.UI/Assets/tray-recording-paused.png";

    private RecordingStatus _currentRecordingStatus = RecordingStatus.Idle;

    // Border window shown around the recording area (visible for all recording types including GIF)
    private Views.RecordingBorderWindow? _borderWindow;

    /// <summary>
    /// Current tray icon based on recording state.
    /// - Idle/Error: Default application icon
    /// - Recording/Initializing: Red recording icon
    /// - Paused/Finalizing: Yellow paused icon
    /// </summary>
    public WindowIcon CurrentTrayIcon
    {
        get
        {
            string idleIconPath = GetIdleIconPath();
            string iconPath = _currentRecordingStatus switch
            {
                RecordingStatus.Recording or RecordingStatus.Initializing => RecordingIconPath,
                RecordingStatus.Paused or RecordingStatus.Finalizing => PausedIconPath,
                _ => idleIconPath
            };

            try
            {
                var uri = new Uri(iconPath);
                var assets = Avalonia.Platform.AssetLoader.Open(uri);
                return new WindowIcon(assets);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, $"Failed to load tray icon: {iconPath}");
                // Fallback to default icon
                try
                {
                    var uri = new Uri(idleIconPath);
                    var assets = Avalonia.Platform.AssetLoader.Open(uri);
                    return new WindowIcon(assets);
                }
                catch
                {
                    return null!;
                }
            }
        }
    }

    private static string GetIdleIconPath()
    {
        // The white/monochrome tray icon is opt-in everywhere. The setting is auto-flipped
        // to true on Linux/macOS (dark system panels — menu bar, GNOME/KDE top bar) but the
        // user can override it on any platform, including Windows. See issue #261.
        bool useWhite = SettingsManager.Settings.UseWhiteShareXIcon;
        return useWhite ? WhiteIconPath : DefaultIconPath;
    }

    /// <summary>
    /// Tooltip text changes based on recording state.
    /// </summary>
    public string TrayToolTipText
    {
        get
        {
            return _currentRecordingStatus switch
            {
                RecordingStatus.Recording => $"{AppResources.AppName} - Recording (Enter or tray click to stop)",
                RecordingStatus.Paused => $"{AppResources.AppName} - Paused (Enter to stop, tray click to resume)",
                RecordingStatus.Initializing => $"{AppResources.AppName} - Starting recording...",
                RecordingStatus.Finalizing => $"{AppResources.AppName} - Finalizing...",
                _ => AppResources.AppName
            };
        }
    }

    /// <summary>
    /// Indicates if a recording session is currently active (recording or paused).
    /// </summary>
    public bool IsRecordingActive => _currentRecordingStatus is RecordingStatus.Recording
        or RecordingStatus.Paused
        or RecordingStatus.Initializing
        or RecordingStatus.Finalizing;

    private TrayIconHelper()
    {
        TrayMenu = new NativeMenu();

        OpenMainWindowCommand = new RelayCommand(OpenMainWindow);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        ExitCommand = new RelayCommand(Exit);
        TrayClickCommand = new RelayCommand(OnTrayClick);

        // Recording commands
        PauseResumeRecordingCommand = new AsyncRelayCommand(PauseResumeRecordingAsync);
        StopRecordingCommand = new AsyncRelayCommand(StopRecordingAsync);
        AbortRecordingCommand = new AsyncRelayCommand(AbortRecordingAsync);
        RestartRecordingCommand = new RelayCommand(() => _screenRecordingCoordinator?.RequestRestart());

        // Initialize from settings
        _showTray = SettingsManager.Settings.ShowTray;

        // Build initial menu
        BuildTrayMenu();

        // Subscribe to settings changes
        SettingsManager.SettingsChanged += OnSettingsChanged;

    }

    public void Initialize(IScreenRecordingCoordinator screenRecordingCoordinator)
    {
        if (ReferenceEquals(_screenRecordingCoordinator, screenRecordingCoordinator))
        {
            return;
        }

        if (_screenRecordingCoordinator != null)
        {
            _screenRecordingCoordinator.StatusChanged -= OnRecordingStatusChanged;
            _screenRecordingCoordinator.RecordingStarted -= OnRecordingStarted;
            _screenRecordingCoordinator.ErrorOccurred -= OnRecordingError;
        }

        _screenRecordingCoordinator = screenRecordingCoordinator;
        _screenRecordingCoordinator.StatusChanged += OnRecordingStatusChanged;
        _screenRecordingCoordinator.RecordingStarted += OnRecordingStarted;
        _screenRecordingCoordinator.ErrorOccurred += OnRecordingError;

        _currentRecordingStatus = _screenRecordingCoordinator.IsPaused
            ? RecordingStatus.Paused
            : _screenRecordingCoordinator.IsRecording
                ? RecordingStatus.Recording
                : RecordingStatus.Idle;

        OnPropertyChanged(nameof(CurrentTrayIcon));
        OnPropertyChanged(nameof(TrayToolTipText));
        OnPropertyChanged(nameof(IsRecordingActive));
        BuildTrayMenu();
    }

    /// <summary>
    /// Handles recording status changes to update tray icon, menu, and border window.
    /// Only rebuilds the tray menu when the recording active state actually transitions
    /// (e.g. Idle->Recording or Recording->Idle), not on every duration tick.
    /// </summary>
    private void OnRecordingStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        // Ensure UI updates happen on the Avalonia UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var previousStatus = _currentRecordingStatus;
            bool wasActive = IsRecordingActive;

            _currentRecordingStatus = e.Status;

            bool isNowActive = IsRecordingActive;

            // Lightweight property updates (icon/tooltip) are fine on every tick
            OnPropertyChanged(nameof(CurrentTrayIcon));
            OnPropertyChanged(nameof(TrayToolTipText));

            // Only rebuild the menu when recording active state transitions or
            // when pause/resume changes the menu items (status category changes)
            bool stateChanged = wasActive != isNowActive;
            bool pauseToggled = (previousStatus == RecordingStatus.Paused) != (e.Status == RecordingStatus.Paused);
            // Initializing→Recording: both are "active" so stateChanged=false, but Stop becomes available now
            bool stopBecameAvailable = e.Status == RecordingStatus.Recording && previousStatus == RecordingStatus.Initializing;

            if (stateChanged || pauseToggled || stopBecameAvailable)
            {
                DebugHelper.WriteLine($"TrayIconHelper: Recording state changed from {previousStatus} to {e.Status}, rebuilding menu");
                OnPropertyChanged(nameof(IsRecordingActive));
                BuildTrayMenu();
            }

            // Hide border window when recording ends (Idle or Error)
            if (e.Status is RecordingStatus.Idle or RecordingStatus.Error)
            {
                HideBorderWindow();
            }
        });
    }

    /// <summary>
    /// Handles recording started event.
    /// Shows the recording border window around the capture area.
    /// Border color indicates recording technology: Red for FFmpeg/GDI, Green for Modern Capture.
    /// </summary>
    private void OnRecordingStarted(object? sender, RecordingStartedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                DebugHelper.WriteLine($"TrayIconHelper: Recording started (fallback={e.IsUsingFallback})");

                // Skip border window on Linux - transparent windows don't work well on Wayland
                if (OperatingSystem.IsLinux())
                {
                    DebugHelper.WriteLine("TrayIconHelper: Skipping recording border on Linux (transparency not supported on Wayland)");
                    return;
                }

                // Create and show border window around the recording area
                _borderWindow = new Views.RecordingBorderWindow();

                // Set border color: Red for FFmpeg/GDI fallback, Green for Modern Capture
                string borderColor = e.IsUsingFallback ? "Red" : "Green";
                _borderWindow.SetBorderColor(borderColor);

                // Determine recording area bounds from options
                var bounds = GetRecordingBounds(e.Options);
                _borderWindow.SetBounds(bounds);

                // Show the border window
                _borderWindow.Show();

                DebugHelper.WriteLine($"TrayIconHelper: Recording border shown - {borderColor} border for {(e.IsUsingFallback ? "FFmpeg/GDI" : "Modern Capture")} recording, bounds={bounds}");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "TrayIconHelper: Failed to show recording border window");
            }
        });
    }

    /// <summary>
    /// Handles recording errors.
    /// </summary>
    private void OnRecordingError(object? sender, RecordingErrorEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (e.IsFatal)
            {
                _currentRecordingStatus = RecordingStatus.Error;
                OnPropertyChanged(nameof(CurrentTrayIcon));
                OnPropertyChanged(nameof(TrayToolTipText));
                OnPropertyChanged(nameof(IsRecordingActive));
                BuildTrayMenu();
                HideBorderWindow();
            }
        });
    }

    /// <summary>
    /// Calculates the recording area bounds based on recording options.
    /// </summary>
    private static System.Drawing.Rectangle GetRecordingBounds(RecordingOptions options)
    {
        switch (options.Mode)
        {
            case RecordingCaptureMode.Region:
                // Use the specified region
                return options.Region;

            case RecordingCaptureMode.Window:
                // Get window bounds from platform services
                if (options.TargetWindowHandle != IntPtr.Zero)
                {
                    try
                    {
                        return Platform.Abstractions.PlatformServices.Window.GetWindowBounds(options.TargetWindowHandle);
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, "TrayIconHelper: Failed to get window bounds");
                    }
                }
                // Fall through to screen mode if window bounds fail
                goto case RecordingCaptureMode.Screen;

            case RecordingCaptureMode.Screen:
            default:
                // Get primary screen bounds
                var screens = Platform.Abstractions.PlatformServices.Screen.GetAllScreens();
                var primaryScreen = screens.FirstOrDefault(s => s.IsPrimary);
                if (primaryScreen != null)
                {
                    return new System.Drawing.Rectangle(
                        primaryScreen.Bounds.Left,
                        primaryScreen.Bounds.Top,
                        primaryScreen.Bounds.Width,
                        primaryScreen.Bounds.Height);
                }
                // Default fallback
                return new System.Drawing.Rectangle(0, 0, 1920, 1080);
        }
    }

    /// <summary>
    /// Closes and disposes the recording border window.
    /// </summary>
    private void HideBorderWindow()
    {
        if (_borderWindow != null)
        {
            try
            {
                _borderWindow.Close();
                _borderWindow = null;
                DebugHelper.WriteLine("TrayIconHelper: Recording border window hidden");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "TrayIconHelper: Failed to hide recording border window");
            }
        }
    }

    /// <summary>
    /// Pauses or resumes the current recording.
    /// </summary>
    private async Task PauseResumeRecordingAsync()
    {
        if (_screenRecordingCoordinator == null)
        {
            return;
        }

        if (!_screenRecordingCoordinator.CurrentCapabilities.SupportsPauseResume)
        {
            DebugHelper.WriteLine("TrayIconHelper: Pause/resume is unavailable for the active recording backend.");
            return;
        }

        try
        {
            await _screenRecordingCoordinator.TogglePauseResumeAsync();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "TrayIconHelper: Failed to pause/resume recording");
        }
    }

    /// <summary>
    /// Stops the current recording and saves the output.
    /// </summary>
    private async Task StopRecordingAsync()
    {
        if (_screenRecordingCoordinator == null)
        {
            return;
        }

        try
        {
            await _screenRecordingCoordinator.StopRecordingAsync();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "TrayIconHelper: Failed to stop recording");
        }
    }

    /// <summary>
    /// Aborts the current recording without saving.
    /// </summary>
    private async Task AbortRecordingAsync()
    {
        if (_screenRecordingCoordinator == null)
        {
            return;
        }

        try
        {
            await _screenRecordingCoordinator.AbortRecordingAsync();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "TrayIconHelper: Failed to abort recording");
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Saves run on the capture worker. Touching the tray there clears the menu,
        // then NativeMenuItem.Parent throws because the menu belongs to the UI thread.
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => OnSettingsChanged(sender, e));
            return;
        }

        // Update ShowTray when settings change
        ShowTray = SettingsManager.Settings.ShowTray;
        OnPropertyChanged(nameof(CurrentTrayIcon));
        // Rebuild menu on settings change (e.g. workflows changed)
        BuildTrayMenu();
    }

    /// <summary>
    /// Call this method to refresh the ShowTray property from settings.
    /// Useful when settings are changed externally.
    /// </summary>
    public void RefreshFromSettings()
    {
        OnSettingsChanged(this, EventArgs.Empty);
    }

    public void BuildTrayMenu()
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(BuildTrayMenu);
            return;
        }

        // Prepare the items before touching the exported menu. All NativeMenu
        // creation and mutation must stay on the UI thread (including on Linux).
        var menu = new NativeMenu();

        // Add recording-specific menu items at the top when recording is active
        // This matches ShareX behavior where recording controls appear first
        if (IsRecordingActive)
        {
            AddRecordingMenuItems(menu);
            menu.Items.Add(new NativeMenuItemSeparator());
        }

        var workflows = SettingsManager.WorkflowsConfig?.Hotkeys;
        if (workflows != null)
        {
            int index = 0;
            foreach (var workflow in workflows)
            {
                // Include if it's one of the top 3 (NavWorkflow) OR manually pinned
                if (index < 3 || workflow.PinnedToTray)
                {
                    string displayName = GetWorkflowName(workflow);
                    var item = new NativeMenuItem
                    {
                        Header = displayName,
                        Command = new RelayCommand(() => ExecuteWorkflow(workflow))
                    };
                    menu.Items.Add(item);
                }
                index++;
            }
        }

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new NativeMenuItemSeparator());
        }

        menu.Items.Add(new NativeMenuItem { Header = "Open Main Window", Command = OpenMainWindowCommand });
        menu.Items.Add(new NativeMenuItem { Header = "Settings", Command = OpenSettingsCommand });
        menu.Items.Add(new NativeMenuItem { Header = "Review pending uploads", Command = new RelayCommand(() =>
        {
            OpenMainWindowCommand.Execute(null);
            Core.Services.UploadQueueService.Instance.ResumePendingReviews();
        }) });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem { Header = "Exit", Command = ExitCommand });

        // macOS's native exporter retains a proxy for the original menu. Replacing
        // that root throws "The menu being updated does not match" (#300).
        // Detach the prepared items from their temporary parent before moving them.
        var items = menu.Items.ToArray();
        menu.Items.Clear();
        TrayMenu.Items.Clear();
        foreach (var item in items)
        {
            TrayMenu.Items.Add(item);
        }

        DebugHelper.WriteLine($"Tray menu rebuilt with {TrayMenu.Items.Count} items.");
    }

    /// <summary>
    /// Adds recording-specific menu items when a recording session is active.
    /// Menu items change based on whether recording is in progress or paused:
    /// - Recording: Shows "Pause Recording", "Stop Recording", "Abort Recording"
    /// - Paused: Shows "Resume Recording", "Stop Recording", "Abort Recording"
    /// - Initializing/Finalizing: Shows only "Abort Recording"
    /// </summary>
    private void AddRecordingMenuItems(NativeMenu menu)
    {
        bool canPauseResume = _screenRecordingCoordinator?.CurrentCapabilities.SupportsPauseResume == true &&
            (_currentRecordingStatus is RecordingStatus.Recording or RecordingStatus.Paused);
        bool canStop = _currentRecordingStatus is RecordingStatus.Recording or RecordingStatus.Paused;
        bool canAbort = _currentRecordingStatus is RecordingStatus.Recording
            or RecordingStatus.Paused
            or RecordingStatus.Initializing;

        if (canPauseResume)
        {
            // Toggle between Pause/Resume based on current state
            string pauseResumeText = _currentRecordingStatus == RecordingStatus.Paused
                ? "Resume Recording"
                : "Pause Recording";

            menu.Items.Add(new NativeMenuItem
            {
                Header = pauseResumeText,
                Command = PauseResumeRecordingCommand
            });
        }

        if (canStop)
        {
            menu.Items.Add(new NativeMenuItem
            {
                Header = "Stop Recording",
                Command = StopRecordingCommand
            });
            menu.Items.Add(new NativeMenuItem
            {
                Header = "Restart Recording",
                Command = RestartRecordingCommand
            });
        }

        if (canAbort)
        {
            menu.Items.Add(new NativeMenuItem
            {
                Header = "Abort Recording",
                Command = AbortRecordingCommand
            });
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Get workflow display name
    /// </summary>
    private static string GetWorkflowName(WorkflowSettings workflow)
    {
        if (workflow == null) return "Unknown Workflow";

        // Priority 1: Custom description from TaskSettings (same as Navigation Bar)
        if (!string.IsNullOrEmpty(workflow.TaskSettings?.Description))
        {
            return workflow.TaskSettings.Description;
        }

        // Priority 2: Workflow Name property (backward compatibility)
        if (!string.IsNullOrEmpty(workflow.Name))
        {
            return workflow.Name;
        }

        // Priority 3: Default Job description
        return EnumExtensions.GetDescription(workflow.Job);
    }

    private async void ExecuteWorkflow(WorkflowSettings workflow)
    {
        if (workflow == null) return;

        DebugHelper.WriteLine($"Tray: Execute workflow (ID: {workflow.Id}): {workflow}");

        // Toggle behavior: if a recording is active and this is a recording workflow,
        // stop the existing recording instead of trying to start a new one
        if (IsRecordingActive && Core.TaskHelpers.IsScreenRecordStartJob(workflow.Job))
        {
            DebugHelper.WriteLine("Tray: Recording already active, stopping existing recording");
            await StopRecordingAsync();
            return;
        }

        // On Linux (Wayland), don't hide the main window for recording workflows.
        // The XDG ScreenCast portal requires a valid parent window surface during
        // PipeWire stream negotiation; hiding it invalidates the session.
        bool hideWindow = true;
        if (OperatingSystem.IsLinux() && Core.TaskHelpers.IsScreenRecordStartJob(workflow.Job))
        {
            hideWindow = false;
        }

        await Core.Helpers.TaskHelpers.ExecuteWorkflow(workflow, workflow.Id, hideMainWindow: hideWindow);
    }

    private void OpenMainWindow()
    {
        DebugHelper.WriteLine("Tray: Open Main Window");
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.MainWindow;
            if (window != null)
            {
                // On macOS, SilentRun doubles as menu-bar-only mode, so keep the Dock icon hidden.
                window.ShowInTaskbar = ShouldShowInTaskbar();
                
                window.Show();
                
                // If minimized, restore it
                if (window.WindowState == Avalonia.Controls.WindowState.Minimized)
                {
                    window.WindowState = ShouldRestoreToNormalWindowState()
                        ? Avalonia.Controls.WindowState.Normal
                        : Avalonia.Controls.WindowState.Maximized;
                }
                
                window.Activate();
                window.Focus();
            }
        }
    }

    private void OpenSettings()
    {
        DebugHelper.WriteLine("Tray: Open Settings");
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is Views.MainWindow mainWindow)
        {
            // Ensure visibility before navigating while preserving macOS menu-bar-only mode.
            mainWindow.ShowInTaskbar = ShouldShowInTaskbar();
            mainWindow.Show();
            
            if (mainWindow.WindowState == Avalonia.Controls.WindowState.Minimized)
            {
                mainWindow.WindowState = ShouldRestoreToNormalWindowState()
                    ? Avalonia.Controls.WindowState.Normal
                    : Avalonia.Controls.WindowState.Maximized;
            }

            mainWindow.Activate();
            mainWindow.NavigateToSettings();
        }
    }

    private void Exit()
    {
        DebugHelper.WriteLine("Tray: Exit");
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Flag that we are explicitly exiting, so OnClosing won't cancel
            App.IsExiting = true;
            desktop.Shutdown();
        }
    }

    private static bool ShouldRestoreToNormalWindowState()
    {
        ApplicationConfig settings = SettingsManager.Settings;
        return settings.RememberMainFormSize || settings.RememberMainFormPosition;
    }

    private static bool ShouldShowInTaskbar()
    {
        return !OperatingSystem.IsMacOS() || !SettingsManager.Settings.SilentRun;
    }

    public void OnTrayClick()
    {
        // When recording is active, tray click honours the tooltip promise:
        // "Recording (Enter or tray click to stop)" / "Paused (Enter to stop, tray click to resume)"
        if (_currentRecordingStatus == RecordingStatus.Recording)
        {
            DebugHelper.WriteLine("Tray click: Recording active, stopping recording");
            _ = StopRecordingAsync();
            return;
        }

        if (_currentRecordingStatus == RecordingStatus.Paused)
        {
            DebugHelper.WriteLine("Tray click: Recording paused, resuming");
            _ = PauseResumeRecordingAsync();
            return;
        }

        // Execute the configured left-click action
        var action = SettingsManager.Settings.TrayLeftClickAction;
        DebugHelper.WriteLine($"Tray click: {action}");
        ExecuteTrayAction(action);
    }

    public void OnTrayDoubleClick()
    {
        var action = SettingsManager.Settings.TrayLeftDoubleClickAction;
        DebugHelper.WriteLine($"Tray double click: {action}");
        ExecuteTrayAction(action);
    }

    public void OnTrayMiddleClick()
    {
        var action = SettingsManager.Settings.TrayMiddleClickAction;
        DebugHelper.WriteLine($"Tray middle click: {action}");
        ExecuteTrayAction(action);
    }

    private async void ExecuteTrayAction(WorkflowType action)
    {
        switch (action)
        {
            case WorkflowType.OpenMainWindow:
                OpenMainWindow();
                break;
            default:
                // Toggle behavior: if a recording is active and this is a recording action,
                // stop the existing recording instead of trying to start a new one
                if (IsRecordingActive && Core.TaskHelpers.IsScreenRecordStartJob(action))
                {
                    DebugHelper.WriteLine("Tray action: Recording already active, stopping existing recording");
                    await StopRecordingAsync();
                    return;
                }

                bool hideWindow = !OperatingSystem.IsLinux() || !Core.TaskHelpers.IsScreenRecordStartJob(action);

                var workflow = SettingsManager.WorkflowsConfig?.Hotkeys?.FirstOrDefault(w => w.Job == action);
                if (workflow != null)
                {
                    await Core.Helpers.TaskHelpers.ExecuteWorkflow(workflow, workflow.Id, hideMainWindow: hideWindow);
                }
                else
                {
                    await Core.Helpers.TaskHelpers.ExecuteJob(action, new TaskSettings { Job = action }, hideMainWindow: hideWindow);
                }
                break;
        }
    }
}
