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

namespace XerahS.Platform.Abstractions;

public enum WatchFolderDaemonScope
{
    User,
    System
}

public enum WatchFolderDaemonState
{
    Stopped,
    Running,
    NotInstalled,
    Unknown
}

public enum WatchFolderDaemonErrorCode
{
    None,
    RequiresElevation,
    UnsupportedScope,
    CommandFailed,
    ValidationError
}

public sealed class WatchFolderDaemonStatus
{
    public WatchFolderDaemonScope Scope { get; set; } = WatchFolderDaemonScope.User;
    public WatchFolderDaemonState State { get; set; } = WatchFolderDaemonState.Unknown;
    public bool Installed { get; set; }
    public bool StartAtStartup { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class WatchFolderDaemonResult
{
    public bool Success { get; set; }
    public WatchFolderDaemonErrorCode ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;

    public static WatchFolderDaemonResult Ok(string message = "")
    {
        return new WatchFolderDaemonResult
        {
            Success = true,
            ErrorCode = WatchFolderDaemonErrorCode.None,
            Message = message
        };
    }

    public static WatchFolderDaemonResult Fail(WatchFolderDaemonErrorCode errorCode, string message)
    {
        return new WatchFolderDaemonResult
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        };
    }
}

public interface IWatchFolderDaemonService
{
    bool IsSupported { get; }

    bool SupportsScope(WatchFolderDaemonScope scope);

    Task<WatchFolderDaemonStatus> GetStatusAsync(WatchFolderDaemonScope scope, CancellationToken cancellationToken = default);

    Task<WatchFolderDaemonResult> StartAsync(
        WatchFolderDaemonScope scope,
        string settingsFolder,
        bool startAtStartup,
        CancellationToken cancellationToken = default);

    Task<WatchFolderDaemonResult> StopAsync(
        WatchFolderDaemonScope scope,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default);

    Task<WatchFolderDaemonResult> RestartAsync(
        WatchFolderDaemonScope scope,
        string settingsFolder,
        bool startAtStartup,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default);
}

public sealed class UnsupportedWatchFolderDaemonService : IWatchFolderDaemonService
{
    public bool IsSupported => false;

    public bool SupportsScope(WatchFolderDaemonScope scope) => false;

    public Task<WatchFolderDaemonStatus> GetStatusAsync(WatchFolderDaemonScope scope, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new WatchFolderDaemonStatus
        {
            Scope = scope,
            State = WatchFolderDaemonState.Unknown,
            Installed = false,
            StartAtStartup = false,
            Message = "Watch folder daemon is not supported on this platform."
        });
    }

    public Task<WatchFolderDaemonResult> StartAsync(
        WatchFolderDaemonScope scope,
        string settingsFolder,
        bool startAtStartup,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(WatchFolderDaemonResult.Fail(
            WatchFolderDaemonErrorCode.ValidationError,
            "Watch folder daemon is not supported on this platform."));
    }

    public Task<WatchFolderDaemonResult> StopAsync(
        WatchFolderDaemonScope scope,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(WatchFolderDaemonResult.Fail(
            WatchFolderDaemonErrorCode.ValidationError,
            "Watch folder daemon is not supported on this platform."));
    }

    public Task<WatchFolderDaemonResult> RestartAsync(
        WatchFolderDaemonScope scope,
        string settingsFolder,
        bool startAtStartup,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(WatchFolderDaemonResult.Fail(
            WatchFolderDaemonErrorCode.ValidationError,
            "Watch folder daemon is not supported on this platform."));
    }
}
