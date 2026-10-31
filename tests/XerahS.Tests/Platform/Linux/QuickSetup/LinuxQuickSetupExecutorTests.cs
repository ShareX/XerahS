// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
using NUnit.Framework;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using XerahS.Platform.Linux.Services.QuickSetup;

namespace XerahS.Tests.Platform.Linux.QuickSetup;

public class LinuxQuickSetupExecutorTests
{
    private static readonly string[] Keyboards = { "/dev/input/event3" };

    [Test]
    public async Task RunAsync_LauncherUnavailable_ReturnsFailure()
    {
        var launcher = new FakeLauncher(isAvailable: false, failureMessage: "no launcher");
        var executor = CreateExecutor((0, string.Empty, string.Empty));

        var result = await executor.RunAsync(launcher, Keyboards, "test", "unexpected");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Is.EqualTo("no launcher"));
        Assert.That(launcher.StartInfoRequests, Is.Zero);
    }

    [Test]
    public async Task RunAsync_IdentityUnresolved_ReturnsFailure()
    {
        var launcher = new FakeLauncher(isAvailable: true, failureMessage: string.Empty);
        var executor = new LinuxQuickSetupExecutor(
            identityResolver: () => null,
            runProcessAsync: (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        var result = await executor.RunAsync(launcher, Keyboards, "test", "unexpected");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("Could not determine"));
    }

    [Test]
    public async Task RunAsync_NoKeyboards_DoesNotPrompt()
    {
        var launcher = new FakeLauncher(isAvailable: true, failureMessage: string.Empty);
        var executor = CreateExecutor((0, string.Empty, string.Empty));

        var result = await executor.RunAsync(launcher, Array.Empty<string>(), "test", "unexpected");

        Assert.That(result.Success, Is.False);
        Assert.That(launcher.StartInfoRequests, Is.Zero);
    }

    [Test]
    public async Task RunAsync_PassesNumericUidAndKeyboardPathsToTheScript()
    {
        var launcher = new FakeLauncher(isAvailable: true, failureMessage: string.Empty);
        var executor = CreateExecutor((0, "keyboards=1\n", string.Empty));

        var result = await executor.RunAsync(launcher, Keyboards, "test", "unexpected");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("alice"));
            Assert.That(result.Message, Does.Contain("keyboards=1"));
            Assert.That(launcher.LastUserId, Is.EqualTo("1000"));
            Assert.That(launcher.LastDevicePaths, Is.EqualTo(Keyboards));
        });
    }

    [Test]
    public async Task RunAsync_ProcessExitsNonZero_TranslatesExitCode()
    {
        var launcher = new FakeLauncher(isAvailable: true, failureMessage: string.Empty);
        var executor = CreateExecutor((LinuxQuickSetupScriptBuilder.MissingSetfaclExitCode, string.Empty, "setfacl is missing on the host.\n"));

        var result = await executor.RunAsync(launcher, Keyboards, "test", "unexpected");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("acl"));
    }

    [TestCase("Arch", "sudo pacman -S acl")]
    [TestCase("Debian", "sudo apt install acl")]
    [TestCase("Fedora", "sudo dnf install acl")]
    [TestCase("NixOS", "pkgs.acl")]
    public void BuildFailureMessage_MissingSetfacl_GivesTheDistroInstallCommand(string family, string expected)
    {
        string message = LinuxQuickSetupExecutor.BuildFailureMessage(
            LinuxQuickSetupScriptBuilder.MissingSetfaclExitCode,
            "setfacl is missing on the host.",
            string.Empty,
            Enum.Parse<LinuxDistroFamily>(family));

        Assert.That(message, Does.Contain(expected));
    }

    [Test]
    public void BuildFailureMessage_MissingPolkitAgent_ExplainsTheAgent()
    {
        string message = LinuxQuickSetupExecutor.BuildFailureMessage(
            127,
            "Error executing command as another user: No authentication agent found.",
            "Error executing command as another user: No authentication agent found.\n",
            LinuxDistroFamily.Arch);

        Assert.That(message, Does.Contain("authentication agent"));
    }

    [Test]
    public void BuildFailureMessage_DismissedPrompt_ReportsCancellation()
    {
        string message = LinuxQuickSetupExecutor.BuildFailureMessage(126, "dismissed", string.Empty, LinuxDistroFamily.Unknown);

        Assert.That(message, Does.Contain("cancelled"));
    }

    [Test]
    public void IdentityResolver_UsesTheEffectiveUidAndSkipsRoot()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("geteuid only exists on Linux.");
        }

        var startInfo = new ProcessStartInfo("id", "-u") { RedirectStandardOutput = true, UseShellExecute = false };
        using Process process = Process.Start(startInfo)!;
        uint expectedUid = uint.Parse(process.StandardOutput.ReadToEnd().Trim(), System.Globalization.CultureInfo.InvariantCulture);
        process.WaitForExit();

        LinuxQuickSetupIdentity? identity = LinuxQuickSetupIdentityResolver.Resolve();

        if (expectedUid == 0)
        {
            Assert.That(identity, Is.Null);
        }
        else
        {
            Assert.That(identity?.Specifier, Is.EqualTo(expectedUid.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    private static LinuxQuickSetupExecutor CreateExecutor((int, string, string) processResult) =>
        new(
            identityResolver: () => new LinuxQuickSetupIdentity("alice", 1000),
            runProcessAsync: (_, _) => Task.FromResult(processResult));

    private sealed class FakeLauncher : IPrivilegedHostCommandLauncher
    {
        private readonly bool _isAvailable;
        private readonly string _failureMessage;

        public FakeLauncher(bool isAvailable, string failureMessage)
        {
            _isAvailable = isAvailable;
            _failureMessage = failureMessage;
        }

        public int StartInfoRequests { get; private set; }

        public string? LastUserId { get; private set; }

        public IReadOnlyList<string>? LastDevicePaths { get; private set; }

        public ValueTask<(bool IsAvailable, string FailureMessage)> IsAvailableAsync(CancellationToken cancellationToken = default)
            => new ValueTask<(bool, string)>((_isAvailable, _failureMessage));

        public ProcessStartInfo CreateStartInfo(string hostScript, string userId, IReadOnlyList<string> devicePaths)
        {
            StartInfoRequests++;
            LastUserId = userId;
            LastDevicePaths = devicePaths;
            return new ProcessStartInfo { FileName = "/bin/sh" };
        }
    }
}
