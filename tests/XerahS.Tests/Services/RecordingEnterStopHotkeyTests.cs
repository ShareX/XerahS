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
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

#nullable enable

using Avalonia.Input;
using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.ScreenRecording;
using XerahS.Tests.Xip0052;
using XerahS.UI.Services;

namespace XerahS.Tests.Services;

[TestFixture]
public class RecordingEnterStopHotkeyTests
{
    [Test]
    public void Recording_RegistersEnterOnce_AndEnterStops()
    {
        var coordinator = new FakeScreenRecordingCoordinator();
        var hotkeys = new FakeHotkeyService();
        using var binder = new RecordingEnterStopHotkey(coordinator, hotkeys);

        coordinator.RaiseStatusChanged(RecordingStatus.Recording, TimeSpan.Zero);
        coordinator.RaiseStatusChanged(RecordingStatus.Recording, TimeSpan.FromSeconds(1));

        Assert.That(hotkeys.RegisterCount, Is.EqualTo(1));
        Assert.That(hotkeys.IsRegistered(hotkeys.LastRegistered!), Is.True);

        hotkeys.Raise(hotkeys.LastRegistered!);

        Assert.That(coordinator.SignalStopCalls, Is.EqualTo(1));
    }

    [Test]
    public void OtherHotkey_DoesNotStop()
    {
        var coordinator = new FakeScreenRecordingCoordinator();
        var hotkeys = new FakeHotkeyService();
        using var binder = new RecordingEnterStopHotkey(coordinator, hotkeys);

        coordinator.RaiseStatusChanged(RecordingStatus.Recording, TimeSpan.Zero);
        var other = new HotkeyInfo(Key.Escape) { Id = 99 };
        hotkeys.Raise(other);

        Assert.That(coordinator.SignalStopCalls, Is.EqualTo(0));
    }

    [Test]
    public void Paused_EnterStillStops_AndIdleUnregisters()
    {
        var coordinator = new FakeScreenRecordingCoordinator();
        var hotkeys = new FakeHotkeyService();
        using var binder = new RecordingEnterStopHotkey(coordinator, hotkeys);

        coordinator.RaiseStatusChanged(RecordingStatus.Paused, TimeSpan.FromSeconds(2));
        hotkeys.Raise(hotkeys.LastRegistered!);
        Assert.That(coordinator.SignalStopCalls, Is.EqualTo(1));

        coordinator.RaiseStatusChanged(RecordingStatus.Idle, TimeSpan.FromSeconds(2));
        Assert.That(hotkeys.UnregisterCount, Is.EqualTo(1));
        Assert.That(hotkeys.IsRegistered(hotkeys.LastRegistered!), Is.False);

        hotkeys.Raise(hotkeys.LastRegistered!);
        Assert.That(coordinator.SignalStopCalls, Is.EqualTo(1));
    }

    [Test]
    public void FailedRegistration_DoesNotStop_AndRetriesOnTheNextRecording()
    {
        var coordinator = new FakeScreenRecordingCoordinator();
        var hotkeys = new FakeHotkeyService { FailNextRegister = true };
        using var binder = new RecordingEnterStopHotkey(coordinator, hotkeys);

        coordinator.RaiseStatusChanged(RecordingStatus.Recording, TimeSpan.Zero);
        coordinator.RaiseStatusChanged(RecordingStatus.Recording, TimeSpan.FromSeconds(1));
        Assert.That(hotkeys.RegisterCount, Is.EqualTo(1));

        coordinator.RaiseStatusChanged(RecordingStatus.Idle, TimeSpan.Zero);
        hotkeys.FailNextRegister = false;
        coordinator.RaiseStatusChanged(RecordingStatus.Recording, TimeSpan.Zero);

        Assert.That(hotkeys.RegisterCount, Is.EqualTo(2));
        Assert.That(hotkeys.IsRegistered(hotkeys.LastRegistered!), Is.True);
    }

    private sealed class FakeHotkeyService : IHotkeyService
    {
        private readonly HashSet<ushort> _registeredIds = new();
        private ushort _nextId = 1;

        public int RegisterCount { get; private set; }
        public int UnregisterCount { get; private set; }
        public bool FailNextRegister { get; set; }
        public HotkeyInfo? LastRegistered { get; private set; }

        public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

        public event EventHandler? HotkeysChanged
        {
            add { }
            remove { }
        }

        public bool IsSuspended { get; set; }

        public bool RegisterHotkey(HotkeyInfo hotkeyInfo)
        {
            RegisterCount++;
            LastRegistered = hotkeyInfo;
            if (hotkeyInfo.Id == 0)
            {
                hotkeyInfo.Id = _nextId++;
            }

            if (FailNextRegister)
            {
                FailNextRegister = false;
                hotkeyInfo.Status = HotkeyStatus.Failed;
                return false;
            }

            hotkeyInfo.Status = HotkeyStatus.Registered;
            _registeredIds.Add(hotkeyInfo.Id);
            return true;
        }

        public bool UnregisterHotkey(HotkeyInfo hotkeyInfo)
        {
            UnregisterCount++;
            hotkeyInfo.Status = HotkeyStatus.NotConfigured;
            return _registeredIds.Remove(hotkeyInfo.Id);
        }

        public void UnregisterAll() => _registeredIds.Clear();

        public bool IsRegistered(HotkeyInfo hotkeyInfo) =>
            hotkeyInfo.Id != 0 && _registeredIds.Contains(hotkeyInfo.Id);

        public void Raise(HotkeyInfo hotkeyInfo) =>
            HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(hotkeyInfo));

        public void Dispose()
        {
        }
    }
}
