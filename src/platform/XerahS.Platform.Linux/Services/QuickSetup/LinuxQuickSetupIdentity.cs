// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Resolves the user whose keyboard access Quick Setup grants.
using System.Globalization;
using System.Runtime.InteropServices;

namespace XerahS.Platform.Linux.Services.QuickSetup;

/// <param name="UserName">Login name, for messages only.</param>
/// <param name="Uid">Effective user id. The ACL is granted to this id, never to the name.</param>
internal sealed record LinuxQuickSetupIdentity(string UserName, uint Uid)
{
    /// <summary>Passed to the privileged script as <c>$1</c>.</summary>
    public string Specifier => Uid.ToString(CultureInfo.InvariantCulture);

    public string LogDisplay => $"{UserName} (uid {Specifier})";
}

internal static class LinuxQuickSetupIdentityResolver
{
    /// <summary>
    /// Returns the effective user of this process. The id comes straight from <c>geteuid()</c>
    /// (glibc and musl), so it is right for LDAP, SSSD and systemd-homed users that are not in
    /// <c>/etc/passwd</c>. Returns null for root, which never needs Quick Setup.
    /// </summary>
    public static LinuxQuickSetupIdentity? Resolve()
    {
        uint uid;
        try
        {
            uid = geteuid();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }

        if (uid == 0)
        {
            return null;
        }

        string name = Environment.UserName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = uid.ToString(CultureInfo.InvariantCulture);
        }

        return new LinuxQuickSetupIdentity(name, uid);
    }

    [DllImport("libc", SetLastError = false)]
    private static extern uint geteuid();
}
