// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Builds the inline shell script that grants the current user read access
// (an ACL entry) to the keyboard event devices XerahS listens to for hotkeys.
using System.Text;

namespace XerahS.Platform.Linux.Services.QuickSetup;

internal static class LinuxQuickSetupScriptBuilder
{
    /// <summary>Exit code when <c>$1</c> is not a numeric user id.</summary>
    public const int InvalidUserExitCode = 21;

    /// <summary>Exit code when <c>setfacl</c> is not installed.</summary>
    public const int MissingSetfaclExitCode = 22;

    /// <summary>Exit code when an argument is not a <c>/dev/input/eventN</c> path.</summary>
    public const int InvalidDeviceExitCode = 23;

    /// <summary>Exit code when no keyboard device could be granted.</summary>
    public const int NoDevicesExitCode = 25;

    /// <summary>
    /// Builds a POSIX <c>sh</c> script (dash, bash and busybox ash compatible) for
    /// <c>/bin/sh -c &lt;script&gt; helper UID DEVICE...</c>, run as root through pkexec or run0.
    /// It grants <c>u:UID:r</c> on each listed device and nothing else:
    /// <list type="bullet">
    ///   <item>The user id must be numeric, so no name lookup happens as root (LDAP, systemd-homed and
    ///         sandboxed passwd databases all work).</item>
    ///   <item>Every device must be exactly <c>/dev/input/eventN</c> and a character device; anything
    ///         else aborts before a single ACL is changed.</item>
    ///   <item>The grant is read-only and lasts until the device node is recreated (reboot or replug).</item>
    /// </list>
    /// Validation runs before the <c>setfacl</c> check so it behaves the same on every host.
    /// </summary>
    public static string Build()
    {
        var script = new StringBuilder();

        // NixOS keeps setuid wrappers and system tools outside the FHS paths.
        _ = script.Append("set -eu; ");
        _ = script.Append("PATH='/run/wrappers/bin:/run/current-system/sw/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin'; export PATH; ");

        _ = script.Append("TARGET_UID=\"${1:-}\"; ");
        _ = script.Append("case \"$TARGET_UID\" in ''|*[!0-9]*) echo 'Quick Setup was given an invalid user id.' >&2; exit ").Append(InvalidUserExitCode).Append(";; esac; ");
        _ = script.Append("shift; ");

        _ = script.Append("if [ \"$#\" -eq 0 ]; then echo 'No keyboard devices were found to grant.' >&2; exit ").Append(NoDevicesExitCode).Append("; fi; ");

        _ = script.Append("for p in \"$@\"; do ");
        _ = script.Append("n=\"${p#/dev/input/event}\"; ");
        _ = script.Append("case \"$p\" in /dev/input/event*) ;; *) n=''; ;; esac; ");
        _ = script.Append("case \"$n\" in ''|*[!0-9]*) echo \"Refusing to change $p: not an input event device.\" >&2; exit ").Append(InvalidDeviceExitCode).Append(";; esac; ");
        _ = script.Append("done; ");

        _ = script.Append("if ! command -v setfacl >/dev/null 2>&1; then echo 'setfacl is missing on the host.' >&2; exit ").Append(MissingSetfaclExitCode).Append("; fi; ");

        _ = script.Append("count=0; ");
        _ = script.Append("for p in \"$@\"; do ");
        _ = script.Append("if [ -c \"$p\" ]; then setfacl -m \"u:${TARGET_UID}:r\" \"$p\"; count=$((count + 1)); fi; ");
        _ = script.Append("done; ");

        _ = script.Append("if [ \"$count\" -eq 0 ]; then echo 'None of the keyboard devices exist any more.' >&2; exit ").Append(NoDevicesExitCode).Append("; fi; ");
        _ = script.Append("printf 'keyboards=%d\\n' \"$count\"; ");
        _ = script.Append("exit 0");

        return script.ToString();
    }
}
