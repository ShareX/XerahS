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
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;

namespace XerahS.UI.Services;

public static class AutoCaptureToolService
{
    private static AutoCaptureWindow? _window;

    public static Task HandleWorkflowAsync(WorkflowType job, Window? owner)
    {
        switch (job)
        {
            case WorkflowType.AutoCapture:
                ShowConfigWindow(owner);
                break;
            case WorkflowType.StartAutoCapture:
                ShowConfigWindow(owner);
                StartCapture();
                break;
            case WorkflowType.StopAutoCapture:
                StopCapture();
                break;
        }

        return Task.CompletedTask;
    }

    private static void ShowConfigWindow(Window? owner)
    {
        if (_window != null)
        {
            try
            {
                _window.Show();
                _window.Activate();
                return;
            }
            catch
            {
                // Window was disposed/closed, create new one
                _window = null;
            }
        }

        var viewModel = new AutoCaptureViewModel();
        _window = new AutoCaptureWindow();
        _window.Initialize(viewModel);

        _window.Closed += (_, _) =>
        {
            _window = null;
        };

        _window.Show();

        DebugHelper.WriteLine("AutoCapture: Config window shown.");
    }

    private static void StartCapture()
    {
        if (_window?.ViewModel == null) return;

        if (!_window.ViewModel.IsRunning)
        {
            _window.ViewModel.StartStopCommand.Execute(null);
        }
    }

    private static void StopCapture()
    {
        if (_window?.ViewModel == null) return;

        if (_window.ViewModel.IsRunning)
        {
            _window.ViewModel.StopCapture();
        }
    }
}
