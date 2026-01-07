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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using ShareX.Ava.Common;
using ShareX.Ava.Core;

namespace ShareX.Ava.UI;

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

    public ICommand CaptureRegionCommand { get; }
    public ICommand CaptureScreenCommand { get; }
    public ICommand CaptureWindowCommand { get; }
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
        CaptureRegionCommand = new RelayCommand(CaptureRegion);
        CaptureScreenCommand = new RelayCommand(CaptureScreen);
        CaptureWindowCommand = new RelayCommand(CaptureWindow);
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
    }

    /// <summary>
    /// Call this method to refresh the ShowTray property from settings.
    /// Useful when settings are changed externally.
    /// </summary>
    public void RefreshFromSettings()
    {
        ShowTray = SettingManager.Settings.ShowTray;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void CaptureRegion()
    {
        DebugHelper.WriteLine("Tray: Capture Region");
        await Core.Helpers.TaskHelpers.ExecuteJob(HotkeyType.RectangleRegion);
    }

    private async void CaptureScreen()
    {
        DebugHelper.WriteLine("Tray: Capture Screen");
        await Core.Helpers.TaskHelpers.ExecuteJob(HotkeyType.PrintScreen);
    }

    private async void CaptureWindow()
    {
        DebugHelper.WriteLine("Tray: Capture Window");
        await Core.Helpers.TaskHelpers.ExecuteJob(HotkeyType.ActiveWindow);
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
            case HotkeyType.RectangleRegion:
            case HotkeyType.PrintScreen:
            case HotkeyType.ActiveWindow:
            case HotkeyType.LastRegion:
                await Core.Helpers.TaskHelpers.ExecuteJob(action);
                break;
            default:
                await Core.Helpers.TaskHelpers.ExecuteJob(action);
                break;
        }
    }
}
