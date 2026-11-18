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

using System.Collections.Generic;
using Avalonia.Input;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Input;

/// <summary>
/// Compares incoming raw key events against configured hotkeys. Matching requires
/// the main key to map to the same evdev code and the active modifier set to match
/// exactly (no extra modifiers). A per-hotkey debounce suppresses key auto-repeat.
/// </summary>
internal sealed class EvdevHotkeyMatcher
{
    private const long DefaultDebounceMs = 250;

    private readonly Dictionary<ushort, long> _lastFireTicks = new();
    private readonly Lock _lock = new();

    public long DebounceMs { get; set; } = DefaultDebounceMs;

    /// <summary>
    /// Pure match test: does the given key/modifier combination satisfy the hotkey?
    /// </summary>
    public static bool IsMatch(HotkeyInfo hotkey, ushort keyCode, KeyModifiers currentModifiers)
    {
        if (hotkey == null || !hotkey.IsValid)
        {
            return false;
        }

        if (!EvdevKeyMap.TryGetEvdevCode(hotkey.Key, out var hotkeyCode))
        {
            return false;
        }

        if (hotkeyCode != keyCode)
        {
            return false;
        }

        // Exact modifier match rejects both missing and extra modifiers.
        return hotkey.Modifiers == currentModifiers;
    }

    /// <summary>
    /// Match test with per-hotkey debounce. Returns true at most once per debounce
    /// window so held keys (auto-repeat) do not fire repeatedly.
    /// </summary>
    public bool TryMatch(HotkeyInfo hotkey, ushort keyCode, KeyModifiers currentModifiers, long nowTicksMs)
    {
        if (!IsMatch(hotkey, keyCode, currentModifiers))
        {
            return false;
        }

        lock (_lock)
        {
            if (_lastFireTicks.TryGetValue(hotkey.Id, out var last) && nowTicksMs - last < DebounceMs)
            {
                return false;
            }

            _lastFireTicks[hotkey.Id] = nowTicksMs;
        }

        return true;
    }

    public void ResetDebounce()
    {
        lock (_lock)
        {
            _lastFireTicks.Clear();
        }
    }

    public void Forget(ushort hotkeyId)
    {
        lock (_lock)
        {
            _lastFireTicks.Remove(hotkeyId);
        }
    }
}
