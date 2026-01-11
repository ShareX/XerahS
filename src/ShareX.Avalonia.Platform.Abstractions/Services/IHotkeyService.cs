#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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

namespace XerahS.Platform.Abstractions;

/// <summary>
/// Platform-agnostic interface for global hotkey registration
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Fired when a registered hotkey is triggered
    /// </summary>
    event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

    /// <summary>
    /// Register a global hotkey
    /// </summary>
    /// <param name="hotkeyInfo">Hotkey to register</param>
    /// <returns>True if registration succeeded</returns>
    bool RegisterHotkey(HotkeyInfo hotkeyInfo);

    /// <summary>
    /// Unregister a previously registered hotkey
    /// </summary>
    /// <param name="hotkeyInfo">Hotkey to unregister</param>
    /// <returns>True if unregistration succeeded</returns>
    bool UnregisterHotkey(HotkeyInfo hotkeyInfo);

    /// <summary>
    /// Unregister all hotkeys
    /// </summary>
    void UnregisterAll();

    /// <summary>
    /// Check if a hotkey is currently registered
    /// </summary>
    bool IsRegistered(HotkeyInfo hotkeyInfo);

    /// <summary>
    /// Temporarily suspend all hotkey processing
    /// </summary>
    bool IsSuspended { get; set; }
}

/// <summary>
/// Event args for hotkey trigger events
/// </summary>
public class HotkeyTriggeredEventArgs : EventArgs
{
    /// <summary>
    /// The hotkey that was triggered
    /// </summary>
    public HotkeyInfo HotkeyInfo { get; }

    public HotkeyTriggeredEventArgs(HotkeyInfo hotkeyInfo)
    {
        HotkeyInfo = hotkeyInfo;
    }
}
