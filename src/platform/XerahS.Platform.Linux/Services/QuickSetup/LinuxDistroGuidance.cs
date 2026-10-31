// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Distro-specific remediation text for Quick Setup, keyed on /etc/os-release.
namespace XerahS.Platform.Linux.Services.QuickSetup;

internal enum LinuxDistroFamily
{
    Unknown,
    Arch,
    Debian,
    Fedora,
    OpenSuse,
    Alpine,
    Void,
    Gentoo,
    Solus,
    NixOS,
}

/// <summary>A package the Quick Setup depends on.</summary>
internal enum QuickSetupPackage
{
    /// <summary>Provides <c>setfacl</c>.</summary>
    Acl,

    /// <summary>Provides <c>pkexec</c>.</summary>
    Polkit,

    /// <summary>Provides <c>tesseract</c> with English language data.</summary>
    Tesseract,
}

internal static class LinuxDistroGuidance
{
    public static LinuxDistroFamily Detect() => Detect(LinuxOsRelease.DistroId, LinuxOsRelease.DistroIdLike);

    /// <summary>Maps os-release <c>ID</c> and <c>ID_LIKE</c> to a package-manager family.</summary>
    public static LinuxDistroFamily Detect(string? id, string? idLike)
    {
        IEnumerable<string> ids = new[] { id }
            .Concat((idLike ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToLowerInvariant());

        foreach (string value in ids)
        {
            LinuxDistroFamily family = value switch
            {
                "arch" or "archarm" or "manjaro" or "endeavouros" or "cachyos" or "garuda" or "omarchy" => LinuxDistroFamily.Arch,
                "debian" or "ubuntu" or "linuxmint" or "pop" or "elementary" or "zorin" or "kali" or "raspbian" => LinuxDistroFamily.Debian,
                "fedora" or "rhel" or "centos" or "rocky" or "almalinux" or "nobara" or "bazzite" => LinuxDistroFamily.Fedora,
                "opensuse" or "opensuse-tumbleweed" or "opensuse-leap" or "suse" or "sles" => LinuxDistroFamily.OpenSuse,
                "alpine" or "postmarketos" => LinuxDistroFamily.Alpine,
                "void" => LinuxDistroFamily.Void,
                "gentoo" or "funtoo" => LinuxDistroFamily.Gentoo,
                "solus" => LinuxDistroFamily.Solus,
                "nixos" => LinuxDistroFamily.NixOS,
                _ => LinuxDistroFamily.Unknown,
            };

            if (family != LinuxDistroFamily.Unknown)
            {
                return family;
            }
        }

        return LinuxDistroFamily.Unknown;
    }

    /// <summary>How to install <paramref name="package"/> on <paramref name="family"/>.</summary>
    public static string InstallHint(LinuxDistroFamily family, QuickSetupPackage package)
    {
        string name = PackageName(family, package);
        return family switch
        {
            LinuxDistroFamily.Arch => $"sudo pacman -S {name}",
            LinuxDistroFamily.Debian => $"sudo apt install {name}",
            LinuxDistroFamily.Fedora => $"sudo dnf install {name}",
            LinuxDistroFamily.OpenSuse => $"sudo zypper install {name}",
            LinuxDistroFamily.Alpine => $"doas apk add {name}",
            LinuxDistroFamily.Void => $"sudo xbps-install {name}",
            LinuxDistroFamily.Gentoo => $"sudo emerge --ask {name}",
            LinuxDistroFamily.Solus => $"sudo eopkg install {name}",
            LinuxDistroFamily.NixOS => package == QuickSetupPackage.Polkit
                ? "set security.polkit.enable = true; in configuration.nix and run nixos-rebuild switch"
                : $"add pkgs.{name} to environment.systemPackages in configuration.nix and run nixos-rebuild switch",
            _ => $"install the '{name}' package with your package manager",
        };
    }

    /// <summary>How to keep keyboard access across reboots, which the Quick Setup grant does not.</summary>
    public static string PersistentAccessHint(LinuxDistroFamily family) => family switch
    {
        LinuxDistroFamily.NixOS =>
            "To keep it after a reboot, add \"input\" to users.users.<you>.extraGroups in configuration.nix, rebuild, then log out and back in.",
        _ =>
            "To keep it after a reboot, add yourself to the input group (sudo usermod -aG input $USER), then log out and back in.",
    };

    private static string PackageName(LinuxDistroFamily family, QuickSetupPackage package) => (family, package) switch
    {
        (LinuxDistroFamily.Gentoo, QuickSetupPackage.Acl) => "sys-apps/acl",
        (LinuxDistroFamily.Gentoo, QuickSetupPackage.Polkit) => "sys-auth/polkit",
        (LinuxDistroFamily.Debian, QuickSetupPackage.Polkit) => "pkexec",
        (LinuxDistroFamily.Arch, QuickSetupPackage.Tesseract) => "tesseract tesseract-data-eng",
        (LinuxDistroFamily.Debian, QuickSetupPackage.Tesseract) => "tesseract-ocr",
        (LinuxDistroFamily.OpenSuse, QuickSetupPackage.Tesseract) => "tesseract-ocr tesseract-ocr-traineddata-english",
        (LinuxDistroFamily.Fedora, QuickSetupPackage.Tesseract) => "tesseract tesseract-langpack-eng",
        (LinuxDistroFamily.Alpine, QuickSetupPackage.Tesseract) => "tesseract-ocr tesseract-ocr-data-eng",
        (LinuxDistroFamily.Void, QuickSetupPackage.Tesseract) => "tesseract-ocr",
        (LinuxDistroFamily.Gentoo, QuickSetupPackage.Tesseract) => "app-text/tesseract",
        (_, QuickSetupPackage.Tesseract) => "tesseract",
        (_, QuickSetupPackage.Acl) => "acl",
        _ => "polkit",
    };
}
