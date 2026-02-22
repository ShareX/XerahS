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

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows.Services;

public sealed class WindowsWatchFolderDaemonService : WatchFolderDaemonServiceBase
{
    private const string ServiceName = "XerahSWatchFolder";
    private const string ServiceDisplayName = "XerahS Watch Folder";

    public override bool IsSupported => true;

    public override bool SupportsScope(WatchFolderDaemonScope scope) => scope == WatchFolderDaemonScope.System;

    public override async Task<WatchFolderDaemonStatus> GetStatusAsync(WatchFolderDaemonScope scope, CancellationToken cancellationToken = default)
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

    public override async Task<WatchFolderDaemonResult> StartAsync(
        WatchFolderDaemonScope scope,
        string settingsFolder,
        bool startAtStartup,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsScope(scope))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.UnsupportedScope, "Windows only supports System scope.");
        }

        if (string.IsNullOrWhiteSpace(settingsFolder))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.ValidationError, "Settings folder is required.");
        }

        string daemonPath = ResolveDaemonPath(
            new[] { "xerahs-watchfolder-daemon.exe", "XerahS.WatchFolder.Daemon.exe" },
            "xerahs-watchfolder-daemon.exe");

        if (string.IsNullOrEmpty(daemonPath) || !File.Exists(daemonPath))
        {
            return WatchFolderDaemonResult.Fail(
                WatchFolderDaemonErrorCode.ValidationError,
                "Daemon executable was not found next to the application.");
        }

        if (!new WindowsPlatformInfo().IsElevated)
        {
            return await StartServiceElevatedAsync(daemonPath, settingsFolder, startAtStartup, cancellationToken);
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

    public override async Task<WatchFolderDaemonResult> StopAsync(
        WatchFolderDaemonScope scope,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsScope(scope))
        {
            return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.UnsupportedScope, "Windows only supports System scope.");
        }

        bool installed = await ServiceExistsAsync(cancellationToken);
        if (!installed)
        {
            return WatchFolderDaemonResult.Ok("Service is not installed.");
        }

        if (!new WindowsPlatformInfo().IsElevated)
        {
            return await StopServiceElevatedAsync(cancellationToken);
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

            await Task.Delay(DefaultPollIntervalMs, cancellationToken);
        }

        return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, "Service did not stop before timeout.");
    }

    private async Task<WatchFolderDaemonResult> EnsureServiceConfiguredAsync(
        string daemonPath,
        string settingsFolder,
        bool startAtStartup,
        CancellationToken cancellationToken)
    {
        string serviceCommand = BuildServiceCommand(daemonPath, settingsFolder);
        string startupMode = startAtStartup ? "auto" : "demand";

        bool exists = await ServiceExistsAsync(cancellationToken);
        CommandResult result;
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

    private static string BuildServiceCommand(string daemonPath, string settingsFolder)
    {
        string escapedDaemonPath = daemonPath.Replace("\"", "\\\"");
        string escapedSettingsPath = settingsFolder.Replace("\"", "\\\"");
        return $"\\\"{escapedDaemonPath}\\\" --service --scope system --settings-folder \\\"{escapedSettingsPath}\\\"";
    }

    private static async Task<WatchFolderDaemonResult> StartServiceElevatedAsync(
        string daemonPath,
        string settingsFolder,
        bool startAtStartup,
        CancellationToken cancellationToken)
    {
        string startupMode = startAtStartup ? "auto" : "demand";
        string serviceCommand = BuildServiceCommand(daemonPath, settingsFolder);
        string script = string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Stop'",
            $"$serviceName = '{EscapePowerShellSingleQuotedString(ServiceName)}'",
            $"$serviceCommand = '{EscapePowerShellSingleQuotedString(serviceCommand)}'",
            $"$displayName = '{EscapePowerShellSingleQuotedString(ServiceDisplayName)}'",
            $"$startupMode = '{startupMode}'",
            string.Empty,
            "sc.exe query \"$serviceName\" *> $null",
            "if ($LASTEXITCODE -eq 1060) {",
            "    sc.exe create \"$serviceName\" binPath= \"$serviceCommand\" start= $startupMode DisplayName= \"$displayName\" *> $null",
            "} else {",
            "    sc.exe config \"$serviceName\" binPath= \"$serviceCommand\" start= $startupMode DisplayName= \"$displayName\" *> $null",
            "}",
            string.Empty,
            "if ($LASTEXITCODE -ne 0) {",
            "    exit $LASTEXITCODE",
            "}",
            string.Empty,
            "sc.exe start \"$serviceName\" *> $null",
            "if (($LASTEXITCODE -ne 0) -and ($LASTEXITCODE -ne 1056)) {",
            "    exit $LASTEXITCODE",
            "}",
            string.Empty,
            "exit 0"
        });

        ElevatedCommandResult elevatedResult = await RunElevatedPowerShellAsync(script, cancellationToken);
        if (elevatedResult.IsSuccess)
        {
            return WatchFolderDaemonResult.Ok("Service started.");
        }

        if (elevatedResult.WasCanceled)
        {
            return WatchFolderDaemonResult.Fail(
                WatchFolderDaemonErrorCode.RequiresElevation,
                "Administrator privileges were not granted.");
        }

        return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, elevatedResult.Output);
    }

    private static async Task<WatchFolderDaemonResult> StopServiceElevatedAsync(CancellationToken cancellationToken)
    {
        string script = string.Join(Environment.NewLine, new[]
        {
            $"$serviceName = '{EscapePowerShellSingleQuotedString(ServiceName)}'",
            "sc.exe stop \"$serviceName\" *> $null",
            "if (($LASTEXITCODE -ne 0) -and ($LASTEXITCODE -ne 1060) -and ($LASTEXITCODE -ne 1062)) {",
            "    exit $LASTEXITCODE",
            "}",
            string.Empty,
            "exit 0"
        });

        ElevatedCommandResult elevatedResult = await RunElevatedPowerShellAsync(script, cancellationToken);
        if (elevatedResult.IsSuccess)
        {
            return WatchFolderDaemonResult.Ok("Service stopped.");
        }

        if (elevatedResult.WasCanceled)
        {
            return WatchFolderDaemonResult.Fail(
                WatchFolderDaemonErrorCode.RequiresElevation,
                "Administrator privileges were not granted.");
        }

        return WatchFolderDaemonResult.Fail(WatchFolderDaemonErrorCode.CommandFailed, elevatedResult.Output);
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

    private static Task<CommandResult> RunScAsync(string arguments, CancellationToken cancellationToken)
    {
        return RunProcessAsync("sc.exe", arguments, cancellationToken, DefaultCommandTimeoutMs);
    }

    private static async Task<ElevatedCommandResult> RunElevatedPowerShellAsync(
        string script,
        CancellationToken cancellationToken)
    {
        try
        {
            string encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            process.Start();

            Task waitTask = process.WaitForExitAsync(cancellationToken);
            Task completedTask = await Task.WhenAny(waitTask, Task.Delay(DefaultElevatedCommandTimeoutMs, cancellationToken));
            if (completedTask != waitTask)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                return new ElevatedCommandResult(false, false, "Elevated command timed out.");
            }

            return process.ExitCode == 0
                ? new ElevatedCommandResult(true, false, string.Empty)
                : new ElevatedCommandResult(false, false, $"Elevated command failed with exit code {process.ExitCode}.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new ElevatedCommandResult(false, true, "UAC prompt was canceled.");
        }
        catch (OperationCanceledException)
        {
            return new ElevatedCommandResult(false, false, "Operation was canceled.");
        }
        catch (Exception ex)
        {
            return new ElevatedCommandResult(false, false, ex.Message);
        }
    }

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''");
    }

    private readonly record struct ElevatedCommandResult(bool IsSuccess, bool WasCanceled, string Output);
}
