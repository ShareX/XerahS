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

using XerahS.Common;
using XerahS.Core.Helpers;

namespace XerahS.Core.Tasks
{
    /// <summary>
    /// WorkerTask partial class for handling tool workflows (ColorPicker, QRCode, etc.)
    /// </summary>
    public partial class WorkerTask
    {
        /// <summary>
        /// Delegate to handle tool workflow execution (ColorPicker, QRCode, etc.).
        /// Set by the UI layer to dispatch tool dialogs.
        /// </summary>
        public static Func<WorkflowType, TaskSettings, Task>? HandleToolWorkflowCallback { get; set; }

        /// <summary>
        /// Handles tool workflow execution (ColorPicker, QRCode, etc.)
        /// Tools require UI context, so this method delegates to the callback set by the UI layer.
        /// </summary>
        private async Task HandleToolWorkflowAsync(CancellationToken token)
        {
            TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "TOOL_WORKFLOW", "HandleToolWorkflowAsync Entry");

            if (HandleToolWorkflowCallback != null)
            {
                TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "TOOL_WORKFLOW", $"Executing tool workflow via callback: {Info.TaskSettings.Job}");
                await HandleToolWorkflowCallback(Info.TaskSettings.Job, Info.TaskSettings);
            }
            else
            {
                DebugHelper.WriteLine($"Tool workflow callback not set for job: {Info.TaskSettings.Job}");
                TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "TOOL_WORKFLOW", "WARNING: HandleToolWorkflowCallback is null");
            }

            TroubleshootingHelper.Log(Info.TaskSettings.Job.ToString(), "TOOL_WORKFLOW", "HandleToolWorkflowAsync Complete");
        }
    }
}
