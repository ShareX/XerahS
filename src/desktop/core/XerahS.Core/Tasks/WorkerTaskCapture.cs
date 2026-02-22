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
    /// WorkerTask partial class for screen capture operations
    /// </summary>
    public partial class WorkerTask
    {
        /// <summary>
        /// Applies capture start delay if configured for the workflow.
        /// </summary>
        private async Task<bool> ApplyCaptureStartDelayAsync(TaskSettings taskSettings, string category, double delaySeconds, CancellationToken token)
        {
            if (delaySeconds <= 0)
            {
                return true;
            }

            var delayMs = (int)Math.Round(delaySeconds * 1000, MidpointRounding.AwayFromZero);
            var workflowId = string.IsNullOrWhiteSpace(taskSettings.WorkflowId) ? "none" : taskSettings.WorkflowId;
            TroubleshootingHelper.Log(taskSettings.Job.ToString(), "CAPTURE_DELAY", $"WorkflowId={workflowId}, Category={category}, DelaySeconds={delaySeconds:F3}, DelayMs={delayMs}");

            try
            {
                await Task.Delay(delayMs, token);
                TroubleshootingHelper.Log(taskSettings.Job.ToString(), "CAPTURE_DELAY", $"WorkflowId={workflowId}, Category={category}, DelayCompleted=true");
                return true;
            }
            catch (OperationCanceledException)
            {
                TroubleshootingHelper.Log(taskSettings.Job.ToString(), "CAPTURE_DELAY", $"WorkflowId={workflowId}, Category={category}, DelayCancelled=true");
                Status = TaskStatus.Stopped;
                OnStatusChanged();
                return false;
            }
        }
    }
}
