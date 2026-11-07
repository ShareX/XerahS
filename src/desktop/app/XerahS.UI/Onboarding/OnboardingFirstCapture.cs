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

using Avalonia.Threading;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;

namespace XerahS.UI.Onboarding;

/// <summary>Starts the region-capture workflow right after the wizard closes, so a new user's first minute ends in a real capture.</summary>
public static class OnboardingFirstCapture
{
    public static WorkflowSettings? FindRegionWorkflow() =>
        SettingsManager.WorkflowsConfig?.Hotkeys?
            .FirstOrDefault(w => w.Enabled && w.Job is WorkflowType.RectangleRegion or WorkflowType.RectangleTransparent);

    public static void Start()
    {
        WorkflowSettings? workflow = FindRegionWorkflow();
        if (workflow == null)
        {
            DebugHelper.WriteLine("[Onboarding] First capture skipped: no region workflow.");
            return;
        }

        Dispatcher.UIThread.Post(async () =>
        {
            // Let the wizard window finish closing so it is not part of the capture.
            await Task.Delay(500);
            if (Avalonia.Application.Current is not App app || !app.TryRunWorkflow(workflow))
            {
                DebugHelper.WriteLine("[Onboarding] First capture skipped: workflow orchestrator not ready.");
            }
        }, DispatcherPriority.Background);
    }
}
