#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using Avalonia.Input;
using ShareX.Avalonia.Platform.Abstractions;
using ShareX.Avalonia.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ShareX.Avalonia.Core.Hotkeys;

/// <summary>
/// High-level hotkey management - orchestrates registration and triggering
/// </summary>
public class HotkeyManager : IDisposable
{
    private readonly IHotkeyService _hotkeyService;
    private readonly Dictionary<ushort, HotkeySettings> _hotkeyMap = new();
    private bool _disposed;

    /// <summary>
    /// List of all configured hotkeys
    /// </summary>
    public List<HotkeySettings> Hotkeys { get; private set; } = new();

    /// <summary>
    /// When true, hotkeys are temporarily disabled
    /// </summary>
    public bool IgnoreHotkeys
    {
        get => _hotkeyService.IsSuspended;
        set => _hotkeyService.IsSuspended = value;
    }

    /// <summary>
    /// Fired when a hotkey is triggered
    /// </summary>
    public event EventHandler<HotkeySettings>? HotkeyTriggered;

    public HotkeyManager(IHotkeyService hotkeyService)
    {
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _hotkeyService.HotkeyTriggered += OnHotkeyServiceTriggered;
    }

    private void OnHotkeyServiceTriggered(object? sender, HotkeyTriggeredEventArgs e)
    {
        if (_hotkeyMap.TryGetValue(e.HotkeyInfo.Id, out var settings))
        {
            Debug.WriteLine($"HotkeyManager: Triggering {settings}");
            HotkeyTriggered?.Invoke(this, settings);
        }
    }

    /// <summary>
    /// Update hotkeys from configuration
    /// </summary>
    public void UpdateHotkeys(List<HotkeySettings> hotkeys, bool showFailedHotkeys = false)
    {
        UnregisterAllHotkeys();
        Hotkeys = hotkeys ?? new List<HotkeySettings>();
        RegisterAllHotkeys();

        if (showFailedHotkeys)
        {
            ShowFailedHotkeys();
        }
    }

    /// <summary>
    /// Register a single hotkey
    /// </summary>
    public bool RegisterHotkey(HotkeySettings settings)
    {
        if (!settings.Enabled || !settings.HotkeyInfo.IsValid)
        {
            settings.HotkeyInfo.Status = HotkeyStatus.NotConfigured;
            return false;
        }

        // Always try to unregister first if this hotkey has an ID
        // Ignore failures - the hotkey might not have been registered yet
        if (settings.HotkeyInfo.Id != 0)
        {
            UnregisterHotkey(settings); // Don't check result
        }

        bool result = _hotkeyService.RegisterHotkey(settings.HotkeyInfo);

        if (result)
        {
            _hotkeyMap[settings.HotkeyInfo.Id] = settings;
            Debug.WriteLine($"HotkeyManager: Registered {settings}");
        }
        else
        {
            Debug.WriteLine($"HotkeyManager: Failed to register {settings}");
        }

        if (!Hotkeys.Contains(settings))
        {
            Hotkeys.Add(settings);
        }

        return result;
    }

    /// <summary>
    /// Unregister a single hotkey
    /// </summary>
    public bool UnregisterHotkey(HotkeySettings settings)
    {
        if (settings.HotkeyInfo.Id == 0)
            return false;

        bool result = _hotkeyService.UnregisterHotkey(settings.HotkeyInfo);
        _hotkeyMap.Remove(settings.HotkeyInfo.Id);

        if (Hotkeys.Contains(settings))
        {
            Hotkeys.Remove(settings);
        }

        return result;
    }

    /// <summary>
    /// Register all hotkeys in the list
    /// </summary>
    public void RegisterAllHotkeys()
    {
        foreach (var settings in Hotkeys)
        {
            RegisterHotkey(settings);
        }
    }

    /// <summary>
    /// Unregister all hotkeys
    /// </summary>
    public void UnregisterAllHotkeys()
    {
        _hotkeyService.UnregisterAll();
        _hotkeyMap.Clear();

        foreach (var settings in Hotkeys)
        {
            settings.HotkeyInfo.Status = HotkeyStatus.NotConfigured;
            settings.HotkeyInfo.Id = 0;
        }
    }

    /// <summary>
    /// Toggle hotkeys on/off
    /// </summary>
    public void ToggleHotkeys(bool disabled)
    {
        IgnoreHotkeys = disabled;
    }

    /// <summary>
    /// Get list of hotkeys that failed to register
    /// </summary>
    public List<HotkeySettings> GetFailedHotkeys()
    {
        return Hotkeys.Where(h => h.HotkeyInfo.Status == HotkeyStatus.Failed).ToList();
    }

    /// <summary>
    /// Show warning for failed hotkeys (placeholder - will be UI-specific)
    /// </summary>
    private void ShowFailedHotkeys()
    {
        var failed = GetFailedHotkeys();
        if (failed.Count > 0)
        {
            Debug.WriteLine($"Warning: {failed.Count} hotkey(s) failed to register:");
            foreach (var h in failed)
            {
                Debug.WriteLine($"  - {h}");
            }
        }
    }

    /// <summary>
    /// Get the default hotkey list (matches ShareX defaults)
    /// </summary>
    public static List<HotkeySettings> GetDefaultHotkeyList()
    {
        return new List<HotkeySettings>
        {
            new HotkeySettings(HotkeyType.RectangleRegion,
                new HotkeyInfo(Key.PrintScreen, KeyModifiers.Control)),
            new HotkeySettings(HotkeyType.PrintScreen,
                new HotkeyInfo(Key.PrintScreen)),
            new HotkeySettings(HotkeyType.ActiveWindow,
                new HotkeyInfo(Key.PrintScreen, KeyModifiers.Alt)),
            new HotkeySettings(HotkeyType.ScreenRecorder,
                new HotkeyInfo(Key.PrintScreen, KeyModifiers.Shift)),
            new HotkeySettings(HotkeyType.ScreenRecorderGIF,
                new HotkeyInfo(Key.PrintScreen, KeyModifiers.Control | KeyModifiers.Shift)),
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        UnregisterAllHotkeys();
        _hotkeyService.HotkeyTriggered -= OnHotkeyServiceTriggered;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
