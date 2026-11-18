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
using System;
using System.IO;
using System.Linq;
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.Views;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Services;

/// <summary>
/// Singleton service that coordinates the auto-update flow.
/// </summary>
public class UpdateService : IDisposable
{
    private static UpdateService? _instance;
    private static readonly Lock _lock = new();
    private const string DefaultReleaseOwner = "ShareX";
    private const string DefaultPreReleaseOwner = "KovaForge";
    private const string DefaultRepo = "XerahS";

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

    /// <summary>
    /// True when this process runs inside a Flatpak sandbox. In that case the
    /// Flatpak runtime owns upgrade notifications and delivery (via
    /// <c>flatpak update</c>), so the in-app GitHub updater must not offer
    /// <c>.deb</c> / <c>.rpm</c> assets that the sandbox cannot install.
    /// Detection mirrors <see cref="XerahS.Common.DebugHelper"/> and the check
    /// already used in <c>XerahS.App/Program.cs</c>: prefer <c>FLATPAK_ID</c>,
    /// fall back to the well-known marker file inside the Flatpak rootfs.
    /// </summary>
    public static bool IsRuntimeManagedByFlatpak =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID"))
        || File.Exists("/.flatpak-info");

    /// <summary>
    /// Human-readable message for users who click "Check for updates" from
    /// inside a Flatpak. Tells them to use the system update channel.
    /// </summary>
    public static string RuntimeManagedUpdateMessage =>
        "Updates are managed by the Flatpak runtime. " +
        "Run 'flatpak update com.xerahs.XerahS' in a terminal to upgrade.";

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
            RefreshConfigurationFromSettings();
            return;
        }

        if (IsRuntimeManagedByFlatpak)
        {
            DebugHelper.WriteLine(
                "UpdateService: Skipping GitHub update manager inside Flatpak sandbox " +
                "(FLATPAK_ID='{0}'); upgrades are delivered by 'flatpak update'.",
                Environment.GetEnvironmentVariable("FLATPAK_ID") ?? "<unset>");
            return;
        }

        var settings = SettingsManager.Settings;
        bool includePreRelease = settings.UpdateChannel == UpdateChannel.PreRelease;
        IReadOnlyList<(string Owner, string Repo)> updateRepositories = ResolveUpdateRepositories(settings);
        var updateRepository = updateRepositories[0];

        _updateManager = new GitHubUpdateManager(updateRepository.Owner, updateRepository.Repo)
        {
            GitHubRepositories = updateRepositories,
            IsPortable = PathsManager.IsPortable,
            IncludePreRelease = includePreRelease,
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

    public void RefreshConfigurationFromSettings()
    {
        if (_updateManager == null)
        {
            // Either initialization has not happened yet, or it was suppressed
            // by the Flatpak runtime (see Initialize). Nothing to refresh.
            return;
        }

        var settings = SettingsManager.Settings;
        IReadOnlyList<(string Owner, string Repo)> updateRepositories = ResolveUpdateRepositories(settings);
        var updateRepository = updateRepositories[0];
        bool includePreRelease = settings.UpdateChannel == UpdateChannel.PreRelease;

        _updateManager.GitHubRepositories = updateRepositories;
        _updateManager.GitHubOwner = updateRepository.Owner;
        _updateManager.GitHubRepo = updateRepository.Repo;
        _updateManager.IncludePreRelease = includePreRelease;
        _updateManager.AllowAutoUpdate = settings.AutoCheckUpdate;
        _updateManager.ConfigureAutoUpdate();
    }

    public static (string Owner, string Repo) ResolveUpdateRepository(ApplicationConfig settings)
    {
        return ResolveUpdateRepositories(settings)[0];
    }

    public static IReadOnlyList<(string Owner, string Repo)> ResolveUpdateRepositories(ApplicationConfig settings)
    {
        if (settings.UpdateChannel != UpdateChannel.PreRelease)
        {
            return [(DefaultReleaseOwner, DefaultRepo)];
        }

        return settings.PreReleaseUpdateSource switch
        {
            PreReleaseUpdateSource.ShareX => [(DefaultReleaseOwner, DefaultRepo)],
            PreReleaseUpdateSource.Custom => [ResolveCustomPreReleaseRepository(settings.CustomPreReleaseUpdateSource)],
            PreReleaseUpdateSource.Any =>
            [
                (DefaultReleaseOwner, DefaultRepo),
                (DefaultPreReleaseOwner, DefaultRepo)
            ],
            _ => [(DefaultPreReleaseOwner, DefaultRepo)]
        };
    }

    public static (string Owner, string Repo) ResolveCustomPreReleaseRepository(string? source)
    {
        string normalized = NormalizeCustomPreReleaseSource(source);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return (DefaultPreReleaseOwner, DefaultRepo);
        }

        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2
            ? (parts[0], parts[1])
            : (parts[0], DefaultRepo);
    }

    private static string NormalizeCustomPreReleaseSource(string? source)
    {
        string normalized = source?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri) &&
            uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            normalized = uri.AbsolutePath;
        }

        return normalized.Trim().Trim('/');
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
                bool? result;
                try
                {
                    var viewModel = UpdateMessageBox.CreateViewModel(updateChecker);
                    result = await ModalDialogHost.ShowAsync(
                        viewModel,
                        set => viewModel.RequestClose = r => set(r == true),
                        dismissResult: false,
                        debugSource: nameof(UpdateMessageBoxViewModel));
                }
                catch (InvalidOperationException ex)
                {
                    DebugHelper.WriteException(ex, "Failed to show update dialog");
                    return true;
                }

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
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Update dialog flow failed");
            return true;
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
            var dialog = new DownloaderWindow(updateChecker);
            bool? result;
            try
            {
                result = await ShowDownloaderWindowAsync(dialog, updateChecker);
            }
            catch (InvalidOperationException ex)
            {
                DebugHelper.WriteException(ex, "Failed to show downloader window");
                if (!string.IsNullOrEmpty(updateChecker.DownloadURL))
                {
                    URLHelpers.OpenURL(updateChecker.DownloadURL);
                }
                return;
            }

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

    private static Window? GetPreferredDialogOwner()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        return desktop.Windows.FirstOrDefault(window => CanUseDialogOwner(window) && window.IsActive)
            ?? desktop.Windows.LastOrDefault(CanUseDialogOwner)
            ?? desktop.MainWindow;
    }

    private static bool CanUseDialogOwner(Window? owner)
    {
        return owner != null &&
               owner.IsVisible &&
               owner.WindowState != Avalonia.Controls.WindowState.Minimized;
    }

    private static async Task<bool?> ShowDownloaderWindowAsync(DownloaderWindow dialog, UpdateChecker updateChecker)
    {
        // Progress UI stays a top-level window (non-modal Show). No Window.ShowDialog.
        DebugHelper.WriteLine("Showing updater downloader as non-modal window.");
        return await dialog.ShowDetachedAsync();
    }

    /// <summary>
    /// Manually trigger an update check.
    /// </summary>
    public async Task<UpdateStatus> CheckForUpdatesAsync()
    {
        if (_updateManager == null)
        {
            if (IsRuntimeManagedByFlatpak)
            {
                DebugHelper.WriteLine(
                    "UpdateService: CheckForUpdatesAsync short-circuited inside Flatpak sandbox; " +
                    "the Flatpak runtime owns upgrade delivery.");
            }
            else
            {
                DebugHelper.WriteLine("UpdateService not initialized. Call Initialize() first.");
            }
            return UpdateStatus.UpdateCheckFailed;
        }

        RefreshConfigurationFromSettings();

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

        return updateChecker.Status;
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
