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

#nullable disable
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
    public TaskSettings DefaultTaskSettings { get; set; } = new TaskSettings { Job = WorkflowType.None };

    /// <summary>
    /// Ensure workflows are valid after loading.
    /// </summary>
    public void EnsureWorkflowIds()
    {
        bool needsSave = false;

        if (DefaultTaskSettings == null)
        {
            DefaultTaskSettings = new TaskSettings { Job = WorkflowType.None };
            needsSave = true;
        }
        else if (DefaultTaskSettings.Job != WorkflowType.None)
        {
            DefaultTaskSettings.Job = WorkflowType.None;
            needsSave = true;
        }

        if (Hotkeys == null)
        {
            Hotkeys = new List<WorkflowSettings>();
            needsSave = true;
        }
        else
        {
            for (int i = Hotkeys.Count - 1; i >= 0; i--)
            {
                var workflow = Hotkeys[i];
                if (workflow == null)
                {
                    Hotkeys.RemoveAt(i);
                    needsSave = true;
                    continue;
                }

                if (workflow.Job == WorkflowType.None)
                {
                    if (workflow.TaskSettings != null)
                    {
                        DefaultTaskSettings = workflow.TaskSettings;
                        DefaultTaskSettings.Job = WorkflowType.None;
                        DefaultTaskSettings.WorkflowId = string.Empty;
                    }

                    Hotkeys.RemoveAt(i);
                    needsSave = true;
                    continue;
                }

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
        var wf01 = new WorkflowSettings(WorkflowType.PrintScreen, new HotkeyInfo(Key.PrintScreen));
        wf01.TaskSettings.Description = "Full screen capture";
        wf01.TaskSettings.CaptureSettings.UseModernCapture = true;
        list.Add(wf01);

        // WF02: Active window capture
        var wf02 = new WorkflowSettings(WorkflowType.ActiveWindow, new HotkeyInfo(Key.PrintScreen, KeyModifiers.Alt));
        wf02.TaskSettings.Description = "Active window capture";
        wf02.TaskSettings.CaptureSettings.UseModernCapture = true;
        list.Add(wf02);

        // WF03: Region capture
        var wf03 = new WorkflowSettings(WorkflowType.RectangleRegion, new HotkeyInfo(Key.PrintScreen, KeyModifiers.Control));
        wf03.TaskSettings.Description = "Region capture";
        wf03.TaskSettings.CaptureSettings.UseModernCapture = true;
        list.Add(wf03);

        // WF04: Record screen using GDI
        var wf04 = new WorkflowSettings(WorkflowType.ScreenRecorder, new HotkeyInfo(Key.PrintScreen, KeyModifiers.Shift));
        wf04.TaskSettings.Description = "Record screen using GDI";
        wf04.TaskSettings.CaptureSettings.UseModernCapture = false;
        wf04.TaskSettings.CaptureSettings.ScreenRecordingSettings.RecordingBackend = XerahS.RegionCapture.ScreenRecording.RecordingBackend.GDI;
        list.Add(wf04);

        // WF05: Record screen for game
        var wf05 = new WorkflowSettings(WorkflowType.ScreenRecorderActiveWindow, new HotkeyInfo(Key.PrintScreen, KeyModifiers.Control | KeyModifiers.Shift));
        wf05.TaskSettings.Description = "Record screen for game";
        wf05.TaskSettings.CaptureSettings.UseModernCapture = true;
        wf05.TaskSettings.CaptureSettings.ScreenRecordingSettings.RecordingIntent = XerahS.RegionCapture.ScreenRecording.RecordingIntent.Game;
        list.Add(wf05);

        // WF05b: Pause screen recording
        var wf05b = new WorkflowSettings(WorkflowType.PauseScreenRecording, new HotkeyInfo());
        wf05b.TaskSettings.Description = "Pause screen recording";
        list.Add(wf05b);

        // WF05c: Abort screen recording
        var wf05c = new WorkflowSettings(WorkflowType.AbortScreenRecording, new HotkeyInfo());
        wf05c.TaskSettings.Description = "Abort screen recording";
        list.Add(wf05c);

        // WF06: File upload
        var wf06 = new WorkflowSettings(WorkflowType.FileUpload, new HotkeyInfo());
        wf06.TaskSettings.Description = "File upload";
        list.Add(wf06);

        return list;
    }
}
