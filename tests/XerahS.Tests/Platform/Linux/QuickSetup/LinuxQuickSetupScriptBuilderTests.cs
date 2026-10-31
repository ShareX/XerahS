// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
using NUnit.Framework;
using System.Diagnostics;
using XerahS.Platform.Linux.Services.QuickSetup;

namespace XerahS.Tests.Platform.Linux.QuickSetup;

public class LinuxQuickSetupScriptBuilderTests
{
    [Test]
    public void Build_GrantsReadOnlyAclToTheNumericUid()
    {
        string script = LinuxQuickSetupScriptBuilder.Build();

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("set -eu"));
            Assert.That(script, Does.Contain("u:${TARGET_UID}:r\""));
            Assert.That(script, Does.Not.Contain(":rw"));
            Assert.That(script, Does.Not.Contain("uinput"));
        });
    }

    [Test]
    public void Build_SearchesNixOSAndFhsToolDirectories()
    {
        string script = LinuxQuickSetupScriptBuilder.Build();

        Assert.That(script, Does.Contain("/run/wrappers/bin:/run/current-system/sw/bin:"));
    }

    [TestCase("")]
    [TestCase("alice")]
    [TestCase("1000;id")]
    [TestCase("-1")]
    public void Script_RejectsANonNumericUid(string uid)
    {
        int exitCode = RunScript(uid, "/dev/input/event0");

        Assert.That(exitCode, Is.EqualTo(LinuxQuickSetupScriptBuilder.InvalidUserExitCode));
    }

    [Test]
    public void Script_RejectsAnEmptyDeviceList()
    {
        int exitCode = RunScript("1000");

        Assert.That(exitCode, Is.EqualTo(LinuxQuickSetupScriptBuilder.NoDevicesExitCode));
    }

    [TestCase("/etc/shadow")]
    [TestCase("/dev/input/mice")]
    [TestCase("/dev/input/event")]
    [TestCase("/dev/input/event1abc")]
    [TestCase("/dev/input/event0/../../../etc/shadow")]
    [TestCase("/dev/uinput")]
    public void Script_RejectsAnythingButAnInputEventDevice(string path)
    {
        // A valid path first proves validation covers every argument before any ACL changes.
        int exitCode = RunScript("1000", "/dev/input/event0", path);

        Assert.That(exitCode, Is.EqualTo(LinuxQuickSetupScriptBuilder.InvalidDeviceExitCode));
    }

    private static int RunScript(params string[] arguments)
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/bin/sh"))
        {
            Assert.Ignore("The Quick Setup script only runs on Linux.");
        }

        var startInfo = new ProcessStartInfo("/bin/sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(LinuxQuickSetupScriptBuilder.Build());
        startInfo.ArgumentList.Add("xerahs-quick-setup");
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        Assert.That(process.WaitForExit(10_000), Is.True, "The script did not finish.");
        return process.ExitCode;
    }
}
