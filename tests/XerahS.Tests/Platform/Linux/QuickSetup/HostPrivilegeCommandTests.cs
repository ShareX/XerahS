// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
using NUnit.Framework;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using XerahS.Platform.Linux.Services.QuickSetup;

namespace XerahS.Tests.Platform.Linux.QuickSetup;

public class HostPrivilegeCommandTests
{
    [Test]
    public async Task SelectAsync_PkexecUsable_PrefersPkexec()
    {
        var (kind, message) = await HostPrivilegeCommand.SelectAsync(
            commandExists: (_, _) => new ValueTask<bool>(true),
            pkexecIsUsable: _ => new ValueTask<bool>(true),
            CancellationToken.None);

        Assert.That(kind, Is.EqualTo(HostPrivilegeKind.Pkexec));
        Assert.That(message, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task SelectAsync_PkexecExistsButNotUsable_FallsBackToRun0()
    {
        var (kind, message) = await HostPrivilegeCommand.SelectAsync(
            commandExists: (name, _) => new ValueTask<bool>(name == "run0"),
            pkexecIsUsable: _ => new ValueTask<bool>(false),
            CancellationToken.None);

        Assert.That(kind, Is.EqualTo(HostPrivilegeKind.Run0));
        Assert.That(message, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task SelectAsync_NeitherAvailable_ReturnsNoneWithMessage()
    {
        var (kind, message) = await HostPrivilegeCommand.SelectAsync(
            commandExists: (_, _) => new ValueTask<bool>(false),
            pkexecIsUsable: _ => new ValueTask<bool>(false),
            CancellationToken.None);

        Assert.That(kind, Is.EqualTo(HostPrivilegeKind.None));
        Assert.That(message, Is.Not.Empty);
    }

    [Test]
    public void AddArguments_Pkexec_AppendsShAndScript()
    {
        var startInfo = new ProcessStartInfo();
        HostPrivilegeCommand.AddArguments(
            startInfo,
            HostPrivilegeKind.Pkexec,
            "echo hello",
            userId: "1000",
            helperName: "xerahs-quick-setup",
            devicePaths: new[] { "/dev/input/event3", "/dev/input/event7" });

        Assert.That(startInfo.ArgumentList, Is.EqualTo(new[]
        {
            "/bin/sh", "-c", "echo hello", "xerahs-quick-setup", "1000", "/dev/input/event3", "/dev/input/event7"
        }));
    }

    [Test]
    public void AddArguments_Run0_PrependsDescription()
    {
        var startInfo = new ProcessStartInfo();
        HostPrivilegeCommand.AddArguments(
            startInfo,
            HostPrivilegeKind.Run0,
            "echo hello",
            userId: "1000",
            helperName: "xerahs-quick-setup",
            devicePaths: new[] { "/dev/input/event3" });

        Assert.That(startInfo.ArgumentList[0], Is.EqualTo("--description=XerahS keyboard access for global hotkeys"));
        Assert.That(startInfo.ArgumentList[1], Is.EqualTo("/bin/sh"));
    }

    [Test]
    public void AddArguments_None_Throws()
    {
        var startInfo = new ProcessStartInfo();
        Assert.Throws<InvalidOperationException>(() =>
            HostPrivilegeCommand.AddArguments(
                startInfo,
                HostPrivilegeKind.None,
                "echo hello",
                userId: "1000",
                helperName: "xerahs-quick-setup",
                devicePaths: new[] { "/dev/input/event3" }));
    }
}
