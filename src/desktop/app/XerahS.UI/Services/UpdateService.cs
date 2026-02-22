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
using Avalonia.Threading;
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

/// <summary>
/// Singleton service that coordinates the auto-update flow.
/// </summary>
public class UpdateService : IDisposable
{
    private static UpdateService? _instance;
    private static readonly object _lock = new();

    public static UpdateService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new UpdateService();
                }
            }
            return _instance;
        }
    }

    private GitHubUpdateManager? _updateManager;
    private bool _disposed;

    public bool IsUpdateDialogOpen { get; private set; }

    private UpdateService()
    {
    }

    /// <summary>
    /// Initialize the update service and start periodic update checks if enabled.
    /// </summary>
    public void Initialize()
    {
        if (_updateManager != null)
        {
            DebugHelper.WriteLine("UpdateService already initialized.");
            return;
        }

        var settings = SettingsManager.Settings;

        _updateManager = new GitHubUpdateManager("ShareX", "XerahS")
        {
            IsPortable = IsPortableBuild(),
            CheckPreReleaseUpdates = settings.CheckPreReleaseUpdates ||
                                     settings.UpdateChannel == UpdateChannel.PreRelease ||
                                     settings.UpdateChannel == UpdateChannel.Dev,
            AllowAutoUpdate = settings.AutoCheckUpdate
        };

        // Wire up the callback for showing the update dialog
        _updateManager.ShowUpdateDialogCallback = ShowUpdateDialogAsync;

        if (settings.AutoCheckUpdate)
        {
            _updateManager.ConfigureAutoUpdate();
            DebugHelper.WriteLine("UpdateService: Auto-update enabled and configured.");
        }
        else
        {
            DebugHelper.WriteLine("UpdateService: Auto-update is disabled.");
        }
    }

    /// <summary>
    /// Shows the update dialog to the user when an update is available.
    /// </summary>
    /// <param name="updateChecker">The update checker with version information.</param>
    /// <returns>True if user accepted the update, false otherwise.</returns>
    public async Task<bool> ShowUpdateDialogAsync(UpdateChecker updateChecker)
    {
        if (IsUpdateDialogOpen)
        {
            DebugHelper.WriteLine("Update dialog is already open.");
            return false;
        }

        if (updateChecker.Status != UpdateStatus.UpdateAvailable)
        {
            return false;
        }

        IsUpdateDialogOpen = true;

        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var owner = GetMainWindow();
                if (owner == null)
                {
                    DebugHelper.WriteLine("Cannot show update dialog: Main window not available.");
                    return false;
                }

                var dialog = new UpdateMessageBox(updateChecker);
                var result = await dialog.ShowDialog<bool?>(owner);

                if (result == true)
                {
                    await HandleUpdateAcceptedAsync(updateChecker);
                    return true;
                }
                else
                {
                    // User clicked No - disable auto-update for this session
                    if (_updateManager != null)
                    {
                        _updateManager.AutoUpdateEnabled = false;
                    }
                    DebugHelper.WriteLine("User declined update. Auto-update disabled until restart.");
                    return false;
                }
            });
        }
        finally
        {
            IsUpdateDialogOpen = false;
        }
    }

    private async Task HandleUpdateAcceptedAsync(UpdateChecker updateChecker)
    {
        if (updateChecker.IsPortable)
        {
            // For portable builds, open the download URL in browser
            if (!string.IsNullOrEmpty(updateChecker.DownloadURL))
            {
                URLHelpers.OpenURL(updateChecker.DownloadURL);
                DebugHelper.WriteLine($"Portable build: Opened download URL in browser: {updateChecker.DownloadURL}");
            }
        }
        else
        {
            // For installer builds, show the downloader window
            await ShowDownloaderWindowAsync(updateChecker);
        }
    }

    private async Task ShowDownloaderWindowAsync(UpdateChecker updateChecker)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = GetMainWindow();
            if (owner == null)
            {
                DebugHelper.WriteLine("Cannot show downloader: Main window not available.");
                // Fallback to opening URL in browser
                if (!string.IsNullOrEmpty(updateChecker.DownloadURL))
                {
                    URLHelpers.OpenURL(updateChecker.DownloadURL);
                }
                return;
            }

            var dialog = new DownloaderWindow(updateChecker);
            var result = await dialog.ShowDialog<bool?>(owner);

            if (result == true)
            {
                // Installer was launched successfully - shut down the application
                DebugHelper.WriteLine("Installer launched. Shutting down application...");
                ShutdownApplication();
            }
        });
    }

    private static void ShutdownApplication()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            App.IsExiting = true;
            desktop.Shutdown();
        }
    }

    private static Window? GetMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    private static bool IsPortableBuild()
    {
        // Check for portable marker file
        var portableMarker = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.txt");
        return File.Exists(portableMarker);
    }

    /// <summary>
    /// Manually trigger an update check.
    /// </summary>
    public async Task CheckForUpdatesAsync()
    {
        if (_updateManager == null)
        {
            DebugHelper.WriteLine("UpdateService not initialized. Call Initialize() first.");
            return;
        }

        var updateChecker = _updateManager.CreateUpdateChecker();
        await updateChecker.CheckUpdateAsync();

        if (updateChecker.Status == UpdateStatus.UpdateAvailable)
        {
            await ShowUpdateDialogAsync(updateChecker);
        }
        else if (updateChecker.Status == UpdateStatus.UpToDate)
        {
            DebugHelper.WriteLine($"Application is up to date. Current version: {updateChecker.CurrentVersion}");
        }
        else
        {
            DebugHelper.WriteLine($"Update check failed. Status: {updateChecker.Status}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _updateManager?.Dispose();
            _updateManager = null;
            _disposed = true;
        }
    }
}
