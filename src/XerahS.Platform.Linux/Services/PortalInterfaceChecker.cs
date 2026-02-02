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
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Tmds.DBus;

namespace XerahS.Platform.Linux.Services;

internal static class PortalInterfaceChecker
{
    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");
    private static readonly ConcurrentDictionary<string, bool> Cache = new(StringComparer.Ordinal);

    public static bool HasInterface(string interfaceName)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            return false;
        }

        return Cache.GetOrAdd(interfaceName, _ => CheckInterface(interfaceName));
    }

    private static bool CheckInterface(string interfaceName)
    {
        try
        {
            using var connection = new Connection(Address.Session);
            connection.ConnectAsync().GetAwaiter().GetResult();
            var proxy = connection.CreateProxy<IIntrospectable>(PortalBusName, PortalObjectPath);
            var xml = proxy.IntrospectAsync().GetAwaiter().GetResult();
            return xml?.Contains($"interface name=\"{interfaceName}\"", StringComparison.Ordinal) ?? false;
        }
        catch
        {
            return false;
        }
    }

    [DBusInterface("org.freedesktop.DBus.Introspectable")]
    private interface IIntrospectable : IDBusObject
    {
        Task<string> IntrospectAsync();
    }
}
