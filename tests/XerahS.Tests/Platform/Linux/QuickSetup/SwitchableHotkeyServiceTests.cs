// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
using Avalonia.Input;
using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Services;

namespace XerahS.Tests.Platform.Linux.QuickSetup;

public class SwitchableHotkeyServiceTests
{
    [Test]
    public void SwitchTo_KeepsIdsAndReRegistersRequestedHotkeys()
    {
        var portal = new FakeHotkeyService("portal");
        var evdev = new FakeHotkeyService("evdev");
        using var service = new SwitchableHotkeyService(portal);
        var capture = new HotkeyInfo(Key.PrintScreen);
        var record = new HotkeyInfo(Key.R, KeyModifiers.Control | KeyModifiers.Shift);
        var removed = new HotkeyInfo(Key.F1);

        service.RegisterHotkey(capture);
        service.RegisterHotkey(record);
        service.RegisterHotkey(removed);
        service.UnregisterHotkey(removed);
        ushort captureId = capture.Id;
        ushort recordId = record.Id;

        service.SwitchTo(evdev);

        Assert.Multiple(() =>
        {
            Assert.That(capture.Id, Is.EqualTo(captureId));
            Assert.That(record.Id, Is.EqualTo(recordId));
            Assert.That(captureId, Is.Not.EqualTo(recordId));
            Assert.That(evdev.Registrations, Is.EquivalentTo(new[] { capture, record }));
            Assert.That(service.IsRegistered(capture), Is.True);
            Assert.That(portal.Disposed, Is.True);
            Assert.That(service.Backend, Is.SameAs(evdev));
        });
    }

    [Test]
    public void SwitchTo_ForwardsTriggersFromTheNewBackendOnly()
    {
        var portal = new FakeHotkeyService("portal");
        var evdev = new FakeHotkeyService("evdev");
        using var service = new SwitchableHotkeyService(portal);
        var hotkey = new HotkeyInfo(Key.PrintScreen);
        service.RegisterHotkey(hotkey);
        var triggered = new List<HotkeyInfo>();
        service.HotkeyTriggered += (_, e) => triggered.Add(e.HotkeyInfo);

        service.SwitchTo(evdev);
        portal.Trigger(hotkey);
        evdev.Trigger(hotkey);

        Assert.That(triggered, Has.Count.EqualTo(1));
    }

    [Test]
    public void SwitchTo_CarriesSuspensionAndReportsTheNewDiagnostics()
    {
        var portal = new FakeHotkeyService("portal", warning: "focus only");
        var evdev = new FakeHotkeyService("evdev");
        using var service = new SwitchableHotkeyService(portal) { IsSuspended = true };
        bool changed = false;
        service.HotkeysChanged += (_, _) => changed = true;

        service.SwitchTo(evdev);

        Assert.Multiple(() =>
        {
            Assert.That(evdev.IsSuspended, Is.True);
            Assert.That(service.GetDiagnostics().BackendName, Is.EqualTo("evdev"));
            Assert.That(service.GetDiagnostics().UserFacingWarning, Is.Null);
            Assert.That(changed, Is.True);
        });
    }

    [Test]
    public void RegisterHotkey_AssignsIdsThatDoNotDependOnTheBackend()
    {
        using var service = new SwitchableHotkeyService(new FakeHotkeyService("portal"));
        var first = new HotkeyInfo(Key.A, KeyModifiers.Control);
        var second = new HotkeyInfo(Key.B, KeyModifiers.Control);

        service.RegisterHotkey(first);
        service.SwitchTo(new FakeHotkeyService("evdev"));
        service.RegisterHotkey(second);

        Assert.That(second.Id, Is.Not.EqualTo(first.Id));
    }
}
