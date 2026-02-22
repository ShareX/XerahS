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

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Xml;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.MacOS.Services;

public sealed class MacOSWatchFolderDaemonService : WatchFolderDaemonServiceBase
{
    private const string Label = "com.getsharex.xerahs.watchfolder";

    public override bool IsSupported => true;

    public override bool SupportsScope(WatchFolderDaemonScope scope)
    {
        return scope == WatchFolderDaemonScope.User || scope == WatchFolderDaemonScope.System;
    }

    public override async Task<WatchFolderDaemonStatus> GetStatusAsync(WatchFolderDaemonScope scope, CancellationToken cancellationToken = default)
    {
        if (!SupportsScope(scope))
        {
            return new WatchFolderDaemonStatus
            {
                Scope = scope,
                State = WatchFolderDaemonState.Unknown,
                Installed = false,
                Message = "Unsupported daemon scope."
            };
        }

        string plistPath = GetPlistPath(scope);
        bool installed = File.Exists(plistPath);
        if (!installed)
        {
            return new WatchFolderDaemonStatus
            {
                Scope = scope,
                State = WatchFolderDaemonState.NotInstalled,
                Installed = false,
                StartAtStartup = false,
                Message = "launchd plist is not installed."
            };
        }

        string domainService = GetDomainService(scope);
        var printResult = await RunLaunchCtlAsync($"print {domainService}", cancellationToken);
        bool loaded = printResult.IsSuccess;
        bool running = loaded && printResult.Output.Contains("state = running", StringComparison.OrdinalIgnoreCase);

        return new WatchFolderDaemonStatus
        {
            Scope = scope,
            State = running ? WatchFolderDaemonState.Running : WatchFolderDaemonState.Stopped,
            Installed = true,
            StartAtStartup = ReadRunAtLoad(plistPath),
            Message = loaded ? (running ? "Daemon is running." : "Daemon is loaded but stopped.") : "Daemon is not loaded."
        };
    }

    public override async Task<WatchFolderDaemonResult> StartAsync(
        WatchFolderDaemonScope scope,
        string settingsFolder,
        bool startAtStartup,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsScope(scope))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.UnsupportedScope, "Unsupported daemon scope.");
        }

        if (string.IsNullOrWhiteSpace(settingsFolder))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.ValidationError, "Settings folder is required.");
        }

        string daemonPath = ResolveDaemonPath(
            new[] { "xerahs-watchfolder-daemon", "XerahS.WatchFolder.Daemon" },
            "xerahs-watchfolder-daemon");

        if (!File.Exists(daemonPath))
        {
            return WatchFolderDaemonResult.Fail(
                WatchFolderDaemonErrorCode.ValidationError,
                $"Daemon executable was not found: {daemonPath}");
        }

        if (scope == WatchFolderDaemonScope.System && !new MacOSPlatformInfo().IsElevated)
        {
            return await StartSystemScopeWithElevationAsync(daemonPath, settingsFolder, startAtStartup, cancellationToken);
        }

        var plistWriteResult = await EnsurePlistAsync(scope, daemonPath, settingsFolder, startAtStartup, cancellationToken);
        if (!plistWriteResult.Success)
        {
            return plistWriteResult;
        }

        string domain = GetDomain(scope);
        string domainService = GetDomainService(scope);
        string plistPath = GetPlistPath(scope);

        _ = await RunLaunchCtlAsync($"bootout {domainService}", cancellationToken);

        var bootstrapResult = await RunLaunchCtlAsync($"bootstrap {domain} \"{plistPath}\"", cancellationToken);
        if (!bootstrapResult.IsSuccess)
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, bootstrapResult.Output);
        }

        var kickstartResult = await RunLaunchCtlAsync($"kickstart -k {domainService}", cancellationToken);
        if (!kickstartResult.IsSuccess)
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, kickstartResult.Output);
        }

        return WatchFolderDaemonResult.Ok("Daemon started.");
    }

    public override async Task<WatchFolderDaemonResult> StopAsync(
        WatchFolderDaemonScope scope,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsScope(scope))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.UnsupportedScope, "Unsupported daemon scope.");
        }

        string plistPath = GetPlistPath(scope);
        if (!File.Exists(plistPath))
        {
            return WatchFolderDaemonResult.Ok("launchd plist is not installed.");
        }

        if (scope == WatchFolderDaemonScope.System && !new MacOSPlatformInfo().IsElevated)
        {
            return await StopSystemScopeWithElevationAsync(cancellationToken);
        }

        string domainService = GetDomainService(scope);
        var bootoutResult = await RunLaunchCtlAsync($"bootout {domainService}", cancellationToken);
        if (!bootoutResult.IsSuccess &&
            !bootoutResult.Output.Contains("Could not find service", StringComparison.OrdinalIgnoreCase))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, bootoutResult.Output);
        }

        var timeout = gracefulTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : gracefulTimeout;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            var printResult = await RunLaunchCtlAsync($"print {domainService}", cancellationToken);
            if (!printResult.IsSuccess)
            {
                return WatchFolderDaemonResult.Ok("Daemon stopped.");
            }

            if (!printResult.Output.Contains("state = running", StringComparison.OrdinalIgnoreCase))
            {
                return WatchFolderDaemonResult.Ok("Daemon stopped.");
            }

            await Task.Delay(DefaultPollIntervalMs, cancellationToken);
        }

        return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, "Daemon did not stop before timeout.");
    }

    private static string GetDomain(WatchFolderDaemonScope scope)
    {
        return scope == WatchFolderDaemonScope.System
            ? "system"
            : $"gui/{getuid()}";
    }

    private static string GetDomainService(WatchFolderDaemonScope scope)
    {
        return $"{GetDomain(scope)}/{Label}";
    }

    private static string GetPlistPath(WatchFolderDaemonScope scope)
    {
        return scope == WatchFolderDaemonScope.System
            ? Path.Combine("/Library/LaunchDaemons", $"{Label}.plist")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "LaunchAgents", $"{Label}.plist");
    }

    private static bool ReadRunAtLoad(string plistPath)
    {
        try
        {
            var document = new XmlDocument();
            document.Load(plistPath);
            var runAtLoadNode = document.SelectSingleNode("/plist/dict/key[text()='RunAtLoad']/following-sibling::*[1]");
            return runAtLoadNode?.Name.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<WatchFolderDaemonResult> EnsurePlistAsync(
        WatchFolderDaemonScope scope,
        string daemonPath,
        string settingsFolder,
        bool startAtStartup,
        CancellationToken cancellationToken)
    {
        try
        {
            string plistPath = GetPlistPath(scope);
            string? directory = Path.GetDirectoryName(plistPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string plistContent = BuildPlistContent(scope, daemonPath, settingsFolder, startAtStartup);
            await File.WriteAllTextAsync(plistPath, plistContent, cancellationToken);
            return WatchFolderDaemonResult.Ok();
        }
        catch (Exception ex)
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, ex.Message);
        }
    }

    private static async Task<WatchFolderDaemonResult> StartSystemScopeWithElevationAsync(
        string daemonPath,
        string settingsFolder,
        bool startAtStartup,
        CancellationToken cancellationToken)
    {
        string tempPlistPath = Path.GetTempFileName();
        try
        {
            string plistContent = BuildPlistContent(WatchFolderDaemonScope.System, daemonPath, settingsFolder, startAtStartup);
            await File.WriteAllTextAsync(tempPlistPath, plistContent, cancellationToken);

            string systemPlistPath = GetPlistPath(WatchFolderDaemonScope.System);
            string domainService = GetDomainService(WatchFolderDaemonScope.System);
            string script = $"""
                             set -e
                             cp '{EscapeShellSingleQuotedString(tempPlistPath)}' '{EscapeShellSingleQuotedString(systemPlistPath)}'
                             chmod 644 '{EscapeShellSingleQuotedString(systemPlistPath)}'
                             launchctl bootout '{EscapeShellSingleQuotedString(domainService)}' >/dev/null 2>&1 || true
                             launchctl bootstrap system '{EscapeShellSingleQuotedString(systemPlistPath)}'
                             launchctl kickstart -k '{EscapeShellSingleQuotedString(domainService)}'
                             """;

            CommandResult privilegedResult = await RunPrivilegedShellScriptAsync(
                script, RunMacOSAdministratorCommandAsync, cancellationToken);

            if (privilegedResult.IsSuccess)
            {
                return WatchFolderDaemonResult.Ok("Daemon started.");
            }

            if (IsElevationDenied(privilegedResult.Output))
            {
                return WatchFolderDaemonResult.Fail(
                    WatchFolderDaemonErrorCode.RequiresElevation,
                    "Administrator privileges were not granted for System scope.");
            }

            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, privilegedResult.Output);
        }
        catch (Exception ex)
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, ex.Message);
        }
        finally
        {
            try
            {
                File.Delete(tempPlistPath);
            }
            catch
            {
            }
        }
    }

    private static async Task<WatchFolderDaemonResult> StopSystemScopeWithElevationAsync(CancellationToken cancellationToken)
    {
        string domainService = GetDomainService(WatchFolderDaemonScope.System);
        string script = $"""
                         launchctl bootout '{EscapeShellSingleQuotedString(domainService)}'
                         """;

        CommandResult privilegedResult = await RunPrivilegedShellScriptAsync(
            script, RunMacOSAdministratorCommandAsync, cancellationToken);

        if (!privilegedResult.IsSuccess &&
            !privilegedResult.Output.Contains("Could not find service", StringComparison.OrdinalIgnoreCase))
        {
            if (IsElevationDenied(privilegedResult.Output))
            {
                return WatchFolderDaemonResult.Fail(
                    WatchFolderDaemonErrorCode.RequiresElevation,
                    "Administrator privileges were not granted for System scope.");
            }

            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, privilegedResult.Output);
        }

        return WatchFolderDaemonResult.Ok("Daemon stopped.");
    }

    private static string BuildPlistContent(
        WatchFolderDaemonScope scope,
        string daemonPath,
        string settingsFolder,
        bool startAtStartup)
    {
        string scopeValue = scope == WatchFolderDaemonScope.System ? "system" : "user";
        string escapedDaemonPath = SecurityElement.Escape(daemonPath) ?? daemonPath;
        string escapedSettingsPath = SecurityElement.Escape(settingsFolder) ?? settingsFolder;

        return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                  <key>Label</key>
                  <string>{Label}</string>
                  <key>ProgramArguments</key>
                  <array>
                    <string>{escapedDaemonPath}</string>
                    <string>--scope</string>
                    <string>{scopeValue}</string>
                    <string>--settings-folder</string>
                    <string>{escapedSettingsPath}</string>
                  </array>
                  <key>RunAtLoad</key>
                  {(startAtStartup ? "<true/>" : "<false/>")}
                  <key>KeepAlive</key>
                  {(startAtStartup ? "<true/>" : "<false/>")}
                  <key>StandardOutPath</key>
                  <string>/tmp/xerahs-watchfolder.log</string>
                  <key>StandardErrorPath</key>
                  <string>/tmp/xerahs-watchfolder.error.log</string>
                </dict>
                </plist>
                """;
    }

    private static async Task<CommandResult> RunMacOSAdministratorCommandAsync(
        string fileName,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        string shellCommand = $"{fileName} '{EscapeShellSingleQuotedString(string.Join("' '", arguments))}'";
        string appleScript = $"do shell script \"{EscapeAppleScriptString(shellCommand)}\" with administrator privileges";
        return await RunProcessWithArgumentsAsync("osascript", new[] { "-e", appleScript }, cancellationToken, DefaultElevatedCommandTimeoutMs);
    }

    private static bool IsElevationDenied(string output)
    {
        return output.Contains("User canceled", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("user cancelled", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("not authorized", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("permission denied", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeAppleScriptString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static Task<CommandResult> RunLaunchCtlAsync(string arguments, CancellationToken cancellationToken)
    {
        return RunProcessAsync("launchctl", arguments, cancellationToken, DefaultCommandTimeoutMs);
    }

    [DllImport("libc")]
    private static extern uint getuid();
}
