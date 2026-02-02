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
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Core.Managers;
using XerahS.RegionCapture.ScreenRecording;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

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

    public event PropertyChangedEventHandler? PropertyChanged;

    public NativeMenu TrayMenu { get; private set; }

    public ICommand OpenMainWindowCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand TrayClickCommand { get; }

    // Recording commands for tray menu
    public ICommand PauseResumeRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand AbortRecordingCommand { get; }

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
    private const string RecordingIconPath = "avares://XerahS.UI/Assets/tray-recording.png";
    private const string PausedIconPath = "avares://XerahS.UI/Assets/tray-recording-paused.png";

    private RecordingStatus _currentRecordingStatus = RecordingStatus.Idle;

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
            string iconPath = _currentRecordingStatus switch
            {
                RecordingStatus.Recording or RecordingStatus.Initializing => RecordingIconPath,
                RecordingStatus.Paused or RecordingStatus.Finalizing => PausedIconPath,
                _ => DefaultIconPath
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
                    var uri = new Uri(DefaultIconPath);
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

    /// <summary>
    /// Tooltip text changes based on recording state.
    /// </summary>
    public string TrayToolTipText
    {
        get
        {
            return _currentRecordingStatus switch
            {
                RecordingStatus.Recording => $"{AppResources.AppName} - Recording (click tray to stop)",
                RecordingStatus.Paused => $"{AppResources.AppName} - Paused (click tray to resume)",
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

        // Initialize from settings
        _showTray = SettingsManager.Settings.ShowTray;

        // Build initial menu
        BuildTrayMenu();

        // Subscribe to settings changes
        SettingsManager.SettingsChanged += OnSettingsChanged;

        // Subscribe to recording state changes to update tray icon and menu
        ScreenRecordingManager.Instance.StatusChanged += OnRecordingStatusChanged;
        ScreenRecordingManager.Instance.RecordingStarted += OnRecordingStarted;
        ScreenRecordingManager.Instance.ErrorOccurred += OnRecordingError;
    }

    /// <summary>
    /// Handles recording status changes to update tray icon and menu.
    /// </summary>
    private void OnRecordingStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        // Ensure UI updates happen on the Avalonia UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var previousStatus = _currentRecordingStatus;
            _currentRecordingStatus = e.Status;

            DebugHelper.WriteLine($"TrayIconHelper: Recording status changed from {previousStatus} to {e.Status}");

            // Notify property changes for icon and tooltip
            OnPropertyChanged(nameof(CurrentTrayIcon));
            OnPropertyChanged(nameof(TrayToolTipText));
            OnPropertyChanged(nameof(IsRecordingActive));

            // Rebuild menu to add/remove recording-specific items
            BuildTrayMenu();
        });
    }

    /// <summary>
    /// Handles recording started event.
    /// </summary>
    private void OnRecordingStarted(object? sender, RecordingStartedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            DebugHelper.WriteLine($"TrayIconHelper: Recording started (fallback={e.IsUsingFallback})");
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
            }
        });
    }

    /// <summary>
    /// Pauses or resumes the current recording.
    /// </summary>
    private async Task PauseResumeRecordingAsync()
    {
        try
        {
            await ScreenRecordingManager.Instance.TogglePauseResumeAsync();
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
        try
        {
            await ScreenRecordingManager.Instance.StopRecordingAsync();
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
        try
        {
            await ScreenRecordingManager.Instance.AbortRecordingAsync();
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "TrayIconHelper: Failed to abort recording");
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Update ShowTray when settings change
        ShowTray = SettingsManager.Settings.ShowTray;
        // Rebuild menu on settings change (e.g. workflows changed)
        BuildTrayMenu();
    }

    /// <summary>
    /// Call this method to refresh the ShowTray property from settings.
    /// Useful when settings are changed externally.
    /// </summary>
    public void RefreshFromSettings()
    {
        ShowTray = SettingsManager.Settings.ShowTray;
        BuildTrayMenu();
    }

    public void BuildTrayMenu()
    {
        TrayMenu.Items.Clear();

        // Add recording-specific menu items at the top when recording is active
        // This matches ShareX behavior where recording controls appear first
        if (IsRecordingActive)
        {
            AddRecordingMenuItems();
            TrayMenu.Items.Add(new NativeMenuItemSeparator());
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
                    TrayMenu.Items.Add(item);
                }
                index++;
            }
        }

        if (TrayMenu.Items.Count > 0)
        {
            TrayMenu.Items.Add(new NativeMenuItemSeparator());
        }

        TrayMenu.Items.Add(new NativeMenuItem { Header = "Open Main Window", Command = OpenMainWindowCommand });
        TrayMenu.Items.Add(new NativeMenuItem { Header = "Settings", Command = OpenSettingsCommand });
        TrayMenu.Items.Add(new NativeMenuItemSeparator());
        TrayMenu.Items.Add(new NativeMenuItem { Header = "Exit", Command = ExitCommand });

        OnPropertyChanged(nameof(TrayMenu));
    }

    /// <summary>
    /// Adds recording-specific menu items when a recording session is active.
    /// Menu items change based on whether recording is in progress or paused:
    /// - Recording: Shows "Pause Recording", "Stop Recording", "Abort Recording"
    /// - Paused: Shows "Resume Recording", "Stop Recording", "Abort Recording"
    /// - Initializing/Finalizing: Shows only "Abort Recording"
    /// </summary>
    private void AddRecordingMenuItems()
    {
        bool canPauseResume = _currentRecordingStatus is RecordingStatus.Recording or RecordingStatus.Paused;
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

            TrayMenu.Items.Add(new NativeMenuItem
            {
                Header = pauseResumeText,
                Command = PauseResumeRecordingCommand
            });
        }

        if (canStop)
        {
            TrayMenu.Items.Add(new NativeMenuItem
            {
                Header = "Stop Recording",
                Command = StopRecordingCommand
            });
        }

        if (canAbort)
        {
            TrayMenu.Items.Add(new NativeMenuItem
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
        if (workflow != null)
        {
            DebugHelper.WriteLine($"Tray: Execute workflow (ID: {workflow.Id}): {workflow}");
            // Hide main window for tray-triggered captures (user clicked tray menu)
            await Core.Helpers.TaskHelpers.ExecuteWorkflow(workflow, workflow.Id, hideMainWindow: true);
        }
    }

    private void OpenMainWindow()
    {
        DebugHelper.WriteLine("Tray: Open Main Window");
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.MainWindow;
            if (window != null)
            {
                // Ensure taskbar visibility is restored
                window.ShowInTaskbar = true;
                
                window.Show();
                
                // If minimized, restore it
                if (window.WindowState == Avalonia.Controls.WindowState.Minimized)
                {
                    window.WindowState = Avalonia.Controls.WindowState.Maximized;
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
            // Ensure visibility before navigating
            mainWindow.ShowInTaskbar = true;
            mainWindow.Show();
            
            if (mainWindow.WindowState == Avalonia.Controls.WindowState.Minimized)
            {
                mainWindow.WindowState = Avalonia.Controls.WindowState.Maximized;
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

    public void OnTrayClick()
    {
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
                // For tray click actions, execute first matching workflow
                // Hide main window for tray-triggered captures (user clicked tray icon)
                var workflow = SettingsManager.WorkflowsConfig?.Hotkeys?.FirstOrDefault(w => w.Job == action);
                if (workflow != null)
                {
                    await Core.Helpers.TaskHelpers.ExecuteWorkflow(workflow, workflow.Id, hideMainWindow: true);
                }
                else
                {
                    // Fallback for actions that aren't workflow-based
                    await Core.Helpers.TaskHelpers.ExecuteJob(action, new TaskSettings { Job = action }, hideMainWindow: true);
                }
                break;
        }
    }
}

