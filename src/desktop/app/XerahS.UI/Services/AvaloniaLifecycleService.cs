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
using XerahS.Services.Abstractions;

namespace XerahS.UI.Services;

/// <summary>
/// Avalonia-backed implementation of <see cref="ILifecycleService"/>.
/// Keeps UI framework references out of ViewModels and core services.
/// </summary>
public sealed class AvaloniaLifecycleService : ILifecycleService
{
    private volatile bool _isShuttingDown;

    public bool IsShuttingDown => _isShuttingDown;

    public void ShowMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is not Window mainWindow)
        {
            return;
        }

        // A SilentRun start hides the window and clears ShowInTaskbar; a plain Show() would bring it
        // back without a taskbar button, and the next minimize would leave no way to restore it.
        // macOS keeps it hidden there: SilentRun is the menu-bar-only mode.
        if (!OperatingSystem.IsMacOS() || !XerahS.Core.SettingsManager.Settings.SilentRun)
        {
            mainWindow.ShowInTaskbar = true;
        }

        mainWindow.ShowActivated = true;

        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Show();
        mainWindow.Activate();
    }

    public void RequestShutdown()
    {
        _isShuttingDown = true;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
