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
using System.IO;
using System.Threading.Tasks;
using Tmds.DBus;
using XerahS.Common;

namespace XerahS.Platform.Linux.Services;

internal static class PortalInterfaceChecker
{
    private const string PortalBusName = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath PortalObjectPath = new("/org/freedesktop/portal/desktop");
    private static readonly ConcurrentDictionary<string, bool> Cache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, PortalProbeResult> ProbeResults = new(StringComparer.Ordinal);

    public static bool HasInterface(string interfaceName)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            return false;
        }

        return Cache.GetOrAdd(interfaceName, _ => CheckInterface(interfaceName));
    }

    public static string GetDiagnosticSummary(string interfaceName)
    {
        if (ProbeResults.TryGetValue(interfaceName, out var probe))
        {
            return $"found={probe.Found}, source={probe.Source}, detail={probe.Detail}";
        }

        return "not-probed";
    }

    private static bool CheckInterface(string interfaceName)
    {
        DebugHelper.WriteLine($"PortalInterfaceChecker: Checking for interface '{interfaceName}'...");

        if (TryCheckInterfaceWithDbusIntrospection(interfaceName, out bool foundWithDbus, out string dbusDetail))
        {
            RecordProbe(interfaceName, foundWithDbus, "dbus-introspect", dbusDetail);
            DebugHelper.WriteLine($"PortalInterfaceChecker: Interface '{interfaceName}' found={foundWithDbus} (source=dbus-introspect)");
            return foundWithDbus;
        }

        DebugHelper.WriteLine($"PortalInterfaceChecker: D-Bus introspection failed for '{interfaceName}'. Falling back to busctl. Details: {dbusDetail}");
        bool foundWithBusctl = CheckInterfaceWithBusctl(interfaceName, out string busctlDetail);
        RecordProbe(interfaceName, foundWithBusctl, "busctl-fallback", $"dbus={dbusDetail}; busctl={busctlDetail}");
        DebugHelper.WriteLine($"PortalInterfaceChecker: Interface '{interfaceName}' found={foundWithBusctl} (source=busctl-fallback)");
        return foundWithBusctl;
    }

    private static bool TryCheckInterfaceWithDbusIntrospection(string interfaceName, out bool found, out string detail)
    {
        found = false;
        detail = string.Empty;

        try
        {
            using var connection = new Connection(Address.Session);
            connection.ConnectAsync().GetAwaiter().GetResult();
            var introspectable = connection.CreateProxy<IIntrospectable>(PortalBusName, PortalObjectPath);
            string xml = introspectable.IntrospectAsync().GetAwaiter().GetResult();

            if (string.IsNullOrWhiteSpace(xml))
            {
                detail = "introspection returned empty XML";
                return false;
            }

            found = xml.Contains($"interface name=\"{interfaceName}\"", StringComparison.Ordinal);
            if (!found)
            {
                found = xml.Contains(interfaceName, StringComparison.Ordinal);
            }

            detail = found ? "interface found in Introspect XML" : "interface not found in Introspect XML";
            return true;
        }
        catch (Exception ex)
        {
            detail = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static bool CheckInterfaceWithBusctl(string interfaceName, out string detail)
    {
        detail = string.Empty;
        string command = "busctl --user introspect org.freedesktop.portal.Desktop /org/freedesktop/portal/desktop";
        string cwd = Environment.CurrentDirectory;
        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string resolvedBusctl = ResolveCommandPath("busctl") ?? "<not found>";

        DebugHelper.WriteLine(
            $"PortalInterfaceChecker: busctl fallback startup: command=\"{command}\", cwd=\"{cwd}\", " +
            $"pathSet={!string.IsNullOrWhiteSpace(pathEnv)}, resolved=\"{resolvedBusctl}\"");

        try
        {
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
                detail = "failed to start busctl process";
                DebugHelper.WriteLine($"PortalInterfaceChecker: {detail}");
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            bool exited = process.WaitForExit(5000);
            if (!exited)
            {
                try { process.Kill(); } catch { }
                detail = "busctl timed out after 5000ms";
                DebugHelper.WriteLine($"PortalInterfaceChecker: {detail}");
                return false;
            }

            DebugHelper.WriteLine($"PortalInterfaceChecker: busctl fallback exitCode={process.ExitCode}");
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                DebugHelper.WriteLine($"PortalInterfaceChecker: busctl fallback stderr: {stderr.Trim()}");
            }

            if (process.ExitCode != 0)
            {
                detail = $"busctl exited with code {process.ExitCode}";
                return false;
            }

            bool found = output.Contains(interfaceName, StringComparison.Ordinal);
            detail = found ? "interface found in busctl output" : "interface not found in busctl output";
            return found;
        }
        catch (Exception ex)
        {
            detail = $"{ex.GetType().Name}: {ex.Message}";
            DebugHelper.WriteLine($"PortalInterfaceChecker: busctl fallback exception ({ex.GetType().Name}): {ex.Message}");
            return false;
        }
    }

    private static string? ResolveCommandPath(string command)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (string segment in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(segment, command);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Best effort diagnostics.
            }
        }

        return null;
    }

    private static void RecordProbe(string interfaceName, bool found, string source, string detail)
    {
        ProbeResults[interfaceName] = new PortalProbeResult(found, source, detail);
    }

    private readonly record struct PortalProbeResult(bool Found, string Source, string Detail);
}

[DBusInterface("org.freedesktop.DBus.Introspectable")]
public interface IIntrospectable : IDBusObject
{
    Task<string> IntrospectAsync();
}
