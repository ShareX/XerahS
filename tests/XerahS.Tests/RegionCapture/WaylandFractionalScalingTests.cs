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
using SkiaSharp;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Services;
using XerahS.RegionCapture.Services;
using CapturePixelPoint = XerahS.RegionCapture.Models.PixelPoint;
using CapturePixelRect = XerahS.RegionCapture.Models.PixelRect;
using DrawingRectangle = System.Drawing.Rectangle;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
public class WaylandFractionalScalingTests
{
    [Test]
    public void ParseHyprlandMonitorsJson_FractionalScaleWithReservedBar_ComputesPhysicalLogicalAndWorkingArea()
    {
        const string json = """
        [
          {
            "id": 0,
            "name": "eDP-1",
            "description": "Apple Computer Inc iMac 9ACC64B374424",
            "make": "Apple Computer Inc",
            "model": "iMac",
            "serial": "9ACC64B374424",
            "width": 3840,
            "height": 2160,
            "refreshRate": 59.997,
            "x": 0,
            "y": 0,
            "reserved": [0, 26, 0, 0],
            "scale": 1.25,
            "transform": 0,
            "focused": true,
            "disabled": false
          }
        ]
        """;

        var monitors = LinuxScreenService.ParseHyprlandMonitorsJson(json);

        Assert.That(monitors, Has.Count.EqualTo(1));
        var m = monitors[0];
        Assert.Multiple(() =>
        {
            Assert.That(m.Name, Is.EqualTo("eDP-1"));
            Assert.That(m.ScaleFactor, Is.EqualTo(1.25).Within(0.0001));
            Assert.That(m.PhysicalBounds, Is.EqualTo(new DrawingRectangle(0, 0, 3840, 2160)));
            Assert.That(m.LogicalBounds, Is.EqualTo(new DrawingRectangle(0, 0, 3072, 1728)));
            Assert.That(m.WorkingAreaLogical, Is.EqualTo(new DrawingRectangle(0, 26, 3072, 1702)));
            Assert.That(m.WorkingAreaPhysical, Is.EqualTo(new DrawingRectangle(0, 32, 3840, 2128)));
            Assert.That(m.IsPrimary, Is.True);
            Assert.That(m.Source, Is.EqualTo("hyprctl"));
        });
    }

    [Test]
    public void ParseHyprlandMonitorsJson_RotatedAndMixedDpi_StacksPhysicalOriginsWithoutOverlap()
    {
        const string json = """
        [
          {
            "id": 0,
            "name": "eDP-1",
            "width": 3840,
            "height": 2160,
            "x": 0,
            "y": 0,
            "reserved": [0, 0, 0, 0],
            "scale": 1.5,
            "transform": 0,
            "focused": true,
            "disabled": false
          },
          {
            "id": 1,
            "name": "DP-1",
            "width": 1920,
            "height": 1080,
            "x": 2560,
            "y": 0,
            "reserved": [0, 0, 0, 0],
            "scale": 1.0,
            "transform": 1,
            "focused": false,
            "disabled": false
          }
        ]
        """;

        var monitors = LinuxScreenService.ParseHyprlandMonitorsJson(json);

        Assert.That(monitors, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(monitors[0].PhysicalBounds, Is.EqualTo(new DrawingRectangle(0, 0, 3840, 2160)));
            Assert.That(monitors[1].PhysicalBounds, Is.EqualTo(new DrawingRectangle(3840, 0, 1080, 1920)));
            Assert.That(monitors[1].LogicalBounds, Is.EqualTo(new DrawingRectangle(2560, 0, 1080, 1920)));
            Assert.That(monitors[0].IsPrimary, Is.True);
            Assert.That(monitors[1].IsPrimary, Is.False);
        });
    }

    [Test]
    public void ParseSwayAndKScreenDoctorJson_ExtractsScaleAndBounds()
    {
        const string swayJson = """
        [
          {
            "name": "HDMI-A-1",
            "make": "Dell",
            "model": "U2723QE",
            "serial": "ABC",
            "active": true,
            "focused": true,
            "primary": true,
            "scale": 1.25,
            "transform": "normal",
            "rect": { "x": 0, "y": 0, "width": 3072, "height": 1728 },
            "current_mode": { "width": 3840, "height": 2160, "refresh": 60000 }
          }
        ]
        """;

        const string kdeJson = """
        {
          "outputs": [
            {
              "name": "eDP-1",
              "enabled": true,
              "connected": true,
              "priority": 1,
              "scale": 1.25,
              "rotation": 1,
              "pos": { "x": 0, "y": 0 },
              "currentModeId": "1",
              "modes": [
                { "id": "1", "size": { "width": 3840, "height": 2160 }, "refreshRate": 60.0 }
              ]
            }
          ]
        }
        """;

        var swayMonitors = LinuxScreenService.ParseSwayOutputsJson(swayJson);
        var kdeMonitors = LinuxScreenService.ParseKScreenDoctorJson(kdeJson);

        Assert.That(swayMonitors, Has.Count.EqualTo(1));
        Assert.That(swayMonitors[0].ScaleFactor, Is.EqualTo(1.25).Within(0.0001));
        Assert.That(swayMonitors[0].PhysicalBounds, Is.EqualTo(new DrawingRectangle(0, 0, 3840, 2160)));
        Assert.That(swayMonitors[0].RefreshRateHz, Is.EqualTo(60.0).Within(0.01));

        Assert.That(kdeMonitors, Has.Count.EqualTo(1));
        Assert.That(kdeMonitors[0].ScaleFactor, Is.EqualTo(1.25).Within(0.0001));
        Assert.That(kdeMonitors[0].PhysicalBounds, Is.EqualTo(new DrawingRectangle(0, 0, 3840, 2160)));
        Assert.That(kdeMonitors[0].LogicalBounds, Is.EqualTo(new DrawingRectangle(0, 0, 3072, 1728)));
        Assert.That(kdeMonitors[0].IsPrimary, Is.True);
    }

    [Test]
    public void ResolveWaylandScreenMetrics_UsesCompositorScaleAndInsets_EvenWhenAvaloniaReportsScalingOne()
    {
        var platformScreens = new[]
        {
            new ScreenInfo
            {
                DeviceName = "eDP-1",
                Bounds = new DrawingRectangle(0, 0, 3840, 2160),
                WorkingArea = new DrawingRectangle(0, 32, 3840, 2128),
                ScaleFactor = 1.25,
                IsPrimary = true
            }
        };

        var (scale, workArea, isPrimary) = MonitorEnumerationService.ResolveWaylandScreenMetrics(
            displayName: "eDP-1",
            logicalBounds: new CapturePixelRect(0, 0, 3072, 1728),
            logicalWorkArea: new CapturePixelRect(0, 0, 3072, 1728),
            avaloniaScaling: 1.0,
            avaloniaIsPrimary: false,
            platformScreens: platformScreens,
            fallbackWindowRenderScaling: null);

        Assert.Multiple(() =>
        {
            Assert.That(scale, Is.EqualTo(1.25).Within(0.0001));
            Assert.That(workArea, Is.EqualTo(new CapturePixelRect(0, 26, 3072, 1702)));
            Assert.That(isPrimary, Is.True);
        });
    }

    [Test]
    public void ResolveWaylandScreenMetrics_DerivesScaleFromXrandrRatioOrRenderScalingFallback()
    {
        var xrandrScreens = new[]
        {
            new ScreenInfo
            {
                DeviceName = "eDP-1",
                Bounds = new DrawingRectangle(0, 0, 3840, 2160),
                WorkingArea = new DrawingRectangle(0, 0, 3840, 2160),
                ScaleFactor = 1.0,
                IsPrimary = true
            }
        };

        var (ratioScale, _, ratioPrimary) = MonitorEnumerationService.ResolveWaylandScreenMetrics(
            displayName: "eDP-1",
            logicalBounds: new CapturePixelRect(0, 0, 3072, 1728),
            logicalWorkArea: new CapturePixelRect(0, 0, 3072, 1728),
            avaloniaScaling: 1.0,
            avaloniaIsPrimary: false,
            platformScreens: xrandrScreens,
            fallbackWindowRenderScaling: null);

        var (windowScale, _, _) = MonitorEnumerationService.ResolveWaylandScreenMetrics(
            displayName: "DP-9",
            logicalBounds: new CapturePixelRect(0, 0, 3072, 1728),
            logicalWorkArea: new CapturePixelRect(0, 0, 3072, 1728),
            avaloniaScaling: 1.0,
            avaloniaIsPrimary: false,
            platformScreens: [],
            fallbackWindowRenderScaling: 1.25);

        Assert.Multiple(() =>
        {
            Assert.That(ratioScale, Is.EqualTo(1.25).Within(0.0001));
            Assert.That(ratioPrimary, Is.True);
            Assert.That(windowScale, Is.EqualTo(1.25).Within(0.0001));
        });
    }

    [Test]
    public void BackgroundBitmapMapper_MapsFullBitmapWithoutCropping_WhenBitmapExceedsLogicalVirtualBounds()
    {
        var virtualBounds = new CapturePixelRect(0, 0, 3072, 1728);
        var mappedRect = BackgroundBitmapMapper.MapRectToBitmap(virtualBounds, virtualBounds, 3840, 2160);
        var mappedCrop = BackgroundBitmapMapper.MapRectToBitmapInt(new CapturePixelRect(100, 200, 400, 300), virtualBounds, 3840, 2160);
        var mappedPoint = BackgroundBitmapMapper.MapPointToBitmap(new CapturePixelPoint(1536, 864), virtualBounds, 3840, 2160);

        Assert.Multiple(() =>
        {
            Assert.That(mappedRect, Is.EqualTo(new Rect(0, 0, 3840, 2160)));
            Assert.That(mappedCrop, Is.EqualTo(new SKRectI(125, 250, 625, 625)));
            Assert.That(mappedPoint, Is.EqualTo((1920, 1080)));
        });
    }

    [Test]
    public void BackgroundBitmapMapper_IsIdentityWhenPhysicalVirtualBoundsMatchBitmap()
    {
        var virtualBounds = new CapturePixelRect(0, 0, 3840, 2160);
        var mappedRect = BackgroundBitmapMapper.MapRectToBitmap(virtualBounds, virtualBounds, 3840, 2160);
        var mappedPoint = BackgroundBitmapMapper.MapPointToBitmap(new CapturePixelPoint(1920, 1080), virtualBounds, 3840, 2160);

        Assert.Multiple(() =>
        {
            Assert.That(mappedRect, Is.EqualTo(new Rect(0, 0, 3840, 2160)));
            Assert.That(mappedPoint, Is.EqualTo((1920, 1080)));
        });
    }
}
