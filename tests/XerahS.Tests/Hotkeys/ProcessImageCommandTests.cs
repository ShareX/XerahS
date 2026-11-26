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

using Avalonia;
using NUnit.Framework;
using XerahS.Common;
using XerahS.UI.Services;

namespace XerahS.Tests.Hotkeys;

[TestFixture]
public class ProcessImageCommandTests
{
    [Test]
    public void ProcessImageFlag_Contract()
    {
        Assert.That(AppContracts.Cli.TryGetProcessImageArgs(
            ["--process-image", " /tmp/a.png ", " joseph-region "], out string path, out string id, out string? rect), Is.True);
        Assert.That(path, Is.EqualTo("/tmp/a.png"));
        Assert.That(id, Is.EqualTo("joseph-region"));
        Assert.That(rect, Is.Null);

        Assert.That(AppContracts.Cli.TryGetProcessImageArgs(
            ["--process-image", "/tmp/a.png", "wf", "2560,360,1920,1080"], out _, out _, out rect), Is.True);
        Assert.That(rect, Is.EqualTo("2560,360,1920,1080"));

        Assert.That(AppContracts.Cli.TryGetProcessImageArgs(["--process-image", "/tmp/a.png"], out _, out _, out _), Is.False);
        Assert.That(AppContracts.Cli.TryGetProcessImageArgs(["--process-image", " ", "wf"], out _, out _, out _), Is.False);
        Assert.That(AppContracts.Cli.TryGetProcessImageArgs(["--run-workflow", "a", "b"], out _, out _, out _), Is.False);
        Assert.That(AppContracts.Cli.TryGetProcessImageArgs(["/tmp/file.png"], out _, out _, out _), Is.False);

        // A process-image relay must not be mistaken for a run-workflow relay.
        Assert.That(AppContracts.Cli.TryGetRunWorkflowId(["--process-image", "/tmp/a.png", "wf"], out _), Is.False);
    }

    [Test]
    public void EditorPlacement_ParsesMonitorRect()
    {
        Assert.That(EditorPlacement.ParseRect("2560,360,1920,1080"), Is.EqualTo(new PixelRect(2560, 360, 1920, 1080)));
        Assert.That(EditorPlacement.ParseRect("-1920,0,1920,1080"), Is.EqualTo(new PixelRect(-1920, 0, 1920, 1080)));
        Assert.That(EditorPlacement.ParseRect(null), Is.Null);
        Assert.That(EditorPlacement.ParseRect("1,2,3"), Is.Null);
        Assert.That(EditorPlacement.ParseRect("0,0,0,1080"), Is.Null);
        Assert.That(EditorPlacement.ParseRect("a,b,c,d"), Is.Null);
    }
}
