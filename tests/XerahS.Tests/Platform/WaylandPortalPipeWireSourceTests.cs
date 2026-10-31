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
using XerahS.Platform.Linux.Recording;

namespace XerahS.Tests.Platform;

/// <summary>
/// Hyprland window streams only negotiate through the portal's own PipeWire remote; without it
/// pipewiresrc fails with "no more input formats" (reproduced against xdg-desktop-portal-hyprland).
/// </summary>
[TestFixture]
public class WaylandPortalPipeWireSourceTests
{
    [Test]
    public void UsesThePortalRemoteAndPassesTheNodeIdAsPath() =>
        Assert.That(WaylandPortalRecordingService.BuildPipeWireSource(106, 95),
            Is.EqualTo("pipewiresrc fd=95 path=106 do-timestamp=true"));

    [Test]
    public void NeverPassesTheNodeIdAsTargetObject()
    {
        // target-object takes an object name or serial. A portal node ID there matches nothing,
        // and pipewiresrc autoconnects to the default video source: the webcam, not the screen.
        Assert.That(WaylandPortalRecordingService.BuildPipeWireSource(106, 95), Does.Not.Contain("target-object"));
    }

    [TestCase(1935, 1208, 1920, 1080, true)]  // window stream, webcam frames (the reported bug)
    [TestCase(1935, 1208, 2419, 1510, false)] // same window at 1.25x scaling
    [TestCase(3072, 1728, 3840, 2160, false)] // full screen at 1.25x scaling
    [TestCase(0, 0, 1920, 1080, false)]       // portal did not report a size
    public void DetectsAStreamWithADifferentShape(int portalWidth, int portalHeight, int frameWidth, int frameHeight, bool differs) =>
        Assert.That(WaylandPortalRecordingService.StreamShapeDiffers(portalWidth, portalHeight, frameWidth, frameHeight), Is.EqualTo(differs));

    [Test]
    public void KeepsTheLegacyFormWithoutARemote() =>
        Assert.That(WaylandPortalRecordingService.BuildPipeWireSource(106, -1),
            Is.EqualTo("pipewiresrc path=106 do-timestamp=true"));
}
