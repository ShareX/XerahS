// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Pattern modeled on CrossMacro's HostPrivilegeCommand
// (src/CrossMacro.Platform.Linux/Services/QuickSetup/HostPrivilegeCommand.cs,
// Apache-2.0), rewritten for XerahS conventions and trimmed to just the
// host-side launcher selection. No code copied verbatim.
using System.Diagnostics;

namespace XerahS.Platform.Linux.Services.QuickSetup;

internal enum HostPrivilegeKind
{
    None,
    Pkexec,
    Run0,
}

internal static class HostPrivilegeCommand
{
    /// <summary>
    /// Select the host-side privilege-escalation command used to run the
    /// Quick Setup script. Preference order: pkexec (setuid root) > run0
    /// (systemd 256+) > None. Either is enough for `setfacl` against the
    /// current user's identity; polkit handles the auth dialog.
    /// </summary>
    public static async ValueTask<(HostPrivilegeKind Kind, string FailureMessage)> SelectAsync(
        Func<string, CancellationToken, ValueTask<bool>> commandExists,
        Func<CancellationToken, ValueTask<bool>> pkexecIsUsable,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandExists);
        ArgumentNullException.ThrowIfNull(pkexecIsUsable);

        bool hasPkexec = await commandExists("pkexec", cancellationToken).ConfigureAwait(false);
        if (hasPkexec && await pkexecIsUsable(cancellationToken).ConfigureAwait(false))
        {
            return (HostPrivilegeKind.Pkexec, string.Empty);
        }

        if (await commandExists("run0", cancellationToken).ConfigureAwait(false))
        {
            return (HostPrivilegeKind.Run0, string.Empty);
        }

        string polkitHint = LinuxDistroGuidance.InstallHint(LinuxDistroGuidance.Detect(), QuickSetupPackage.Polkit);
        return (HostPrivilegeKind.None, hasPkexec
            ? "pkexec is installed but is not setuid root, and systemd run0 (systemd 256+) is unavailable. Restore pkexec's setuid bit (chmod 4755) and retry."
            : $"Neither pkexec nor systemd run0 is available. To use Quick Setup, {polkitHint}.");
    }

    /// <summary>
    /// Appends the launcher-specific arguments so that <c>/bin/sh -c <paramref name="hostScript"/></c>
    /// runs as root with <c>$0</c> = <paramref name="helperName"/>, <c>$1</c> = <paramref name="userId"/>
    /// and the device paths after it.
    /// </summary>
    public static void AddArguments(
        ProcessStartInfo startInfo,
        HostPrivilegeKind commandKind,
        string hostScript,
        string userId,
        string helperName,
        IReadOnlyList<string> devicePaths)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostScript);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(helperName);
        ArgumentNullException.ThrowIfNull(devicePaths);

        if (commandKind == HostPrivilegeKind.Run0)
        {
            // run0 (systemd 256+) takes --description for the auth prompt.
            startInfo.ArgumentList.Add("--description=XerahS keyboard access for global hotkeys");
        }
        else if (commandKind != HostPrivilegeKind.Pkexec)
        {
            throw new InvalidOperationException("No host privilege command was selected.");
        }

        startInfo.ArgumentList.Add("/bin/sh");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(hostScript);
        startInfo.ArgumentList.Add(helperName);     // $0
        startInfo.ArgumentList.Add(userId);         // $1
        foreach (string devicePath in devicePaths)
        {
            startInfo.ArgumentList.Add(devicePath); // $2...
        }
    }
}
