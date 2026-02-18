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

namespace XerahS.Platform.Windows.Services;

public sealed class WindowsWatchFolderDaemonService : IWatchFolderDaemonService
{
    private const string ServiceName = "XerahSWatchFolder";
    private const string ServiceDisplayName = "XerahS Watch Folder";
    private const int CommandTimeoutMs = 10000;
    private const int PollIntervalMs = 300;

    public bool IsSupported => true;

    public bool SupportsScope(WatchFolderDaemonScope scope) => scope == WatchFolderDaemonScope.System;

    public async Task<WatchFolderDaemonStatus> GetStatusAsync(WatchFolderDaemonScope scope, CancellationToken cancellationToken = default)
    {
        if (!SupportsScope(scope))
        {
            return new WatchFolderDaemonStatus
            {
                Scope = scope,
                State = WatchFolderDaemonState.Unknown,
                Installed = false,
                Message = "Windows watch folder daemon only supports System scope."
            };
        }

        bool installed = await ServiceExistsAsync(cancellationToken);
        if (!installed)
        {
            return new WatchFolderDaemonStatus
            {
                Scope = scope,
                State = WatchFolderDaemonState.NotInstalled,
                Installed = false,
                StartAtStartup = false,
                Message = "Service is not installed."
            };
        }

        var queryResult = await RunScAsync($"query \"{ServiceName}\"", cancellationToken);
        var qcResult = await RunScAsync($"qc \"{ServiceName}\"", cancellationToken);

        bool isRunning = queryResult.IsSuccess && queryResult.Output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
        bool startAtStartup = qcResult.IsSuccess && qcResult.Output.Contains("AUTO_START", StringComparison.OrdinalIgnoreCase);

        return new WatchFolderDaemonStatus
        {
            Scope = scope,
            State = isRunning ? WatchFolderDaemonState.Running : WatchFolderDaemonState.Stopped,
            Installed = true,
            StartAtStartup = startAtStartup,
            Message = isRunning ? "Service is running." : "Service is stopped."
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
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.UnsupportedScope, "Windows only supports System scope.");
        }

        if (!new WindowsPlatformInfo().IsElevated)
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.RequiresElevation, "Administrator privileges are required.");
        }

        if (string.IsNullOrWhiteSpace(settingsFolder))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.ValidationError, "Settings folder is required.");
        }

        string daemonPath = ResolveDaemonPath();
        if (string.IsNullOrEmpty(daemonPath) || !File.Exists(daemonPath))
        {
            return WatchFolderDaemonResult.Fail(
                WatchFolderDaemonErrorCode.ValidationError,
                "Daemon executable was not found next to the application.");
        }

        var ensureResult = await EnsureServiceConfiguredAsync(daemonPath, settingsFolder, startAtStartup, cancellationToken);
        if (!ensureResult.Success)
        {
            return ensureResult;
        }

        var startResult = await RunScAsync($"start \"{ServiceName}\"", cancellationToken);
        if (!startResult.IsSuccess &&
            !startResult.Output.Contains("SERVICE_ALREADY_RUNNING", StringComparison.OrdinalIgnoreCase))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, startResult.Output);
        }

        return WatchFolderDaemonResult.Ok("Service started.");
    }

    public async Task<WatchFolderDaemonResult> StopAsync(
        WatchFolderDaemonScope scope,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsScope(scope))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.UnsupportedScope, "Windows only supports System scope.");
        }

        if (!new WindowsPlatformInfo().IsElevated)
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.RequiresElevation, "Administrator privileges are required.");
        }

        bool installed = await ServiceExistsAsync(cancellationToken);
        if (!installed)
        {
            return WatchFolderDaemonResult.Ok("Service is not installed.");
        }

        var stopResult = await RunScAsync($"stop \"{ServiceName}\"", cancellationToken);
        if (!stopResult.IsSuccess &&
            !stopResult.Output.Contains("SERVICE_NOT_ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, stopResult.Output);
        }

        var timeout = gracefulTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : gracefulTimeout;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            var queryResult = await RunScAsync($"query \"{ServiceName}\"", cancellationToken);
            if (queryResult.IsSuccess && queryResult.Output.Contains("STOPPED", StringComparison.OrdinalIgnoreCase))
            {
                return WatchFolderDaemonResult.Ok("Service stopped.");
            }

            await Task.Delay(PollIntervalMs, cancellationToken);
        }

        return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, "Service did not stop before timeout.");
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
            return string.Empty;
        }

        string[] candidates =
        {
            Path.Combine(processDirectory, "xerahs-watchfolder-daemon.exe"),
            Path.Combine(processDirectory, "XerahS.WatchFolder.Daemon.exe")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private async Task<WatchFolderDaemonResult> EnsureServiceConfiguredAsync(
        string daemonPath,
        string settingsFolder,
        bool startAtStartup,
        CancellationToken cancellationToken)
    {
        string escapedDaemonPath = daemonPath.Replace("\"", "\\\"");
        string escapedSettingsPath = settingsFolder.Replace("\"", "\\\"");
        string serviceCommand = $"\\\"{escapedDaemonPath}\\\" --service --scope system --settings-folder \\\"{escapedSettingsPath}\\\"";
        string startupMode = startAtStartup ? "auto" : "demand";

        bool exists = await ServiceExistsAsync(cancellationToken);
        ScCommandResult result;
        if (exists)
        {
            result = await RunScAsync(
                $"config \"{ServiceName}\" binPath= \"{serviceCommand}\" start= {startupMode} DisplayName= \"{ServiceDisplayName}\"",
                cancellationToken);
        }
        else
        {
            result = await RunScAsync(
                $"create \"{ServiceName}\" binPath= \"{serviceCommand}\" start= {startupMode} DisplayName= \"{ServiceDisplayName}\"",
                cancellationToken);
        }

        if (!result.IsSuccess)
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, result.Output);
        }

        return WatchFolderDaemonResult.Ok();
    }

    private async Task<bool> ServiceExistsAsync(CancellationToken cancellationToken)
    {
        var queryResult = await RunScAsync($"query \"{ServiceName}\"", cancellationToken);
        if (queryResult.IsSuccess)
        {
            return true;
        }

        return !queryResult.Output.Contains("FAILED 1060", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ScCommandResult> RunScAsync(string arguments, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
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

                return new ScCommandResult(false, "sc.exe command timed out.");
            }

            string output = await outputTask;
            string error = await errorTask;
            string combined = string.IsNullOrWhiteSpace(error) ? output : $"{output}{Environment.NewLine}{error}";

            return new ScCommandResult(process.ExitCode == 0, combined.Trim());
        }
        catch (Exception ex)
        {
            return new ScCommandResult(false, ex.Message);
        }
    }

    private readonly record struct ScCommandResult(bool IsSuccess, string Output);
}
