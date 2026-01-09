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

    // Dynamic workflow commands (first 3 workflows from list)
    public ICommand Workflow1Command { get; }
    public ICommand Workflow2Command { get; }
    public ICommand Workflow3Command { get; }
    public ICommand OpenMainWindowCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand TrayClickCommand { get; }

    // Workflow names for tray menu display
    public string Workflow1Name => GetWorkflowName(0) ?? "Workflow 1";
    public string Workflow2Name => GetWorkflowName(1) ?? "Workflow 2";
    public string Workflow3Name => GetWorkflowName(2) ?? "Workflow 3";
    public bool HasWorkflow1 => GetWorkflow(0) != null;
    public bool HasWorkflow2 => GetWorkflow(1) != null;
    public bool HasWorkflow3 => GetWorkflow(2) != null;

    // Workflow IDs for execution
    public string? Workflow1Id => GetWorkflow(0)?.Id;
    public string? Workflow2Id => GetWorkflow(1)?.Id;
    public string? Workflow3Id => GetWorkflow(2)?.Id;

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
        Workflow1Command = new RelayCommand(() => ExecuteWorkflowByIndex(0));
        Workflow2Command = new RelayCommand(() => ExecuteWorkflowByIndex(1));
        Workflow3Command = new RelayCommand(() => ExecuteWorkflowByIndex(2));
        OpenMainWindowCommand = new RelayCommand(OpenMainWindow);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        ExitCommand = new RelayCommand(Exit);
        TrayClickCommand = new RelayCommand(OnTrayClick);

        // Initialize from settings
        _showTray = SettingManager.Settings.ShowTray;

        // Subscribe to settings changes 
        SettingManager.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Update ShowTray when settings change
        ShowTray = SettingManager.Settings.ShowTray;
        // Notify workflow name changes in case workflow list changed
        OnPropertyChanged(nameof(Workflow1Name));
        OnPropertyChanged(nameof(Workflow2Name));
        OnPropertyChanged(nameof(Workflow3Name));
        OnPropertyChanged(nameof(HasWorkflow1));
        OnPropertyChanged(nameof(HasWorkflow2));
        OnPropertyChanged(nameof(HasWorkflow3));
    }

    /// <summary>
    /// Call this method to refresh the ShowTray property from settings.
    /// Useful when settings are changed externally.
    /// </summary>
    public void RefreshFromSettings()
    {
        ShowTray = SettingManager.Settings.ShowTray;
        OnPropertyChanged(nameof(Workflow1Name));
        OnPropertyChanged(nameof(Workflow2Name));
        OnPropertyChanged(nameof(Workflow3Name));
        OnPropertyChanged(nameof(HasWorkflow1));
        OnPropertyChanged(nameof(HasWorkflow2));
        OnPropertyChanged(nameof(HasWorkflow3));
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Get workflow by list index (0-based)
    /// </summary>
    private static WorkflowSettings? GetWorkflow(int index)
    {
        var workflows = SettingManager.WorkflowsConfig?.Hotkeys;
        if (workflows != null && index >= 0 && index < workflows.Count)
        {
            return workflows[index];
        }
        return null;
    }

    /// <summary>
    /// Get workflow display name by list index
    /// </summary>
    private static string? GetWorkflowName(int index)
    {
        var workflow = GetWorkflow(index);
        if (workflow == null) return null;

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

    /// <summary>
    /// Execute workflow by list index using its ID
    /// </summary>
    private async void ExecuteWorkflowByIndex(int index)
    {
        var workflow = GetWorkflow(index);
        if (workflow != null)
        {
            DebugHelper.WriteLine($"Tray: Execute workflow {index} (ID: {workflow.Id}): {workflow}");
            await Core.Helpers.TaskHelpers.ExecuteWorkflow(workflow, workflow.Id);
        }
        else
        {
            DebugHelper.WriteLine($"Tray: No workflow at index {index}");
        }
    }

    private void OpenMainWindow()
    {
        DebugHelper.WriteLine("Tray: Open Main Window");
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow?.Show();
            desktop.MainWindow?.Activate();
        }
    }

    private void OpenSettings()
    {
        DebugHelper.WriteLine("Tray: Open Settings");
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is Views.MainWindow mainWindow)
        {
            mainWindow.Show();
            mainWindow.Activate();
            mainWindow.NavigateToSettings();
        }
    }

    private void Exit()
    {
        DebugHelper.WriteLine("Tray: Exit");
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void OnTrayClick()
    {
        // Execute the configured left-click action
        var action = SettingManager.Settings.TrayLeftClickAction;
        DebugHelper.WriteLine($"Tray click: {action}");
        ExecuteTrayAction(action);
    }

    public void OnTrayDoubleClick()
    {
        var action = SettingManager.Settings.TrayLeftDoubleClickAction;
        DebugHelper.WriteLine($"Tray double click: {action}");
        ExecuteTrayAction(action);
    }

    public void OnTrayMiddleClick()
    {
        var action = SettingManager.Settings.TrayMiddleClickAction;
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
                var workflow = SettingManager.WorkflowsConfig?.Hotkeys?.FirstOrDefault(w => w.Job == action);
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

