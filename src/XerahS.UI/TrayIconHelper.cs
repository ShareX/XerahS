#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace XerahS.UI;

/// <summary>
/// Helper class for TrayIcon commands and actions.
/// Implements INotifyPropertyChanged for dynamic XAML binding updates.
/// Provides a singleton instance accessible from XAML bindings.
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

    private TrayIconHelper()
    {
        TrayMenu = new NativeMenu();
        
        OpenMainWindowCommand = new RelayCommand(OpenMainWindow);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        ExitCommand = new RelayCommand(Exit);
        TrayClickCommand = new RelayCommand(OnTrayClick);

        // Initialize from settings
        _showTray = SettingsManager.Settings.ShowTray;

        // Build initial menu
        BuildTrayMenu();

        // Subscribe to settings changes 
        SettingsManager.SettingsChanged += OnSettingsChanged;
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
            await Core.Helpers.TaskHelpers.ExecuteWorkflow(workflow, workflow.Id);
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

    private async void ExecuteTrayAction(HotkeyType action)
    {
        switch (action)
        {
            case HotkeyType.OpenMainWindow:
                OpenMainWindow();
                break;
            default:
                // For tray click actions, execute first matching workflow
                var workflow = SettingsManager.WorkflowsConfig?.Hotkeys?.FirstOrDefault(w => w.Job == action);
                if (workflow != null)
                {
                    await Core.Helpers.TaskHelpers.ExecuteWorkflow(workflow, workflow.Id);
                }
                else
                {
                    // Fallback for actions that aren't workflow-based
                    await Core.Helpers.TaskHelpers.ExecuteJob(action, new TaskSettings { Job = action });
                }
                break;
        }
    }
}

