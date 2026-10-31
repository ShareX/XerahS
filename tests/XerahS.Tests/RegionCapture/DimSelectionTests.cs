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

using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Newtonsoft.Json;
using NUnit.Framework;
using SkiaSharp;
using XerahS.RegionCapture;
using XerahS.RegionCapture.Models;
using XerahS.RegionCapture.Services;
using XerahS.RegionCapture.UI;
using CapturePixelRect = XerahS.RegionCapture.Models.PixelRect;
using CapturePixelPoint = XerahS.RegionCapture.Models.PixelPoint;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
[NonParallelizable]
public class DimSelectionTests
{
    [Test]
    public void SettingDefaultsOffAndSurvivesSerialization()
    {
        Assert.That(JsonConvert.DeserializeObject<XerahS.Core.RegionCaptureOptions>("{}")!.DimAllExceptSelection, Is.False);
        var settings = new XerahS.Core.RegionCaptureOptions { DimAllExceptSelection = true, BackgroundDimStrength = 35 };
        var restored = JsonConvert.DeserializeObject<XerahS.Core.RegionCaptureOptions>(JsonConvert.SerializeObject(settings))!;
        Assert.Multiple(() =>
        {
            Assert.That(restored.DimAllExceptSelection, Is.True);
            Assert.That(restored.BackgroundDimStrength, Is.EqualTo(35));
        });
    }

    [TestCase(true, 35, true, 0.35)]
    [TestCase(false, 35, true, 0.0)]
    [TestCase(true, 150, false, 1.0)]
    public void CaptureSessionMapsPersistedDimmingOptions(bool enabled, int strength, bool dimAll, double opacity)
    {
        var settings = XerahS.Core.SettingsManager.DefaultTaskSettings.CaptureSettings;
        var previous = settings.RegionCaptureOptions;
        try
        {
            settings.RegionCaptureOptions = new XerahS.Core.RegionCaptureOptions
            { UseDimming = enabled, BackgroundDimStrength = strength, DimAllExceptSelection = dimAll };
            var session = typeof(XerahS.UI.Views.MainWindow).Assembly.GetType("XerahS.UI.Services.Capture.OverlayRegionCaptureSession")!;
            var method = session.GetMethod("CreateOverlayOptions", BindingFlags.Static | BindingFlags.NonPublic)!;
            var options = (RegionCaptureOptions)method.Invoke(null, new object?[] { null, null, false, null })!;
            Assert.Multiple(() =>
            {
                Assert.That(options.DimOpacity, Is.EqualTo(opacity));
                Assert.That(options.DimAllExceptSelection, Is.EqualTo(dimAll));
            });
        }
        finally { settings.RegionCaptureOptions = previous; }
    }

    [AvaloniaTest]
    public void HoverStaysDimmedButDraggedAndReleasedSelectionsAreBright()
    {
        VerifyOverlay(dimAll: true);
    }

    [AvaloniaTest]
    public void DefaultModeStillUndimsHoveredWindow()
    {
        VerifyOverlay(dimAll: false);
    }

    private static void VerifyOverlay(bool dimAll)
    {
        var bounds = new CapturePixelRect(0, 0, 800, 600);
        var control = new RegionCaptureControl(new MonitorInfo("Test", bounds, bounds, 1, true), new RegionCaptureOptions
        {
            UseTransparentOverlay = true,
            EnableWindowSnapping = false,
            EnableMagnifier = false,
            ShowScreenCrosshair = false,
            ShowInfo = false,
            QuickCrop = false,
            DimOpacity = 0.5,
            DimAllExceptSelection = dimAll
        });
        var state = (SelectionStateMachine)typeof(RegionCaptureControl).GetField("_stateMachine", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(control)!;
        var backdrop = new Border { Background = global::Avalonia.Media.Brushes.White, Child = control };
        var window = new Window { Width = 800, Height = 600, Content = backdrop };
        try
        {
            window.Show();
            control.UpdateLayout();
            var hover = new CapturePixelRect(100, 180, 400, 250);
            state.UpdateHoveredWindow(new WindowInfo(1, "Window", "Test", hover, hover, false, 0));
            AssertBrightness(control, 180, 280, dimAll ? 128 : 255, "hover");
            AssertBrightness(control, 650, 300, 128, "outside hover");

            // A press starts with an empty drag rectangle, so it must not light the hovered window.
            state.BeginDrag(new CapturePixelPoint(100, 200));
            AssertBrightness(control, 180, 280, 128, "press before drag");
            state.UpdateCursorPosition(new CapturePixelPoint(350, 400));
            AssertBrightness(control, 180, 280, 255, "dragged selection");
            AssertBrightness(control, 450, 300, 128, "hovered window outside drag");
            state.EndDrag();
            Assert.That(state.CurrentState, Is.EqualTo(CaptureState.Selected));
            AssertBrightness(control, 180, 280, 255, "released selection");

            state.BeginMove(new CapturePixelPoint(200, 300));
            state.UpdateCursorPosition(new CapturePixelPoint(220, 300));
            AssertBrightness(control, 180, 280, 255, "moving selection");
            state.EndDrag();
            state.BeginResize(SelectionHandle.BottomRight, new CapturePixelPoint(370, 400));
            state.UpdateCursorPosition(new CapturePixelPoint(400, 450));
            AssertBrightness(control, 180, 280, 255, "resizing selection");
            state.EndDrag();

            // A click (without a drag) selects a hovered window only on release.
            state.BeginDrag(new CapturePixelPoint(200, 300));
            AssertBrightness(control, 180, 280, 128, "click press");
            state.EndDrag();
            Assert.That(state.SelectionRect, Is.EqualTo(hover));
            AssertBrightness(control, 180, 280, 255, "click release");
        }
        finally { window.Close(); }
    }

    private static void AssertBrightness(Control control, int x, int y, int expected, string phase)
    {
        control.InvalidateVisual();
        using var target = new RenderTargetBitmap(new PixelSize(800, 600), new Vector(96, 96));
        target.Render((Control)control.Parent!);
        using var stream = new MemoryStream();
        target.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        using var bitmap = SKBitmap.Decode(stream);
        Assert.That((int)bitmap.GetPixel(x, y).Red, Is.EqualTo(expected).Within(2), phase);
    }
}




