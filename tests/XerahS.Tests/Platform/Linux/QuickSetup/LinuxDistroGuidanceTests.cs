// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
using NUnit.Framework;
using XerahS.Platform.Linux.Services.QuickSetup;

namespace XerahS.Tests.Platform.Linux.QuickSetup;

public class LinuxDistroGuidanceTests
{
    [TestCase("arch", null, "Arch")]
    [TestCase("omarchy", "arch", "Arch")]
    [TestCase("cachyos", "arch", "Arch")]
    [TestCase("ubuntu", "debian", "Debian")]
    [TestCase("pop", "ubuntu debian", "Debian")]
    [TestCase("fedora", null, "Fedora")]
    [TestCase("rocky", "rhel centos fedora", "Fedora")]
    [TestCase("opensuse-tumbleweed", "opensuse suse", "OpenSuse")]
    [TestCase("alpine", null, "Alpine")]
    [TestCase("void", null, "Void")]
    [TestCase("gentoo", null, "Gentoo")]
    [TestCase("nixos", null, "NixOS")]
    [TestCase("someremix", "ubuntu", "Debian")]
    [TestCase("slackware", null, "Unknown")]
    [TestCase(null, null, "Unknown")]
    public void Detect_UsesIdThenIdLike(string? id, string? idLike, string expected)
    {
        Assert.That(LinuxDistroGuidance.Detect(id, idLike), Is.EqualTo(Enum.Parse<LinuxDistroFamily>(expected)));
    }

    [TestCase("Debian", "Polkit", "sudo apt install pkexec")]
    [TestCase("Gentoo", "Acl", "sudo emerge --ask sys-apps/acl")]
    [TestCase("Alpine", "Acl", "doas apk add acl")]
    [TestCase("OpenSuse", "Polkit", "sudo zypper install polkit")]
    [TestCase("NixOS", "Polkit", "security.polkit.enable = true")]
    [TestCase("Unknown", "Acl", "'acl'")]
    public void InstallHint_UsesTheDistroPackageManager(string family, string package, string expected)
    {
        string hint = LinuxDistroGuidance.InstallHint(Enum.Parse<LinuxDistroFamily>(family), Enum.Parse<QuickSetupPackage>(package));

        Assert.That(hint, Does.Contain(expected));
    }

    [Test]
    public void PersistentAccessHint_UsesDeclarativeConfigOnNixOS()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LinuxDistroGuidance.PersistentAccessHint(LinuxDistroFamily.NixOS), Does.Contain("extraGroups"));
            Assert.That(LinuxDistroGuidance.PersistentAccessHint(LinuxDistroFamily.Arch), Does.Contain("usermod -aG input"));
        });
    }
}
