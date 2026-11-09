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

                if (workflow.TaskSettings == null)
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

                string originalId = workflow.Id;
                string originalWorkflowId = workflow.TaskSettings?.WorkflowId;

                workflow.EnsureId();

                if (!string.Equals(originalId, workflow.Id, StringComparison.Ordinal) ||
                    !string.Equals(originalWorkflowId, workflow.TaskSettings?.WorkflowId, StringComparison.Ordinal))
                {
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
        bool preferTransparentRegionWorkflow = ShouldUseTransparentRegionWorkflowByDefault();

        // WF01: Full screen capture (Ctrl + Shift + F)
        var wf01 = new WorkflowSettings(WorkflowType.PrintScreen, new HotkeyInfo(Key.F, KeyModifiers.Control | KeyModifiers.Shift));
        wf01.TaskSettings.Description = "Full screen capture";
        wf01.TaskSettings.CaptureSettings.UseModernCapture = true;
        list.Add(wf01);

        // WF02: Active window capture (Ctrl + Shift + A)
        var wf02 = new WorkflowSettings(WorkflowType.ActiveWindow, new HotkeyInfo(Key.A, KeyModifiers.Control | KeyModifiers.Shift));
        wf02.TaskSettings.Description = "Active window capture";
        wf02.TaskSettings.CaptureSettings.UseModernCapture = true;
        list.Add(wf02);

        // WF03: Region capture (Ctrl + Shift + X)
        var regionWorkflowType = preferTransparentRegionWorkflow ? WorkflowType.RectangleTransparent : WorkflowType.RectangleRegion;
        var wf03 = new WorkflowSettings(regionWorkflowType, new HotkeyInfo(Key.X, KeyModifiers.Control | KeyModifiers.Shift));
        wf03.TaskSettings.Description = preferTransparentRegionWorkflow ? "Region capture (Transparent)" : "Region capture";
        wf03.TaskSettings.CaptureSettings.UseModernCapture = true;
        list.Add(wf03);

        // WF04: Record screen (Ctrl + Shift + R). The GDI backend is Windows-only, so the label must not say GDI elsewhere.
        var wf04 = new WorkflowSettings(WorkflowType.ScreenRecorder, new HotkeyInfo(Key.R, KeyModifiers.Control | KeyModifiers.Shift));
        wf04.TaskSettings.Description = OperatingSystem.IsWindows() ? "Record screen using GDI" : "Record screen";
        wf04.TaskSettings.CaptureSettings.UseModernCapture = false;
        wf04.TaskSettings.CaptureSettings.ScreenRecordingSettings.RecordingBackend = XerahS.RegionCapture.ScreenRecording.RecordingBackend.GDI;
        list.Add(wf04);

        // WF05: Record the active window (Ctrl + Shift + G)
        var wf05 = new WorkflowSettings(WorkflowType.ScreenRecorderActiveWindow, new HotkeyInfo(Key.G, KeyModifiers.Control | KeyModifiers.Shift));
        wf05.TaskSettings.Description = "Record active window";
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

    private static bool ShouldUseTransparentRegionWorkflowByDefault()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        string[] desktopHints =
        {
            Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? string.Empty,
            Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP") ?? string.Empty,
            Environment.GetEnvironmentVariable("DESKTOP_SESSION") ?? string.Empty
        };

        foreach (string hint in desktopHints)
        {
            foreach (string token in hint.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string normalized = token.ToUpperInvariant();
                if (normalized.Contains("GNOME", StringComparison.Ordinal) ||
                    normalized.Contains("UBUNTU", StringComparison.Ordinal) ||
                    normalized.Contains("UNITY", StringComparison.Ordinal) ||
                    normalized.Contains("BUDGIE", StringComparison.Ordinal) ||
                    normalized.Contains("PANTHEON", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
