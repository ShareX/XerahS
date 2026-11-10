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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;
using XerahS.Common;

namespace XerahS.Platform.Linux.Capture.Portal;

[DBusInterface("org.freedesktop.host.portal.Registry")]
public interface IPortalHostRegistry : IDBusObject
{
    Task RegisterAsync(string appId, IDictionary<string, object> options);
}

/// <summary>
/// Associates an unsandboxed process's portal D-Bus connection with a stable application id via
/// org.freedesktop.host.portal.Registry. Without it, an unsandboxed app gets its portal id from the
/// systemd unit name or nothing, so xdg-desktop-portal-kde cannot store the Screenshot permission and
/// asks every time. Must be called once per connection, before any other portal call on it.
/// Not available to sandboxed apps or on xdg-desktop-portal older than 1.19, so failure is non-fatal.
/// </summary>
internal static class PortalHostRegistry
{
    /// <summary>Matches the installed xerahs.desktop and the Wayland app_id set in App.axaml.cs.</summary>
    public const string AppId = "xerahs";

    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");

    public static async Task<bool> TryRegisterAsync(Connection connection, bool isSandboxed, string logPrefix)
    {
        if (isSandboxed)
        {
            return false;
        }

        try
        {
            var registry = connection.CreateProxy<IPortalHostRegistry>(PortalBusName, PortalObjectPath);
            await registry.RegisterAsync(AppId, new Dictionary<string, object>()).ConfigureAwait(false);
            DebugHelper.WriteLine($"{logPrefix}: Registered portal app id '{AppId}'.");
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"{logPrefix}: Portal host registry unavailable ({ex.GetType().Name}: {ex.Message}); continuing without an app id.");
            return false;
        }
    }
}
