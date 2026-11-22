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
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.Services;
using XerahS.UI.Diagnostics;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;
using DrawingSizeF = System.Drawing.SizeF;

namespace XerahS.Tests.Services;

[TestFixture]
public class DisplayDiagnosticsCollectorTests
{
    private sealed class FakeLinuxDisplayDiagnosticsService : IDisplayDiagnosticsService
    {
        public bool IsSupported => true;
        public bool IsWindowsNative => false;
        public string ProviderName => "Hyprland (hyprctl)";

        public IReadOnlyList<DisplayMonitorInfo> EnumerateMonitors() =>
        [
            new DisplayMonitorInfo
            {
                Handle = (IntPtr)1,
                DeviceName = "eDP-1",
                FriendlyName = "Apple Computer Inc iMac",
                Bounds = new DrawingRectangle(0, 0, 3840, 2160),
                WorkingArea = new DrawingRectangle(0, 33, 3840, 2127),
                LogicalBounds = new DrawingRectangle(0, 0, 3072, 1728),
                LogicalWorkingArea = new DrawingRectangle(0, 26, 3072, 1702),
                ScaleFactor = 1.25,
                RefreshRateHz = 60,
                Orientation = 0,
                ReservedTop = 26,
                Primary = true,
                Source = "hyprctl"
            }
        ];

        public DisplayModeInfo? GetCurrentDisplayMode(string deviceName) => new()
        {
            Width = 3840,
            Height = 2160,
            Position = new DrawingPoint(0, 0),
            BitsPerPixel = 24,
            Frequency = 60,
            Orientation = 0
        };

        public IReadOnlyList<DisplayDeviceInfo> GetAttachedAdapters() =>
        [
            new DisplayDeviceInfo
            {
                DeviceName = "card1",
                DeviceString = "amdgpu (1002:7340)"
            }
        ];

        public DrawingPoint? GetCursorPosition() => new(1000, 800);
    }

    [Test]
    public void CollectCore_WaylandFractionalScaleAligned_ReportsNoMismatchesAndConvertsCursor()
    {
        var avaloniaScreens = new[]
        {
            new DisplayDiagnosticsAvaloniaScreen(
                DisplayName: "eDP-1",
                Bounds: new DrawingRectangle(0, 0, 3072, 1728),
                WorkingArea: new DrawingRectangle(0, 0, 3072, 1728),
                Scaling: 1.0,
                IsPrimary: false)
        };

        var regionMonitors = new[]
        {
            new MonitorInfo(
                DeviceName: "eDP-1",
                PhysicalBounds: new PixelRect(0, 0, 3840, 2160),
                WorkArea: new PixelRect(0, 33, 3840, 2127),
                ScaleFactor: 1.25,
                IsPrimary: true,
                OverlayBoundsOverride: new PixelRect(0, 0, 3072, 1728),
                OverlayWorkAreaOverride: new PixelRect(0, 26, 3072, 1702))
        };

        var entries = new[]
        {
            new OverlayWindowGeometryEntry
            {
                Time = DateTime.Now,
                Source = "Region capture overlay",
                Backend = "Wayland (zwlr_layer_shell_v1)",
                MonitorDeviceName = "eDP-1",
                ExpectedPhysicalBounds = new DrawingRectangle(0, 0, 3840, 2160),
                ExpectedOverlayBounds = new DrawingRectangle(0, 0, 3072, 1728),
                ActualBounds = new DrawingRectangle(0, 0, 3840, 2160),
                ClientSize = new DrawingSizeF(3072, 1728),
                RenderScaling = 1.25,
                MonitorScaleFactor = 1.25,
                ImageSize = new DrawingSize(3840, 2160)
            }
        };

        var result = DisplayDiagnosticsCollector.CollectCore(
            native: new FakeLinuxDisplayDiagnosticsService(),
            avaloniaScreens: avaloniaScreens,
            regionMonitors: regionMonitors,
            overlayEntries: entries,
            platformVirtualBounds: new DrawingRectangle(0, 0, 3840, 2160),
            appMetadata: new DisplayDiagnosticsAppMetadata("XerahS 0.36.0", "Avalonia 12.1.7-sharex.1", "Wayland (zwlr_layer_shell_v1)", "4.155.0", ".NET 11"),
            windowContext: new DisplayDiagnosticsWindowContext(null, null, new DrawingSize(1200, 800), 1.25, "Wayland (zwlr_layer_shell_v1)", true),
            taskSettings: null,
            linuxDesktopProfileDescription: "Wayland=True, Hyprland=True");

        Assert.Multiple(() =>
        {
            Assert.That(result.Monitors, Has.Count.EqualTo(1));
            Assert.That(result.Monitors[0].ScalePercent, Is.EqualTo(125));
            Assert.That(result.Monitors[0].Bounds, Is.EqualTo(new DrawingRectangle(0, 0, 3840, 2160)));
            Assert.That(result.CursorPosition, Is.EqualTo(new DrawingPoint(1250, 1000)));
            Assert.That(result.Findings, Is.Empty);
            Assert.That(result.Text, Does.Contain("Nothing unusual detected."));
            Assert.That(result.Text, Does.Contain("Recent capture windows"));
            Assert.That(result.Text, Does.Contain("Panels and reserved areas"));
            Assert.That(result.Text, Does.Contain("T=26"));
        });
    }

    [Test]
    public void CollectCore_DetectsScaleAndImageSizeMismatch()
    {
        var avaloniaScreens = new[]
        {
            new DisplayDiagnosticsAvaloniaScreen(
                DisplayName: "eDP-1",
                Bounds: new DrawingRectangle(0, 0, 3072, 1728),
                WorkingArea: new DrawingRectangle(0, 0, 3072, 1728),
                Scaling: 1.0,
                IsPrimary: false)
        };

        // Simulate the buggy state where RegionCapture thought scale=1.0 (3072x1728) while screenshot is 3840x2160.
        var regionMonitors = new[]
        {
            new MonitorInfo(
                DeviceName: "eDP-1",
                PhysicalBounds: new PixelRect(0, 0, 3072, 1728),
                WorkArea: new PixelRect(0, 0, 3072, 1728),
                ScaleFactor: 1.0,
                IsPrimary: true,
                OverlayBoundsOverride: new PixelRect(0, 0, 3072, 1728),
                OverlayWorkAreaOverride: new PixelRect(0, 0, 3072, 1728))
        };

        var entries = new[]
        {
            new OverlayWindowGeometryEntry
            {
                Time = DateTime.Now,
                Source = "Region capture overlay",
                Backend = "Wayland (zwlr_layer_shell_v1)",
                MonitorDeviceName = "eDP-1",
                ExpectedPhysicalBounds = new DrawingRectangle(0, 0, 3072, 1728),
                ExpectedOverlayBounds = new DrawingRectangle(0, 0, 3072, 1728),
                ActualBounds = new DrawingRectangle(0, 0, 3840, 2160),
                ClientSize = new DrawingSizeF(3072, 1728),
                RenderScaling = 1.25,
                MonitorScaleFactor = 1.0,
                ImageSize = new DrawingSize(3840, 2160)
            }
        };

        var result = DisplayDiagnosticsCollector.CollectCore(
            native: new FakeLinuxDisplayDiagnosticsService(),
            avaloniaScreens: avaloniaScreens,
            regionMonitors: regionMonitors,
            overlayEntries: entries,
            platformVirtualBounds: new DrawingRectangle(0, 0, 3840, 2160),
            appMetadata: new DisplayDiagnosticsAppMetadata("XerahS 0.36.0", "Avalonia 12.1.7-sharex.1", "Wayland (zwlr_layer_shell_v1)", "4.155.0", ".NET 11"),
            windowContext: null,
            taskSettings: null,
            linuxDesktopProfileDescription: null);

        Assert.That(result.Findings, Has.Some.Contains("RegionCapture uses scale 1 (100%)"));
        Assert.That(result.Findings, Has.Some.Contains("rendered at 125% while MonitorInfo used 100%"));
    }
}
