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
using XerahS.Common;
using XerahS.Core.Hotkeys;

using HotkeyInfo = XerahS.Platform.Abstractions.HotkeyInfo;

namespace XerahS.Core;

/// <summary>
/// Hotkey configuration bound to a specific task
/// </summary>
// Duplicate HotkeySettings class removed. using XerahS.Core.Hotkeys.HotkeySettings instead.

/// <summary>
/// Workflows configuration storage
/// </summary>
public class WorkflowsConfig : SettingsBase<WorkflowsConfig>
{
    public List<WorkflowSettings> Hotkeys { get; set; } = GetDefaultWorkflowList();

    /// <summary>
    /// Ensure all workflows have valid IDs after loading
    /// </summary>
    public void EnsureWorkflowIds()
    {
        bool needsSave = false;

        if (Hotkeys != null)
        {
            foreach (var workflow in Hotkeys)
            {
                if (string.IsNullOrEmpty(workflow.Id))
                {
                    workflow.EnsureId();
                    needsSave = true;
                }
            }
        }

        // Save if we generated any new IDs
        if (needsSave && !string.IsNullOrEmpty(FilePath))
        {
            Save();
        }
    }

    /// <summary>
    /// Get default hotkey list for ShareX
    /// </summary>
    public static List<WorkflowSettings> GetDefaultWorkflowList()
    {
        var list = new List<WorkflowSettings>();

        // WF01: Full screen capture
        var wf01 = new WorkflowSettings(HotkeyType.PrintScreen, new HotkeyInfo(Key.PrintScreen))
        {
            Name = "Full screen capture"
        };
        wf01.TaskSettings.CaptureSettings.UseModernCapture = true;
        list.Add(wf01);

        // WF02: Active window capture
        var wf02 = new WorkflowSettings(HotkeyType.ActiveWindow, new HotkeyInfo(Key.PrintScreen, KeyModifiers.Alt))
        {
            Name = "Active window capture"
        };
        wf02.TaskSettings.CaptureSettings.UseModernCapture = true;
        list.Add(wf02);

        // WF03: Record screen using GDI
        var wf03 = new WorkflowSettings(HotkeyType.ScreenRecorder, new HotkeyInfo(Key.PrintScreen, KeyModifiers.Shift))
        {
            Name = "Record screen using GDI"
        };
        wf03.TaskSettings.CaptureSettings.UseModernCapture = false;
        wf03.TaskSettings.CaptureSettings.ScreenRecordingSettings.RecordingBackend = XerahS.ScreenCapture.ScreenRecording.RecordingBackend.GDI;
        list.Add(wf03);

        // WF04: Record screen for game
        var wf04 = new WorkflowSettings(HotkeyType.ScreenRecorder, new HotkeyInfo(Key.PrintScreen, KeyModifiers.Control | KeyModifiers.Shift))
        {
            Name = "Record screen for game"
        };
        wf04.TaskSettings.CaptureSettings.UseModernCapture = true;
        wf04.TaskSettings.CaptureSettings.ScreenRecordingSettings.RecordingIntent = XerahS.ScreenCapture.ScreenRecording.RecordingIntent.Game;
        list.Add(wf04);

        return list;
    }
}
