// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
using Avalonia.Input;
using NUnit.Framework;
using System.Diagnostics;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Services;
using XerahS.Platform.Linux.Services.QuickSetup;

namespace XerahS.Tests.Platform.Linux.QuickSetup;

public class LinuxInputQuickSetupServiceTests
{
    private static readonly string[] Keyboards = { "/dev/input/event3" };

    [Test]
    public void IsSetupRecommended_WhenHotkeysAreDegradedAndKeyboardsNeedAccess()
    {
        var fixture = new Fixture(warning: "Only works while XerahS is focused.");

        Assert.That(fixture.Service.IsSetupRecommended, Is.True);
    }

    [Test]
    public void IsSetupRecommended_NotWhenTheCurrentBackendWorks()
    {
        var fixture = new Fixture(warning: null);

        Assert.That(fixture.Service.IsSetupRecommended, Is.False);
    }

    [Test]
    public void IsSetupRecommended_NotWhenTheBackendIsForced()
    {
        var fixture = new Fixture(warning: "degraded", backendForced: true);

        Assert.That(fixture.Service.IsSetupRecommended, Is.False);
    }

    [Test]
    public void IsSetupRecommended_NotWhenNoKeyboardCanBeGranted()
    {
        var fixture = new Fixture(warning: "degraded") { KeyboardsNeedingAccess = Array.Empty<string>() };

        Assert.That(fixture.Service.IsSetupRecommended, Is.False);
    }

    [Test]
    public async Task RunSetupAsync_OnSuccess_SwitchesHotkeysToEvdev()
    {
        var fixture = new Fixture(warning: "degraded");
        var hotkey = new HotkeyInfo(Key.PrintScreen);
        fixture.Hotkeys.RegisterHotkey(hotkey);
        fixture.ProcessResult = (0, "keyboards=1\n", string.Empty);
        fixture.OnProcessRun = () => fixture.EvdevAvailable = true;

        HotkeyAccessSetupResult result = await fixture.Service.RunSetupAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(fixture.Hotkeys.Backend, Is.SameAs(fixture.Evdev));
            Assert.That(fixture.Evdev.Registrations, Does.Contain(hotkey));
            Assert.That(fixture.Service.IsSetupRecommended, Is.False);
        });
    }

    [Test]
    public async Task RunSetupAsync_OnFailure_KeepsTheCurrentBackend()
    {
        var fixture = new Fixture(warning: "degraded");
        fixture.ProcessResult = (126, string.Empty, "Request dismissed\n");

        HotkeyAccessSetupResult result = await fixture.Service.RunSetupAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("cancelled"));
            Assert.That(fixture.Hotkeys.Backend, Is.SameAs(fixture.Portal));
        });
    }

    [Test]
    public async Task RunSetupAsync_WhenAccessAlreadyExists_SwitchesWithoutPrompting()
    {
        var fixture = new Fixture(warning: "degraded") { KeyboardsNeedingAccess = Array.Empty<string>(), EvdevAvailable = true };

        HotkeyAccessSetupResult result = await fixture.Service.RunSetupAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(fixture.ProcessRuns, Is.Zero);
            Assert.That(fixture.Hotkeys.Backend, Is.SameAs(fixture.Evdev));
        });
    }

    [Test]
    public async Task RunSetupAsync_WhenGrantedButStillUnreadable_ReportsFailure()
    {
        var fixture = new Fixture(warning: "degraded");
        fixture.ProcessResult = (0, "keyboards=1\n", string.Empty);

        HotkeyAccessSetupResult result = await fixture.Service.RunSetupAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("doctor"));
            Assert.That(fixture.Hotkeys.Backend, Is.SameAs(fixture.Portal));
        });
    }

    private sealed class Fixture
    {
        public Fixture(string? warning, bool backendForced = false)
        {
            Portal = new FakeHotkeyService("portal", warning);
            Hotkeys = new SwitchableHotkeyService(Portal);
            var executor = new LinuxQuickSetupExecutor(
                identityResolver: () => new LinuxQuickSetupIdentity("alice", 1000),
                runProcessAsync: (_, _) =>
                {
                    ProcessRuns++;
                    OnProcessRun?.Invoke();
                    return Task.FromResult(ProcessResult);
                });

            Service = new LinuxInputQuickSetupService(
                Hotkeys,
                backendForced,
                new AvailableLauncher(),
                executor,
                () => KeyboardsNeedingAccess,
                () => EvdevAvailable,
                () => Evdev,
                () => LinuxDistroFamily.Arch);
        }

        public FakeHotkeyService Portal { get; }

        public FakeHotkeyService Evdev { get; } = new("evdev");

        public SwitchableHotkeyService Hotkeys { get; }

        public LinuxInputQuickSetupService Service { get; }

        public IReadOnlyList<string> KeyboardsNeedingAccess { get; set; } = Keyboards;

        public bool EvdevAvailable { get; set; }

        public (int, string, string) ProcessResult { get; set; } = (0, string.Empty, string.Empty);

        public Action? OnProcessRun { get; set; }

        public int ProcessRuns { get; private set; }
    }

    private sealed class AvailableLauncher : IPrivilegedHostCommandLauncher
    {
        public ValueTask<(bool IsAvailable, string FailureMessage)> IsAvailableAsync(CancellationToken cancellationToken = default)
            => new((true, string.Empty));

        public ProcessStartInfo CreateStartInfo(string hostScript, string userId, IReadOnlyList<string> devicePaths)
            => new("/bin/sh");
    }
}
