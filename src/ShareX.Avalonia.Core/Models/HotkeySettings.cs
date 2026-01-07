#nullable disable
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

using Avalonia.Input;
using ShareX.Ava.Common;
using System.Collections.Generic;
using ShareX.Ava.Core.Hotkeys;
using ShareX.Ava.Platform.Abstractions;

using HotkeyInfo = ShareX.Ava.Platform.Abstractions.HotkeyInfo;

namespace ShareX.Ava.Core;

/// <summary>
/// Hotkey configuration bound to a specific task
/// </summary>
// Duplicate HotkeySettings class removed. Using ShareX.Ava.Core.Hotkeys.HotkeySettings instead.

/// <summary>
/// Workflows configuration storage
/// </summary>
public class WorkflowsConfig : SettingsBase<WorkflowsConfig>
{
    public List<WorkflowSettings> Hotkeys { get; set; } = GetDefaultWorkflowList();

    /// <summary>
    /// Get default hotkey list for ShareX
    /// </summary>
    public static List<WorkflowSettings> GetDefaultWorkflowList()
    {
        return new List<WorkflowSettings>
        {
            new WorkflowSettings(HotkeyType.PrintScreen, new HotkeyInfo(Key.PrintScreen)),
            new WorkflowSettings(HotkeyType.RectangleRegion, new HotkeyInfo(Key.PrintScreen, KeyModifiers.Control)),
            new WorkflowSettings(HotkeyType.ActiveWindow, new HotkeyInfo(Key.PrintScreen, KeyModifiers.Alt)),
            new WorkflowSettings(HotkeyType.CustomWindow, new HotkeyInfo()),
            new WorkflowSettings(HotkeyType.ScreenRecorder, new HotkeyInfo(Key.PrintScreen, KeyModifiers.Shift)),
            new WorkflowSettings(HotkeyType.ScreenRecorderGIF, new HotkeyInfo(Key.PrintScreen, KeyModifiers.Control | KeyModifiers.Shift)),
        };
    }
}
