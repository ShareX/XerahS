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
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NUnit.Framework;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Presentation.Controls;
using XerahS.RegionCapture.UI;
using XerahS.RegionCapture.ViewModels;
using CaptureMonitorInfo = XerahS.RegionCapture.Models.MonitorInfo;
using CapturePixelPoint = XerahS.RegionCapture.Models.PixelPoint;
using CapturePixelRect = XerahS.RegionCapture.Models.PixelRect;

namespace XerahS.Tests.RegionCapture;

[TestFixture]
[NonParallelizable]
public class RegionCaptureUiSmokeTests
{
    [AvaloniaTest]
    public void AnnotationToolbar_Loads_With_ImageEditor_Resources()
    {
        var viewModel = new RegionCaptureAnnotationViewModel
        {
            ActiveTool = EditorTool.Rectangle
        };

        var toolbar = new AnnotationToolbar
        {
            DataContext = viewModel
        };

        var window = new Window
        {
            Width = 1200,
            Height = 300,
            Content = toolbar
        };

        try
        {
            window.Show();

            Assert.That(toolbar.FindControl<Button>("ShadowToggleButton"), Is.Not.Null);
            Assert.That(toolbar.FindControl<Control>("StrokeColorPicker"), Is.Not.Null);
            Assert.That(toolbar.FindControl<Control>("TextColorPicker"), Is.Not.Null);
            Assert.That(toolbar.FindControl<Control>("CornerRadiusPicker"), Is.Not.Null);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void AnnotationToolbar_ExposesAllRegionCaptureToolButtons()
    {
        var expectedTools = new[]
        {
            EditorTool.Select,
            EditorTool.Rectangle,
            EditorTool.Ellipse,
            EditorTool.Line,
            EditorTool.Arrow,
            EditorTool.Freehand,
            EditorTool.Text,
            EditorTool.SpeechBalloon,
            EditorTool.Step,
            EditorTool.Image,
            EditorTool.Cursor,
            EditorTool.Highlight,
            EditorTool.SmartEraser,
            EditorTool.Blur,
            EditorTool.Pixelate,
            EditorTool.Magnify,
            EditorTool.Spotlight,
            EditorTool.Crop,
            EditorTool.CutOut
        };

        var viewModel = new RegionCaptureAnnotationViewModel();

        Assert.That(
            viewModel.VisibleToolbarItems.Select(item => item.Tool).Where(tool => tool.HasValue).Select(tool => tool!.Value),
            Is.EquivalentTo(expectedTools));

        var toolbar = new AnnotationToolbar
        {
            DataContext = viewModel,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
        };

        var window = new Window
        {
            Width = 480,
            Height = 240,
            Content = toolbar
        };

        try
        {
            window.Show();
            toolbar.UpdateLayout();

            var toolButtons = toolbar.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Tag is ShareX.ImageEditor.Presentation.ViewModels.ToolbarCustomizationItemViewModel { Tool: not null })
                .ToList();

            Assert.That(toolButtons.Count, Is.EqualTo(expectedTools.Length));

            var scrollViewer = toolbar.FindControl<ScrollViewer>("PrimaryToolbarScrollViewer");
            Assert.That(scrollViewer, Is.Not.Null);
            Assert.That(scrollViewer!.Viewport.Width, Is.GreaterThan(0).And.LessThanOrEqualTo(480));
            Assert.That(scrollViewer.Extent.Width, Is.GreaterThan(scrollViewer.Viewport.Width));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void OverlayWindow_Loads_AnnotationToolbar_Surface()
    {
        var overlayWindow = new OverlayWindow();

        try
        {
            overlayWindow.Show();

            Assert.That(overlayWindow.FindControl<AnnotationToolbar>("AnnotationToolbarControl"), Is.Not.Null);
            Assert.That(overlayWindow.FindControl<Canvas>("AnnotationCanvas"), Is.Not.Null);
            Assert.That(overlayWindow.FindControl<Panel>("RootPanel"), Is.Not.Null);
        }
        finally
        {
            overlayWindow.Close();
        }
    }

    [AvaloniaTest]
    public void RegionCaptureControl_UpdateAimFromOverlayPointer_TracksAnnotationCanvasHover()
    {
        var monitor = new CaptureMonitorInfo(
            DeviceName: "Display 1",
            PhysicalBounds: new CapturePixelRect(100, 200, 1920, 1080),
            WorkArea: new CapturePixelRect(100, 200, 1920, 1040),
            ScaleFactor: 2.0,
            IsPrimary: true);

        var control = new RegionCaptureControl(monitor);

        control.UpdateAimFromOverlayPointer(new Point(12.5, 20), KeyModifiers.None);

        Assert.That(control.CurrentPointForTests, Is.EqualTo(new CapturePixelPoint(125, 240)));
    }

    [AvaloniaTest]
    public void RegionCaptureControl_ArrowKeys_MoveCursorWhileHovering()
    {
        var monitor = new CaptureMonitorInfo(
            DeviceName: "Display 1",
            PhysicalBounds: new CapturePixelRect(100, 200, 1920, 1080),
            WorkArea: new CapturePixelRect(100, 200, 1920, 1040),
            ScaleFactor: 1.0,
            IsPrimary: true);

        var control = new RegionCaptureControl(monitor);
        control.UpdateAimFromOverlayPointer(new Point(10, 20), KeyModifiers.None);
        control.NudgeFromKeyboardForTests(1, 0, shift: false);

        Assert.That(control.CurrentPointForTests, Is.EqualTo(new CapturePixelPoint(111, 220)));
    }
}
