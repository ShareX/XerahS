// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
using XerahS.Platform.Abstractions;

namespace XerahS.Tests.Platform.Linux.QuickSetup;

/// <summary>In-memory hotkey backend for switching and Quick Setup tests.</summary>
internal sealed class FakeHotkeyService : IHotkeyService
{
    private readonly HashSet<ushort> _registered = new();
    private ushort _nextId = 1;

    public FakeHotkeyService(string name, string? warning = null)
    {
        Name = name;
        Warning = warning;
    }

    public string Name { get; }

    public string? Warning { get; set; }

    public bool Disposed { get; private set; }

    public List<HotkeyInfo> Registrations { get; } = new();

    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

    public event EventHandler? HotkeysChanged
    {
        add { }
        remove { }
    }

    public bool IsSuspended { get; set; }

    public bool RegisterHotkey(HotkeyInfo hotkeyInfo)
    {
        if (hotkeyInfo.Id == 0)
        {
            hotkeyInfo.Id = _nextId++;
        }

        Registrations.Add(hotkeyInfo);
        return _registered.Add(hotkeyInfo.Id);
    }

    public bool UnregisterHotkey(HotkeyInfo hotkeyInfo) => _registered.Remove(hotkeyInfo.Id);

    public void UnregisterAll() => _registered.Clear();

    public bool IsRegistered(HotkeyInfo hotkeyInfo) => _registered.Contains(hotkeyInfo.Id);

    public HotkeyDiagnostics GetDiagnostics() => new(HotkeyBackendState.Native, Name, Warning);

    public void Trigger(HotkeyInfo hotkeyInfo) => HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(hotkeyInfo));

    public void Dispose() => Disposed = true;
}
