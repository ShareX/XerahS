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

using ShareX.ImageEditor;
using XerahS.Core;

namespace XerahS.UI.Services;

/// <summary>
/// Resolves and persists region-capture annotation options stored in TaskSettings.
/// </summary>
internal static class RegionCaptureAnnotationOptionsStore
{
    public static EditorOptions GetEditorOptions(string? workflowId = null, WorkflowType? workflowType = null)
    {
        var taskSettings = ResolveTaskSettings(workflowId, workflowType);
        taskSettings.CaptureSettings ??= new TaskSettingsCapture();
        taskSettings.CaptureSettings.RegionCaptureOptions ??= new XerahS.Core.RegionCaptureOptions();
        taskSettings.CaptureSettings.RegionCaptureOptions.AnnotationOptions ??= new EditorOptions();
        return taskSettings.CaptureSettings.RegionCaptureOptions.AnnotationOptions;
    }

    public static void Persist()
    {
        SettingsManager.SaveWorkflowsConfigAsync();
    }

    private static TaskSettings ResolveTaskSettings(string? workflowId, WorkflowType? workflowType)
    {
        if (!string.IsNullOrWhiteSpace(workflowId))
        {
            return SettingsManager.GetWorkflowTaskSettings(workflowId);
        }

        if (workflowType.HasValue)
        {
            var workflow = SettingsManager.GetFirstWorkflow(workflowType.Value);
            if (workflow?.TaskSettings != null)
            {
                return workflow.TaskSettings;
            }
        }

        return SettingsManager.DefaultTaskSettings;
    }
}
