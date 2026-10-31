// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Runs the host-side Quick Setup script via the launcher (pkexec/run0),
// captures exit codes and surfaces the polkit/CLI failure messages.
// Modeled on CrossMacro's LinuxQuickSetupExecutor.
using System.Diagnostics;
using System.Text;

namespace XerahS.Platform.Linux.Services.QuickSetup;

internal sealed record QuickSetupResult(bool Success, string Message);

internal sealed class LinuxQuickSetupExecutor
{
    private readonly Func<LinuxQuickSetupIdentity?> _identityResolver;
    private readonly Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> _runProcessAsync;

    public LinuxQuickSetupExecutor()
        : this(LinuxQuickSetupIdentityResolver.Resolve, RunProcessAsync)
    {
    }

    internal LinuxQuickSetupExecutor(
        Func<LinuxQuickSetupIdentity?> identityResolver,
        Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> runProcessAsync)
    {
        _identityResolver = identityResolver ?? throw new ArgumentNullException(nameof(identityResolver));
        _runProcessAsync = runProcessAsync ?? throw new ArgumentNullException(nameof(runProcessAsync));
    }

    public async Task<QuickSetupResult> RunAsync(
        IPrivilegedHostCommandLauncher launcher,
        IReadOnlyList<string> keyboardDevicePaths,
        string logContext,
        string unexpectedFailureMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(keyboardDevicePaths);

        var identity = _identityResolver();
        if (identity is null)
        {
            return new QuickSetupResult(
                Success: false,
                Message: "Could not determine which user to grant keyboard access to.");
        }

        if (keyboardDevicePaths.Count == 0)
        {
            return new QuickSetupResult(
                Success: false,
                Message: "No keyboard devices need access. Run 'xerahs doctor --linux-input' for details.");
        }

        var (isAvailable, failureMessage) = await launcher.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        if (!isAvailable)
        {
            return new QuickSetupResult(
                Success: false,
                Message: failureMessage);
        }

        var startInfo = launcher.CreateStartInfo(
            LinuxQuickSetupScriptBuilder.Build(),
            identity.Specifier,
            keyboardDevicePaths);

        try
        {
            var (exitCode, stdout, stderr) = await _runProcessAsync(startInfo, cancellationToken).ConfigureAwait(false);
            if (exitCode == 0)
            {
                XerahS.Common.DebugHelper.WriteLine($"[{logContext}] Keyboard access granted to {identity.LogDisplay}");
                return new QuickSetupResult(
                    Success: true,
                    Message: BuildSuccessMessage(stdout, identity.UserName));
            }

            string errorText = FirstNonEmptyLine(stderr) ?? FirstNonEmptyLine(stdout) ?? "Unknown host setup error.";
            XerahS.Common.DebugHelper.WriteLine($"[{logContext}] Quick Setup failed (ExitCode={exitCode}): {errorText}");
            return new QuickSetupResult(
                Success: false,
                Message: BuildFailureMessage(exitCode, errorText, stderr, LinuxDistroGuidance.Detect()));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            XerahS.Common.DebugHelper.WriteException(ex, $"[{logContext}] Failed to run Quick Setup");
            return new QuickSetupResult(
                Success: false,
                Message: unexpectedFailureMessage);
        }
    }

    private static string? FirstNonEmptyLine(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        foreach (string raw in content.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length > 0)
            {
                return line;
            }
        }

        return null;
    }

    private static string BuildSuccessMessage(string stdout, string userName)
    {
        string? counts = FirstNonEmptyLine(stdout);
        return counts is null
            ? $"Granted {userName} read access to the keyboard."
            : $"Granted {userName} read access to the keyboard ({counts}).";
    }

    internal static string BuildFailureMessage(int exitCode, string errorText, string stderr, LinuxDistroFamily family)
    {
        // polkit reports a missing agent in the text, not the exit code (pkexec 127, run0 1).
        if (stderr.Contains("authentication agent", StringComparison.OrdinalIgnoreCase))
        {
            return "No polkit authentication agent is running, so the password prompt could not open. " +
                   "Start one (for example hyprpolkitagent, polkit-gnome, polkit-kde-agent or lxqt-policykit) and retry.";
        }

        return exitCode switch
        {
            LinuxQuickSetupScriptBuilder.MissingSetfaclExitCode =>
                $"setfacl is not installed. To fix it, {LinuxDistroGuidance.InstallHint(family, QuickSetupPackage.Acl)}, then retry.",
            LinuxQuickSetupScriptBuilder.InvalidUserExitCode or LinuxQuickSetupScriptBuilder.InvalidDeviceExitCode =>
                $"Quick Setup refused its input ({errorText}). Nothing was changed.",
            LinuxQuickSetupScriptBuilder.NoDevicesExitCode =>
                $"No keyboard devices could be granted ({errorText}). Run 'xerahs doctor --linux-input' for details.",
            126 => "Authentication was cancelled. Nothing was changed.",
            127 => $"Authentication failed or is not allowed for this user ({errorText}). Administrator rights are needed.",
            _ => $"Quick Setup failed (exit {exitCode}): {errorText}",
        };
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                _ = stdoutBuilder.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                _ = stderrBuilder.AppendLine(e.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start session helper process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
                // ignored — the kill may race with normal exit.
            }
        });

        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        return (process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
    }
}
