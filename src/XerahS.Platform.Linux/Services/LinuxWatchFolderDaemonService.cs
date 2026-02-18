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

public sealed class LinuxWatchFolderDaemonService : IWatchFolderDaemonService
{
    private const string UnitName = "xerahs-watchfolder.service";
    private const int CommandTimeoutMs = 10000;
    private const int PollIntervalMs = 250;

    public bool IsSupported => true;

    public bool SupportsScope(WatchFolderDaemonScope scope)
    {
        return scope == WatchFolderDaemonScope.User || scope == WatchFolderDaemonScope.System;
    }

    public async Task<WatchFolderDaemonStatus> GetStatusAsync(WatchFolderDaemonScope scope, CancellationToken cancellationToken = default)
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

    public async Task<WatchFolderDaemonResult> StartAsync(
        WatchFolderDaemonScope scope,
        string settingsFolder,
        bool startAtStartup,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsScope(scope))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.UnsupportedScope, "Unsupported daemon scope.");
        }

        if (!ValidateMutatingScope(scope, out var scopeValidation))
        {
            return scopeValidation;
        }

        if (string.IsNullOrWhiteSpace(settingsFolder))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.ValidationError, "Settings folder is required.");
        }

        string daemonPath = ResolveDaemonPath();
        if (!File.Exists(daemonPath))
        {
            return WatchFolderDaemonResult.Fail(
                WatchFolderDaemonErrorCode.ValidationError,
                $"Daemon executable was not found: {daemonPath}");
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

    public async Task<WatchFolderDaemonResult> StopAsync(
        WatchFolderDaemonScope scope,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsScope(scope))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.UnsupportedScope, "Unsupported daemon scope.");
        }

        if (!ValidateMutatingScope(scope, out var scopeValidation))
        {
            return scopeValidation;
        }

        if (!File.Exists(GetUnitFilePath(scope)))
        {
            return WatchFolderDaemonResult.Ok("Daemon unit is not installed.");
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

            await Task.Delay(PollIntervalMs, cancellationToken);
        }

        return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, "Daemon did not stop before timeout.");
    }

    public async Task<WatchFolderDaemonResult> RestartAsync(
        WatchFolderDaemonScope scope,
        string settingsFolder,
        bool startAtStartup,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        var stopResult = await StopAsync(scope, gracefulTimeout, cancellationToken);
        if (!stopResult.Success && stopResult.ErrorCode != WatchFolderDaemonErrorCode.CommandFailed)
        {
            return stopResult;
        }

        return await StartAsync(scope, settingsFolder, startAtStartup, cancellationToken);
    }

    private static string ResolveDaemonPath()
    {
        string? processPath = Environment.ProcessPath;
        string? processDirectory = string.IsNullOrWhiteSpace(processPath) ? null : Path.GetDirectoryName(processPath);
        if (string.IsNullOrWhiteSpace(processDirectory))
        {
            processDirectory = AppContext.BaseDirectory;
        }

        if (string.IsNullOrWhiteSpace(processDirectory))
        {
            return "xerahs-watchfolder-daemon";
        }

        string[] candidates =
        {
            Path.Combine(processDirectory, "xerahs-watchfolder-daemon"),
            Path.Combine(processDirectory, "XerahS.WatchFolder.Daemon")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
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

    private static bool ValidateMutatingScope(WatchFolderDaemonScope scope, out WatchFolderDaemonResult result)
    {
        if (scope == WatchFolderDaemonScope.System && !new LinuxPlatformInfo().IsElevated)
        {
            result = WatchFolderDaemonResult.Fail(
                WatchFolderDaemonErrorCode.RequiresElevation,
                "Root privileges are required for System scope.");
            return false;
        }

        result = WatchFolderDaemonResult.Ok();
        return true;
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

            string wantedBy = scope == WatchFolderDaemonScope.System ? "multi-user.target" : "default.target";
            string escapedDaemonPath = daemonPath.Replace("\"", "\\\"");
            string escapedSettingsFolder = settingsFolder.Replace("\"", "\\\"");

            string content = $"""
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

            await File.WriteAllTextAsync(unitPath, content, cancellationToken);
            return WatchFolderDaemonResult.Ok();
        }
        catch (Exception ex)
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, ex.Message);
        }
    }

    private static async Task<SystemCommandResult> RunSystemctlAsync(
        WatchFolderDaemonScope scope,
        string arguments,
        CancellationToken cancellationToken)
    {
        string fullArguments = scope == WatchFolderDaemonScope.User
            ? $"--user {arguments}"
            : arguments;

        return await RunProcessAsync("systemctl", fullArguments, cancellationToken);
    }

    private static async Task<SystemCommandResult> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            Task waitTask = process.WaitForExitAsync(cancellationToken);

            Task completedTask = await Task.WhenAny(waitTask, Task.Delay(CommandTimeoutMs, cancellationToken));
            if (completedTask != waitTask)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                return new SystemCommandResult(false, $"{fileName} command timed out.");
            }

            string stdout = await stdoutTask;
            string stderr = await stderrTask;
            string combined = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}{Environment.NewLine}{stderr}";

            return new SystemCommandResult(process.ExitCode == 0, combined.Trim());
        }
        catch (Exception ex)
        {
            return new SystemCommandResult(false, ex.Message);
        }
    }

    private readonly record struct SystemCommandResult(bool IsSuccess, string Output);
}
