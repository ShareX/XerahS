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
using XerahS.Common;
using XerahS.Core;

namespace XerahS.Tests.Helpers;

public class TaskHelpersCaptureDelayTests
{
    [Test]
    public void GetCaptureStartDelaySeconds_ScreenCapture_ReturnsScreenshotDelay()
    {
        var settings = new TaskSettings
        {
            Job = WorkflowType.PrintScreen,
            CaptureSettings = new TaskSettingsCapture
            {
                ScreenshotDelay = 2.5m
            }
        };

        var delaySeconds = TaskHelpers.GetCaptureStartDelaySeconds(settings, out var category);

        Assert.That(category, Is.EqualTo(EnumExtensions.WorkflowType_Category_ScreenCapture));
        Assert.That(delaySeconds, Is.EqualTo(2.5d).Within(0.0001d));
    }

    [Test]
    public void GetCaptureStartDelaySeconds_ScreenRecord_ReturnsStartDelay()
    {
        var settings = new TaskSettings
        {
            Job = WorkflowType.ScreenRecorderActiveWindow,
            CaptureSettings = new TaskSettingsCapture
            {
                ScreenRecordStartDelay = 3.5f
            }
        };

        var delaySeconds = TaskHelpers.GetCaptureStartDelaySeconds(settings, out var category);

        Assert.That(category, Is.EqualTo(EnumExtensions.WorkflowType_Category_ScreenRecord));
        Assert.That(delaySeconds, Is.EqualTo(3.5d).Within(0.0001d));
    }

    [Test]
    public void GetCaptureStartDelaySeconds_StopRecording_ReturnsZero()
    {
        var settings = new TaskSettings
        {
            Job = WorkflowType.StopScreenRecording,
            CaptureSettings = new TaskSettingsCapture
            {
                ScreenRecordStartDelay = 4f
            }
        };

        var delaySeconds = TaskHelpers.GetCaptureStartDelaySeconds(settings, out var category);

        Assert.That(category, Is.EqualTo(EnumExtensions.WorkflowType_Category_ScreenRecord));
        Assert.That(delaySeconds, Is.EqualTo(0d));
    }

    [Test]
    public void GetCaptureStartDelaySeconds_UploadJob_ReturnsZero()
    {
        var settings = new TaskSettings
        {
            Job = WorkflowType.ClipboardUpload
        };

        var delaySeconds = TaskHelpers.GetCaptureStartDelaySeconds(settings, out var category);

        Assert.That(category, Is.EqualTo(EnumExtensions.WorkflowType_Category_Upload));
        Assert.That(delaySeconds, Is.EqualTo(0d));
    }
}

