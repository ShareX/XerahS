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
using XerahS.Platform.Linux.Input.Evdev;

namespace XerahS.Platform.Linux.Input;

/// <summary>
/// Tracks which modifier keys are currently held down across all listened input
/// devices. Because evdev delivers raw key-down/key-up events with no implicit
/// modifier state, XerahS must maintain it explicitly.
/// </summary>
internal sealed class ModifierStateTracker
{
    private readonly HashSet<ushort> _pressedModifierCodes = new();
    private readonly Lock _lock = new();

    /// <summary>Records a key-down for the given evdev code if it is a modifier.</summary>
    public void OnKeyDown(ushort code)
    {
        if (!InputEventCodes.IsModifierKey(code))
        {
            return;
        }

        lock (_lock)
        {
            _pressedModifierCodes.Add(code);
        }
    }

    /// <summary>Records a key-up for the given evdev code if it is a modifier.</summary>
    public void OnKeyUp(ushort code)
    {
        if (!InputEventCodes.IsModifierKey(code))
        {
            return;
        }

        lock (_lock)
        {
            _pressedModifierCodes.Remove(code);
        }
    }

    /// <summary>Clears all tracked modifier state (e.g. on focus loss or restart).</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _pressedModifierCodes.Clear();
        }
    }

    /// <summary>The currently active modifier flags derived from pressed modifier keys.</summary>
    public KeyModifiers CurrentModifiers
    {
        get
        {
            var result = KeyModifiers.None;
            lock (_lock)
            {
                foreach (var code in _pressedModifierCodes)
                {
                    result |= EvdevKeyMap.GetModifierFlag(code);
                }
            }

            return result;
        }
    }
}
