// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Runs the Quick Setup script as root through pkexec or systemd run0.
using System.Diagnostics;

namespace XerahS.Platform.Linux.Services.QuickSetup;

internal sealed class DirectPolkitHostCommandLauncher : IPrivilegedHostCommandLauncher
{
    private const string HelperName = "xerahs-quick-setup";

    private readonly Func<string, CancellationToken, ValueTask<bool>> _commandExists;
    private readonly Func<CancellationToken, ValueTask<bool>> _pkexecIsUsable;
    private readonly Func<HostPrivilegeKind, string?> _resolveLauncher;
    private int _selectedKind;

    public DirectPolkitHostCommandLauncher()
        : this(HostCommandProbe.CommandExistsAsync, HostCommandProbe.PkexecIsUsableAsync, HostCommandProbe.ResolveLauncher)
    {
    }

    internal DirectPolkitHostCommandLauncher(
        Func<string, CancellationToken, ValueTask<bool>> commandExists,
        Func<CancellationToken, ValueTask<bool>> pkexecIsUsable,
        Func<HostPrivilegeKind, string?> resolveLauncher)
    {
        _commandExists = commandExists ?? throw new ArgumentNullException(nameof(commandExists));
        _pkexecIsUsable = pkexecIsUsable ?? throw new ArgumentNullException(nameof(pkexecIsUsable));
        _resolveLauncher = resolveLauncher ?? throw new ArgumentNullException(nameof(resolveLauncher));
    }

    public async ValueTask<(bool IsAvailable, string FailureMessage)> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var (kind, failureMessage) = await HostPrivilegeCommand.SelectAsync(
            _commandExists,
            _pkexecIsUsable,
            cancellationToken).ConfigureAwait(false);

        Interlocked.Exchange(ref _selectedKind, (int)kind);

        return kind == HostPrivilegeKind.None
            ? (false, failureMessage)
            : (true, string.Empty);
    }

    public ProcessStartInfo CreateStartInfo(string hostScript, string userId, IReadOnlyList<string> devicePaths)
    {
        var kind = (HostPrivilegeKind)Volatile.Read(ref _selectedKind);

        // Launch by absolute path: GUI sessions often have a minimal PATH, and on NixOS only the
        // setuid wrapper in /run/wrappers/bin can elevate.
        string fileName = _resolveLauncher(kind)
            ?? throw new InvalidOperationException("No host privilege command was selected.");

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        HostPrivilegeCommand.AddArguments(startInfo, kind, hostScript, userId, HelperName, devicePaths);
        return startInfo;
    }
}
