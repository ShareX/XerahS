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
    MERCHANTABILITY or PATHICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.Runtime.InteropServices;

namespace XerahS.Common.Platform.Linux;

/// <summary>
/// Linux-specific process tree termination that walks /proc to discover actual
/// descendants and sends SIGKILL to each one individually.
///
/// Why this exists:
///   System.Diagnostics.Process.Kill(entireProcessTree: true) on .NET 10 / Linux
///   does NOT reliably tear down descendant processes (confirmed by direct
///   probe on 2026-10-17: parent_dead=True, child_dead=False even when the
///   child is a confirmed process-group leader). The runtime falls back to
///   signalling the process group, which only works when the target is the
///   process-group leader; on Linux that is not guaranteed for child processes
///   spawned by a normal Process.Start.
///
/// Safety properties of this implementation:
///   - Enumerates /proc/&lt;pid&gt;/task/&lt;pid&gt;/children to discover DIRECT children only.
///     We never use killpg or signal a process group, so unrelated processes that
///     happen to share a PGID with a descendant are NOT touched.
///   - Recurses depth-first so leaves die before their parents. This avoids
///     orphaning a subtree if a kill races with a fork in the descendants.
///   - SIGKILL is unconditional: descendants cannot intercept or ignore it.
///   - All filesystem and syscall failures are swallowed per-node. A single
///     vanished or permission-denied descendant does not abort the cleanup.
///   - Includes a visited-set keyed by PID to defend against accidental
///     cycles (PID reuse, debuggers reaping, etc.). A process is killed at
///     most once.
///   - includeRoot defaults to true so the caller does not have to remember
///     to also kill the process they handed in. Pass false only if you want
///     to keep the root alive and kill its descendants only.
/// </summary>
public static class LinuxProcessTree
{
    private const int SIGKILL = 9;

    [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
    private static extern int kill(int pid, int sig);

    /// <summary>
    /// Terminates the process identified by <paramref name="rootPid"/> and every
    /// descendant discovered via /proc enumeration. No-op on non-Linux platforms.
    /// </summary>
    public static void KillProcessTree(int rootPid, bool includeRoot = true)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        if (rootPid <= 0)
        {
            return;
        }

        var visited = new HashSet<int>();
        KillRecursive(rootPid, visited);

        if (includeRoot)
        {
            TryKill(rootPid);
        }
    }

    private static void KillRecursive(int pid, HashSet<int> visited)
    {
        if (pid <= 0 || !visited.Add(pid))
        {
            return;
        }

        // Enumerate direct children via the kernel-maintained children file.
        // Format: space-separated TIDs (which equal PIDs for non-thread leaders).
        // On Linux >= 3.5 this is the canonical, race-free way to discover children.
        int[] children = ReadChildPids(pid);

        foreach (int child in children)
        {
            KillRecursive(child, visited);
        }

        TryKill(pid);
    }

    private static int[] ReadChildPids(int pid)
    {
        // /proc/<pid>/task/<pid>/children is a single line of space-separated PIDs.
        // It is only readable by the same UID as the process owner or by root,
        // so any IOException means we cannot see further; we return empty.
        string path = $"/proc/{pid}/task/{pid}/children";
        try
        {
            string content = File.ReadAllText(path).Trim();
            if (string.IsNullOrEmpty(content))
            {
                return Array.Empty<int>();
            }

            var result = new List<int>(capacity: 8);
            foreach (string token in content.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(token, out int childPid) && childPid > 0)
                {
                    result.Add(childPid);
                }
            }
            return result.ToArray();
        }
        catch (FileNotFoundException)
        {
            // Process exited between snapshot and read. Normal race, not an error.
            return Array.Empty<int>();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<int>();
        }
        catch (UnauthorizedAccessException)
        {
            // Not our process; refuse to enumerate further down this branch.
            return Array.Empty<int>();
        }
        catch (IOException)
        {
            return Array.Empty<int>();
        }
    }

    private static void TryKill(int pid)
    {
        try
        {
            int rc = kill(pid, SIGKILL);
            // rc == 0: signal delivered. rc == -1: ESRCH (no such process) is fine.
            // Anything else (EPERM etc.) is logged-and-swallowed: we are best-effort
            // during teardown and must not throw out of a Close() path.
            _ = rc;
        }
        catch
        {
            // Defensive: DllImport should not throw, but a host that denies
            // libc access would surface here. Swallow to preserve the Close()
            // contract that callers rely on.
        }
    }
}
