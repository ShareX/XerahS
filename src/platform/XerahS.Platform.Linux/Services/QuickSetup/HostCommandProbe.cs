// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Probes for the host-side privilege-escalation command. Modeled on
// CrossMacro's HostCommandProbe but rewritten to fit XerahS conventions.
namespace XerahS.Platform.Linux.Services.QuickSetup;

internal static class HostCommandProbe
{
    /// <summary>
    /// Searched after PATH. Desktop launchers often start XerahS with a minimal PATH, and NixOS keeps
    /// setuid wrappers in <c>/run/wrappers/bin</c> and system tools in <c>/run/current-system/sw/bin</c>.
    /// </summary>
    internal static readonly string[] WellKnownDirectories =
    {
        "/run/wrappers/bin",
        "/run/current-system/sw/bin",
        "/usr/local/sbin",
        "/usr/local/bin",
        "/usr/sbin",
        "/usr/bin",
        "/sbin",
        "/bin",
    };

    /// <summary>
    /// Returns true if <paramref name="name"/> resolves to an executable file on PATH, in a well-known
    /// system directory, or as an absolute path.
    /// </summary>
    public static ValueTask<bool> CommandExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ValueTask.FromResult(ResolveCommand(name) != null);
    }

    /// <summary>
    /// Returns true if a <c>pkexec</c> with its setuid-root bit exists. Without setuid, polkit cannot
    /// authorise it, so it counts as absent. pkexec is never run here, because that would prompt the user.
    /// </summary>
    public static ValueTask<bool> PkexecIsUsableAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(ResolveSetuidCommand("pkexec") != null);
    }

    /// <summary>Absolute path of the command to launch for <paramref name="kind"/>, or null.</summary>
    public static string? ResolveLauncher(HostPrivilegeKind kind) => kind switch
    {
        // NixOS puts a non-setuid pkexec on PATH next to the setuid wrapper; only the wrapper works.
        HostPrivilegeKind.Pkexec => ResolveSetuidCommand("pkexec"),
        HostPrivilegeKind.Run0 => ResolveCommand("run0"),
        _ => null,
    };

    public static string? ResolveCommand(string name)
    {
        if (Path.IsPathRooted(name))
        {
            return File.Exists(name) ? name : null;
        }

        return Candidates(name).FirstOrDefault(File.Exists);
    }

    private static string? ResolveSetuidCommand(string name)
    {
        return Candidates(name).FirstOrDefault(IsSetuidRoot);
    }

    private static IEnumerable<string> Candidates(string name)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        IEnumerable<string> directories = (pathEnv ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Concat(WellKnownDirectories);

        foreach (string directory in directories)
        {
            string candidate = Path.Combine(directory, name);
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static bool IsSetuidRoot(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows() || !File.Exists(path))
            {
                return false;
            }

            return (File.GetUnixFileMode(path) & UnixFileMode.SetUser) != 0;
        }
        catch
        {
            return false;
        }
    }
}
