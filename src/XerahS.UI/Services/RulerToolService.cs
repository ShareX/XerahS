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

using Avalonia.Controls;
using XerahS.Core;
using XerahS.RegionCapture;
using XerahS.RegionCapture.Services;

namespace XerahS.UI.Services;

public static class RulerToolService
{
    public static async Task HandleWorkflowAsync(WorkflowType job, Window? owner)
    {
        if (job == WorkflowType.Ruler)
        {
            await ShowRulerAsync();
        }
    }

    private static async Task ShowRulerAsync()
    {
        // TODO: This is a placeholder that will be replaced with full RegionCapture integration
        // For now, we'll document that Ruler requires RegionCapture system enhancement

        // When fully implemented, this should:
        // 1. Create RegionCapture with Mode = Ruler
        // 2. Set QuickCrop = false, UseLightResizeNodes = true
        // 3. Add ruler tick rendering in RegionCapture overlay
        // 4. Show measurement overlay with all metrics

        await Task.CompletedTask;
    }
}
