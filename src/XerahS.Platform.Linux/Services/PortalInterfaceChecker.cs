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
using System.Diagnostics;

namespace XerahS.Platform.Linux.Services;

internal static class PortalInterfaceChecker
{
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
            XerahS.Common.DebugHelper.WriteLine($"PortalInterfaceChecker: Checking for interface '{interfaceName}'...");

            // Use busctl to check for the interface - more reliable than Tmds.DBus proxy generation
            var startInfo = new ProcessStartInfo
            {
                FileName = "busctl",
                Arguments = "--user introspect org.freedesktop.portal.Desktop /org/freedesktop/portal/desktop",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                XerahS.Common.DebugHelper.WriteLine("PortalInterfaceChecker: Failed to start busctl process");
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0)
            {
                XerahS.Common.DebugHelper.WriteLine($"PortalInterfaceChecker: busctl exited with code {process.ExitCode}");
                return false;
            }

            // busctl output format: "org.freedesktop.portal.ScreenCast          interface -"
            bool found = output.Contains(interfaceName, StringComparison.Ordinal);
            XerahS.Common.DebugHelper.WriteLine($"PortalInterfaceChecker: Interface '{interfaceName}' found={found}");

            return found;
        }
        catch (Exception ex)
        {
            XerahS.Common.DebugHelper.WriteLine($"PortalInterfaceChecker: Exception checking '{interfaceName}': {ex.Message}");
            return false;
        }
    }
}
