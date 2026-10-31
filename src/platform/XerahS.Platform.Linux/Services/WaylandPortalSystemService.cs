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

    You should have received a copy of the GNU General Public
    License along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Tmds.DBus;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture;

namespace XerahS.Platform.Linux.Services;

public sealed class WaylandPortalSystemService : ISystemService, IDisposable
{
    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");

    private readonly LinuxSystemService _fallback = new();
    private readonly bool _allowNativeFallback;
    private Connection? _connection;
    private IOpenUriPortal? _portal;
    private bool _disposed;

    public WaylandPortalSystemService(bool allowNativeFallback = true)
    {
        _allowNativeFallback = allowNativeFallback;

        if (!allowNativeFallback || LinuxRuntimeEnvironment.Detect().RequiresPortalServices)
        {
            InitializePortal();
        }
    }

    private void InitializePortal()
    {
        if (!PortalInterfaceChecker.HasInterface("org.freedesktop.portal.OpenURI"))
        {
            return;
        }

        try
        {
            _connection = new Connection(Address.Session);
            var connectionInfo = _connection.ConnectAsync().GetAwaiter().GetResult();
            global::XerahS.Platform.Linux.Capture.PortalRequestExtensions.CacheLocalConnectionName(_connection, connectionInfo);
            _portal = _connection.CreateProxy<IOpenUriPortal>(PortalBusName, PortalObjectPath);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalSystemService: Unable to initialize OpenURI portal");
            Dispose();
        }
    }

    public bool IsDesktopWallpaperSupported => _allowNativeFallback && _fallback.IsDesktopWallpaperSupported;

    public bool ShowFileInExplorer(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        if (_portal != null)
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (TryPortalRequest(options => _portal.OpenDirectoryAsync(string.Empty, stream.SafeFileHandle, options)))
            {
                return true;
            }
        }

        return _allowNativeFallback && _fallback.ShowFileInExplorer(filePath);
    }

    public bool OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        if (_portal != null)
        {
            if (TryPortalRequest(options => _portal.OpenURIAsync(string.Empty, url, options)))
            {
                return true;
            }
        }

        return _allowNativeFallback && _fallback.OpenUrl(url);
    }

    public bool OpenFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
        {
            return false;
        }

        if (Directory.Exists(filePath))
        {
            if (_portal != null)
            {
                string folderUri = CreateDirectoryUri(filePath);
                if (TryPortalRequest(options => _portal.OpenURIAsync(string.Empty, folderUri, options)))
                {
                    return true;
                }
            }

            return _allowNativeFallback && _fallback.OpenFile(filePath);
        }

        if (_portal != null)
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (TryPortalRequest(options => _portal.OpenFileAsync(string.Empty, stream.SafeFileHandle, options)))
            {
                return true;
            }
        }

        return _allowNativeFallback && _fallback.OpenFile(filePath);
    }

    public bool TryGetDesktopWallpaperPath(out string? path)
    {
        if (_allowNativeFallback)
        {
            return _fallback.TryGetDesktopWallpaperPath(out path);
        }

        path = null;
        return false;
    }

    public bool TryGetDesktopWallpaper(out DesktopWallpaperInfo? wallpaper)
    {
        if (_allowNativeFallback)
        {
            return _fallback.TryGetDesktopWallpaper(out wallpaper);
        }

        wallpaper = null;
        return false;
    }

    internal static string CreateDirectoryUri(string directoryPath)
    {
        string fullPath = Path.GetFullPath(directoryPath);
        return new Uri(fullPath, UriKind.Absolute).AbsoluteUri;
    }

    private bool TryPortalRequest(Func<IDictionary<string, object>, Task<ObjectPath>> requestFactory)
    {
        if (_connection == null)
        {
            return false;
        }

        try
        {
            var options = new Dictionary<string, object>();
            var (response, _) = _connection
                .SendPortalRequestAsync(
                    PortalBusName,
                    options,
                    () => requestFactory(options))
                .GetAwaiter()
                .GetResult();
            return response == 0;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalSystemService: Portal request failed");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _portal = null;
        _connection?.Dispose();
        _connection = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    }

[DBusInterface("org.freedesktop.portal.OpenURI")]
public interface IOpenUriPortal : IDBusObject
{
    Task<ObjectPath> OpenURIAsync(string parentWindow, string uri, IDictionary<string, object> options);
    Task<ObjectPath> OpenFileAsync(string parentWindow, SafeFileHandle fd, IDictionary<string, object> options);
    Task<ObjectPath> OpenDirectoryAsync(string parentWindow, SafeFileHandle fd, IDictionary<string, object> options);
}
