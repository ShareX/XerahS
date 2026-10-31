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

using NUnit.Framework;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.Tests.Xip0052;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.RegionCapture;

[TestFixture, NonParallelizable]
public sealed class RecordingViewModelAudioRoutingTests
{
    [TestCase(false), TestCase(true)]
    public void SelectingAudio_PreservesExplicitBackendPreference(bool forceFfmpeg)
    {
        var config = SettingsManager.WorkflowsConfig;
        try
        {
            var workflow = new WorkflowSettings(WorkflowType.ScreenRecorder, new HotkeyInfo());
            workflow.TaskSettings.CaptureSettings.ScreenRecordingSettings.ForceFFmpeg = forceFfmpeg;
            SettingsManager.WorkflowsConfig = new WorkflowsConfig { Hotkeys = [workflow] };
            using var viewModel = new RecordingViewModel(new FakeScreenRecordingCoordinator());
            viewModel.CaptureSystemAudio = true;
            viewModel.CaptureMicrophone = true;
            var settings = workflow.TaskSettings.CaptureSettings.ScreenRecordingSettings;
            Assert.Multiple(() =>
            {
                Assert.That(settings.CaptureSystemAudio, Is.True);
                Assert.That(settings.CaptureMicrophone, Is.True);
                Assert.That(settings.ForceFFmpeg, Is.EqualTo(forceFfmpeg));
            });
        }
        finally { SettingsManager.WorkflowsConfig = config; }
    }
}
