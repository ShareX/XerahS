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

using System.Drawing;
using NUnit.Framework;
using XerahS.Platform.Linux;

namespace XerahS.Tests.Platform.Linux;

[TestFixture]
public class LinuxWindowServiceTests
{
    [Test]
    public void ApplyFrameExtents_ExpandsClientBoundsToOuterFrame()
    {
        var clientBounds = new Rectangle(100, 200, 800, 600);

        var frameBounds = LinuxWindowService.ApplyFrameExtents(clientBounds, left: 8, right: 8, top: 30, bottom: 8);

        Assert.That(frameBounds, Is.EqualTo(new Rectangle(92, 170, 816, 638)));
    }

    [Test]
    public void ApplyFrameExtents_IgnoresExtentsWhenOuterBoundsWouldOverflow()
    {
        var clientBounds = new Rectangle(100, 200, 800, 600);

        var frameBounds = LinuxWindowService.ApplyFrameExtents(clientBounds, left: int.MaxValue, right: int.MaxValue, top: 30, bottom: 8);

        Assert.That(frameBounds, Is.EqualTo(clientBounds));
    }

    [Test]
    public void ApplyFrameExtents_IgnoresExtentsWhenOuterOriginWouldOverflow()
    {
        var clientBounds = new Rectangle(int.MinValue + 10, int.MinValue + 20, 800, 600);

        var frameBounds = LinuxWindowService.ApplyFrameExtents(clientBounds, left: 20, right: 8, top: 30, bottom: 8);

        Assert.That(frameBounds, Is.EqualTo(clientBounds));
    }

    [Test]
    public void ContainsExcludedWindowTypeName_SkipsDesktopAndDockWindows()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                LinuxWindowService.ContainsExcludedWindowTypeName(["_NET_WM_WINDOW_TYPE_DESKTOP"]),
                Is.True);
            Assert.That(
                LinuxWindowService.ContainsExcludedWindowTypeName(["_NET_WM_WINDOW_TYPE_DOCK"]),
                Is.True);
            Assert.That(
                LinuxWindowService.ContainsExcludedWindowTypeName(["_NET_WM_WINDOW_TYPE_DIALOG", "_NET_WM_WINDOW_TYPE_NORMAL"]),
                Is.False);
        });
    }

    [Test]
    public void ContainsExcludedWindowStateName_SkipsHiddenAndPagerSuppressedWindows()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                LinuxWindowService.ContainsExcludedWindowStateName(["_NET_WM_STATE_HIDDEN"]),
                Is.True);
            Assert.That(
                LinuxWindowService.ContainsExcludedWindowStateName(["_NET_WM_STATE_SKIP_TASKBAR"]),
                Is.True);
            Assert.That(
                LinuxWindowService.ContainsExcludedWindowStateName(["_NET_WM_STATE_FOCUSED"]),
                Is.False);
        });
    }

    [Test]
    public void IsInvalidWindowHandle_RejectsZeroAndPointerRoot()
    {
        Assert.Multiple(() =>
        {
            Assert.That(NativeMethods.IsInvalidWindowHandle(IntPtr.Zero), Is.True);
            Assert.That(NativeMethods.IsInvalidWindowHandle(NativeMethods.PointerRoot), Is.True);
            Assert.That(NativeMethods.IsInvalidWindowHandle(new IntPtr(0x200001)), Is.False);
        });
    }

    [Test]
    public void X11WindowQueries_HandlePointerRootAndInvalidWindowIdsWithoutCrashing()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Ignore("Linux-only X11 safety check.");
        }

        using var service = new LinuxWindowService();

        IntPtr foreground = service.GetForegroundWindow();
        IntPtr bogusHandle = new(0x7FFFFFFF);

        Assert.Multiple(() =>
        {
            Assert.That(foreground, Is.Not.EqualTo(NativeMethods.PointerRoot));

            Assert.That(service.GetWindowText(NativeMethods.PointerRoot), Is.Empty);
            Assert.That(service.GetWindowBounds(NativeMethods.PointerRoot), Is.EqualTo(Rectangle.Empty));
            Assert.That(service.IsWindowVisible(NativeMethods.PointerRoot), Is.False);
            Assert.That(service.SetForegroundWindow(NativeMethods.PointerRoot), Is.False);
            Assert.That(service.ShowWindow(NativeMethods.PointerRoot, 1), Is.False);
            Assert.That(service.SetWindowPos(NativeMethods.PointerRoot, IntPtr.Zero, 0, 0, 100, 100, 0), Is.False);

            // Forces real X11 BadWindow replies if XWayland/X11 is active; must not abort via _XDefaultError.
            Assert.That(service.GetWindowText(bogusHandle), Is.Empty);
            Assert.That(service.GetWindowClassName(bogusHandle), Is.Empty);
            Assert.That(service.GetWindowBounds(bogusHandle), Is.EqualTo(Rectangle.Empty));
            Assert.That(service.IsWindowVisible(bogusHandle), Is.False);
        });
    }
}
