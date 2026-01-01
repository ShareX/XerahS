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

using ShareX.Ava.Common;
using ShareX.Ava.Platform.Abstractions;
using ShareX.Ava.Core;
using Avalonia.Input;

using HotkeyInfo = ShareX.Ava.Platform.Abstractions.HotkeyInfo;

namespace ShareX.Ava.Core.Hotkeys;

/// <summary>
/// Links a hotkey binding to an action type
/// </summary>
public class HotkeySettings
{
    /// <summary>
    /// The key binding for this hotkey
    /// </summary>
    public HotkeyInfo HotkeyInfo { get; set; }

    /// <summary>
    /// The action to execute when this hotkey is triggered
    /// </summary>
    /// <summary>
    /// The action to execute when this hotkey is triggered.
    /// Proxies to TaskSettings.Job.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public HotkeyType Job 
    { 
        get => TaskSettings.Job;
        set => TaskSettings.Job = value;
    }

    /// <summary>
    /// Configuration for the task to execute
    /// </summary>
    public TaskSettings TaskSettings { get; set; }

    /// <summary>
    /// Optional display name for this hotkey
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Whether this hotkey is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    public HotkeySettings()
    {
        HotkeyInfo = new HotkeyInfo();
        TaskSettings = new TaskSettings();
    }

    public HotkeySettings(HotkeyType job, HotkeyInfo hotkeyInfo) : this()
    {
        TaskSettings.Job = job;
        HotkeyInfo = hotkeyInfo;
    }

    public HotkeySettings(HotkeyType job, Key key) : this()
    {
        TaskSettings.Job = job;
        HotkeyInfo = new HotkeyInfo(key);
    }

    public override string ToString()
    {
        return $"{Job}: {HotkeyInfo}";
    }
}
