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
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

public sealed class LinuxWatchFolderDaemonService : WatchFolderDaemonServiceBase
{
    private const string UnitName = "xerahs-watchfolder.service";

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

        string unitFilePath = GetUnitFilePath(scope);
        bool installed = File.Exists(unitFilePath);
        if (!installed)
        {
            return new WatchFolderDaemonStatus
            {
                Scope = scope,
                State = WatchFolderDaemonState.NotInstalled,
                Installed = false,
                StartAtStartup = false,
                Message = "systemd unit is not installed."
            };
        }

        var activeResult = await RunSystemctlAsync(scope, $"is-active {UnitName}", cancellationToken);
        var enabledResult = await RunSystemctlAsync(scope, $"is-enabled {UnitName}", cancellationToken);

        bool isRunning = activeResult.Output.Trim().Equals("active", StringComparison.OrdinalIgnoreCase);
        bool startAtStartup = enabledResult.Output.Trim().Equals("enabled", StringComparison.OrdinalIgnoreCase);

        return new WatchFolderDaemonStatus
        {
            Scope = scope,
            State = isRunning ? WatchFolderDaemonState.Running : WatchFolderDaemonState.Stopped,
            Installed = true,
            StartAtStartup = startAtStartup,
            Message = isRunning ? "Daemon is running." : "Daemon is stopped."
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

        if (scope == WatchFolderDaemonScope.System && !new LinuxPlatformInfo().IsElevated)
        {
            return await StartSystemScopeWithElevationAsync(daemonPath, settingsFolder, startAtStartup, cancellationToken);
        }

        var ensureResult = await EnsureUnitFileAsync(scope, daemonPath, settingsFolder, cancellationToken);
        if (!ensureResult.Success)
        {
            return ensureResult;
        }

        var reloadResult = await RunSystemctlAsync(scope, "daemon-reload", cancellationToken);
        if (!reloadResult.IsSuccess)
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, reloadResult.Output);
        }

        var enableCommand = startAtStartup ? "enable" : "disable";
        var enableResult = await RunSystemctlAsync(scope, $"{enableCommand} {UnitName}", cancellationToken);
        if (!enableResult.IsSuccess)
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, enableResult.Output);
        }

        var startResult = await RunSystemctlAsync(scope, $"start {UnitName}", cancellationToken);
        if (!startResult.IsSuccess)
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, startResult.Output);
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

        if (!File.Exists(GetUnitFilePath(scope)))
        {
            return WatchFolderDaemonResult.Ok("Daemon unit is not installed.");
        }

        if (scope == WatchFolderDaemonScope.System && !new LinuxPlatformInfo().IsElevated)
        {
            return await StopSystemScopeWithElevationAsync(cancellationToken);
        }

        var stopResult = await RunSystemctlAsync(scope, $"stop {UnitName}", cancellationToken);
        if (!stopResult.IsSuccess && !stopResult.Output.Contains("not loaded", StringComparison.OrdinalIgnoreCase))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, stopResult.Output);
        }

        var timeout = gracefulTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : gracefulTimeout;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            var activeResult = await RunSystemctlAsync(scope, $"is-active {UnitName}", cancellationToken);
            bool isRunning = activeResult.Output.Trim().Equals("active", StringComparison.OrdinalIgnoreCase);
            if (!isRunning)
            {
                return WatchFolderDaemonResult.Ok("Daemon stopped.");
            }

            await Task.Delay(DefaultPollIntervalMs, cancellationToken);
        }

        return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, "Daemon did not stop before timeout.");
    }

    private static string GetUnitFilePath(WatchFolderDaemonScope scope)
    {
        if (scope == WatchFolderDaemonScope.System)
        {
            return Path.Combine("/etc/systemd/system", UnitName);
        }

        string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config");
        return Path.Combine(configHome, "systemd", "user", UnitName);
    }

    private static async Task<WatchFolderDaemonResult> EnsureUnitFileAsync(
        WatchFolderDaemonScope scope,
        string daemonPath,
        string settingsFolder,
        CancellationToken cancellationToken)
    {
        try
        {
            string unitPath = GetUnitFilePath(scope);
            string? directory = Path.GetDirectoryName(unitPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string content = BuildUnitFileContent(scope, daemonPath, settingsFolder);
            await File.WriteAllTextAsync(unitPath, content, cancellationToken);
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
        string tempUnitPath = Path.GetTempFileName();
        try
        {
            string unitContent = BuildUnitFileContent(WatchFolderDaemonScope.System, daemonPath, settingsFolder);
            await File.WriteAllTextAsync(tempUnitPath, unitContent, cancellationToken);

            string unitPath = GetUnitFilePath(WatchFolderDaemonScope.System);
            string enableCommand = startAtStartup ? "enable" : "disable";
            string script = $"""
                             set -e
                             cp '{EscapeShellSingleQuotedString(tempUnitPath)}' '{EscapeShellSingleQuotedString(unitPath)}'
                             chmod 644 '{EscapeShellSingleQuotedString(unitPath)}'
                             systemctl daemon-reload
                             systemctl {enableCommand} '{EscapeShellSingleQuotedString(UnitName)}'
                             systemctl start '{EscapeShellSingleQuotedString(UnitName)}'
                             """;

            CommandResult privilegedResult = await RunPrivilegedShellScriptAsync(
                script, RunPrivilegedProcessAsync, cancellationToken);

            if (privilegedResult.IsSuccess)
            {
                return WatchFolderDaemonResult.Ok("Daemon started.");
            }

            if (IsElevationDenied(privilegedResult.Output))
            {
                return WatchFolderDaemonResult.Fail(
                    WatchFolderDaemonErrorCode.RequiresElevation,
                    "Root privileges were not granted for System scope.");
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
                File.Delete(tempUnitPath);
            }
            catch
            {
            }
        }
    }

    private static async Task<WatchFolderDaemonResult> StopSystemScopeWithElevationAsync(CancellationToken cancellationToken)
    {
        CommandResult stopResult = await RunPrivilegedProcessAsync("systemctl", new[] { "stop", UnitName }, cancellationToken);
        if (!stopResult.IsSuccess &&
            !stopResult.Output.Contains("not loaded", StringComparison.OrdinalIgnoreCase))
        {
            if (IsElevationDenied(stopResult.Output))
            {
                return WatchFolderDaemonResult.Fail(
                    WatchFolderDaemonErrorCode.RequiresElevation,
                    "Root privileges were not granted for System scope.");
            }

            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, stopResult.Output);
        }

        return WatchFolderDaemonResult.Ok("Daemon stopped.");
    }

    private static string BuildUnitFileContent(
        WatchFolderDaemonScope scope,
        string daemonPath,
        string settingsFolder)
    {
        string wantedBy = scope == WatchFolderDaemonScope.System ? "multi-user.target" : "default.target";
        string escapedDaemonPath = daemonPath.Replace("\"", "\\\"");
        string escapedSettingsFolder = settingsFolder.Replace("\"", "\\\"");

        return $"""
                [Unit]
                Description=XerahS Watch Folder Daemon
                After=network.target

                [Service]
                Type=simple
                ExecStart="{escapedDaemonPath}" --scope {scope.ToString().ToLowerInvariant()} --settings-folder "{escapedSettingsFolder}"
                Restart=on-failure
                RestartSec=3
                KillSignal=SIGTERM
                TimeoutStopSec=30

                [Install]
                WantedBy={wantedBy}
                """;
    }

    private static async Task<CommandResult> RunPrivilegedProcessAsync(
        string fileName,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        var pkexecArguments = new List<string> { fileName };
        pkexecArguments.AddRange(arguments);

        CommandResult pkexecResult = await RunProcessWithArgumentsAsync(
            "pkexec",
            pkexecArguments,
            cancellationToken,
            DefaultElevatedCommandTimeoutMs);

        if (pkexecResult.IsSuccess || !IsExecutableNotFound(pkexecResult.Output))
        {
            return pkexecResult;
        }

        var sudoArguments = new List<string> { fileName };
        sudoArguments.AddRange(arguments);

        CommandResult sudoResult = await RunProcessWithArgumentsAsync(
            "sudo",
            sudoArguments,
            cancellationToken,
            DefaultElevatedCommandTimeoutMs);

        if (sudoResult.IsSuccess || !IsExecutableNotFound(sudoResult.Output))
        {
            return sudoResult;
        }

        return new CommandResult(false, "Neither pkexec nor sudo is available for privileged daemon operations.");
    }

    private static bool IsExecutableNotFound(string output)
    {
        return output.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsElevationDenied(string output)
    {
        return output.Contains("not authorized", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("authorization failed", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("authentication failed", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("permission denied", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("a terminal is required", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("no tty present", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("canceled", StringComparison.OrdinalIgnoreCase) ||
               output.Contains("cancelled", StringComparison.OrdinalIgnoreCase);
    }

    private static Task<CommandResult> RunSystemctlAsync(
        WatchFolderDaemonScope scope,
        string arguments,
        CancellationToken cancellationToken)
    {
        string fullArguments = scope == WatchFolderDaemonScope.User
            ? $"--user {arguments}"
            : arguments;

        return RunProcessAsync("systemctl", fullArguments, cancellationToken, DefaultCommandTimeoutMs);
    }
}
