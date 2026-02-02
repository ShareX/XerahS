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
    private Connection? _connection;
    private IOpenUriPortal? _portal;
    private bool _disposed;

    public WaylandPortalSystemService()
    {
        if (!WaylandPortalStrategy.IsSupported())
        {
            return;
        }

        try
        {
            _connection = new Connection(Address.Session);
            _connection.ConnectAsync().GetAwaiter().GetResult();
            _portal = _connection.CreateProxy<IOpenUriPortal>(PortalBusName, PortalObjectPath);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "WaylandPortalSystemService: Unable to initialize OpenURI portal");
            Dispose();
        }
    }

    public bool ShowFileInExplorer(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        if (_portal != null)
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (TryPortalRequest(() => _portal.OpenDirectoryAsync(string.Empty, stream.SafeFileHandle, new Dictionary<string, object>())))
            {
                return true;
            }
        }

        return _fallback.ShowFileInExplorer(filePath);
    }

    public bool OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        if (_portal != null)
        {
            if (TryPortalRequest(() => _portal.OpenURIAsync(string.Empty, url, new Dictionary<string, object>())))
            {
                return true;
            }
        }

        return _fallback.OpenUrl(url);
    }

    public bool OpenFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        if (_portal != null)
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (TryPortalRequest(() => _portal.OpenFileAsync(string.Empty, stream.SafeFileHandle, new Dictionary<string, object>())))
            {
                return true;
            }
        }

        return _fallback.OpenFile(filePath);
    }

    private bool TryPortalRequest(Func<Task<ObjectPath>> requestFactory)
    {
        if (_connection == null)
        {
            return false;
        }

        try
        {
            var requestPath = requestFactory().GetAwaiter().GetResult();
            var request = _connection.CreateProxy<IPortalRequest>(PortalBusName, requestPath);
            var (response, _) = request.WaitForResponseAsync().GetAwaiter().GetResult();
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

    [DBusInterface("org.freedesktop.portal.OpenURI")]
    internal interface IOpenUriPortal : IDBusObject
    {
        Task<ObjectPath> OpenURIAsync(string parentWindow, string uri, IDictionary<string, object> options);
        Task<ObjectPath> OpenFileAsync(string parentWindow, SafeFileHandle fd, IDictionary<string, object> options);
        Task<ObjectPath> OpenDirectoryAsync(string parentWindow, SafeFileHandle fd, IDictionary<string, object> options);
    }
}
