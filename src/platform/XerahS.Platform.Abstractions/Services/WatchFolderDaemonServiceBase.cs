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

namespace XerahS.Platform.Abstractions;

/// <summary>
/// Base class for platform-specific watch folder daemon services.
/// Provides shared process execution utilities, restart logic, and daemon path resolution.
/// </summary>
public abstract class WatchFolderDaemonServiceBase : IWatchFolderDaemonService
{
    protected const int DefaultCommandTimeoutMs = 10000;
    protected const int DefaultElevatedCommandTimeoutMs = 180000;
    protected const int DefaultPollIntervalMs = 250;

    public abstract bool IsSupported { get; }

    public abstract bool SupportsScope(WatchFolderDaemonScope scope);

    public abstract Task<WatchFolderDaemonStatus> GetStatusAsync(
        WatchFolderDaemonScope scope,
        CancellationToken cancellationToken = default);

    public abstract Task<WatchFolderDaemonResult> StartAsync(
        WatchFolderDaemonScope scope,
        string settingsFolder,
        bool startAtStartup,
        CancellationToken cancellationToken = default);

    public abstract Task<WatchFolderDaemonResult> StopAsync(
        WatchFolderDaemonScope scope,
        TimeSpan gracefulTimeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops then starts the daemon. Shared implementation across all platforms.
    /// </summary>
    public virtual async Task<WatchFolderDaemonResult> RestartAsync(
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

    /// <summary>
    /// Resolves the daemon executable path by checking candidate file names
    /// relative to the application directory.
    /// </summary>
    /// <param name="candidateFileNames">Platform-specific executable names to search for.</param>
    /// <param name="bareDefault">Fallback name returned when the application directory cannot be determined.</param>
    protected static string ResolveDaemonPath(string[] candidateFileNames, string bareDefault)
    {
        string? processPath = Environment.ProcessPath;
        string? processDirectory = string.IsNullOrWhiteSpace(processPath) ? null : Path.GetDirectoryName(processPath);
        if (string.IsNullOrWhiteSpace(processDirectory))
        {
            processDirectory = AppContext.BaseDirectory;
        }

        if (string.IsNullOrWhiteSpace(processDirectory))
        {
            return bareDefault;
        }

        string[] candidates = candidateFileNames
            .Select(name => Path.Combine(processDirectory, name))
            .ToArray();

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    /// <summary>
    /// Runs a process with string arguments, captures stdout/stderr, and applies a timeout.
    /// </summary>
    protected static async Task<CommandResult> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken,
        int timeoutMs = DefaultCommandTimeoutMs)
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

            Task completedTask = await Task.WhenAny(waitTask, Task.Delay(timeoutMs, cancellationToken));
            if (completedTask != waitTask)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                return new CommandResult(false, $"{fileName} command timed out.");
            }

            string stdout = await stdoutTask;
            string stderr = await stderrTask;
            string combined = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}{Environment.NewLine}{stderr}";

            return new CommandResult(process.ExitCode == 0, combined.Trim());
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Runs a process using ArgumentList (safe for arguments containing spaces/special chars),
    /// captures stdout/stderr, and applies a timeout.
    /// </summary>
    protected static async Task<CommandResult> RunProcessWithArgumentsAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        int timeoutMs = DefaultCommandTimeoutMs)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            foreach (string argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            Task waitTask = process.WaitForExitAsync(cancellationToken);

            Task completedTask = await Task.WhenAny(waitTask, Task.Delay(timeoutMs, cancellationToken));
            if (completedTask != waitTask)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                return new CommandResult(false, $"{fileName} command timed out.");
            }

            string stdout = await stdoutTask;
            string stderr = await stderrTask;
            string combined = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}{Environment.NewLine}{stderr}";

            return new CommandResult(process.ExitCode == 0, combined.Trim());
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Escapes a value for safe inclusion inside single-quoted shell strings.
    /// Shared by macOS and Linux daemon services.
    /// </summary>
    protected static string EscapeShellSingleQuotedString(string value)
    {
        return value.Replace("'", "'\"'\"'");
    }

    /// <summary>
    /// Writes a script to a temporary file, runs it via a privileged process runner,
    /// and cleans up the temp file. Shared pattern across macOS and Linux.
    /// </summary>
    protected static async Task<CommandResult> RunPrivilegedShellScriptAsync(
        string scriptContents,
        Func<string, string[], CancellationToken, Task<CommandResult>> runPrivilegedProcess,
        CancellationToken cancellationToken)
    {
        string scriptPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(scriptPath, scriptContents, cancellationToken);
            return await runPrivilegedProcess("/bin/sh", new[] { scriptPath }, cancellationToken);
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message);
        }
        finally
        {
            try
            {
                File.Delete(scriptPath);
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Unified result type for command execution, replacing platform-specific
    /// ScCommandResult, LaunchCtlResult, and SystemCommandResult.
    /// </summary>
    protected readonly record struct CommandResult(bool IsSuccess, string Output);
}
