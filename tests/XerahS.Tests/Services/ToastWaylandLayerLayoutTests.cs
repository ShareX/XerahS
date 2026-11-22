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
using NUnit.Framework;
using XerahS.Platform.Abstractions;
using XerahS.UI.Views;

namespace XerahS.Tests.Services;

[TestFixture]
public class ToastWaylandLayerLayoutTests
{
    [TestCase(ContentPlacement.TopLeft, WaylandLayerAnchor.Top | WaylandLayerAnchor.Left, 12, 12, 0, 0)]
    [TestCase(ContentPlacement.TopCenter, WaylandLayerAnchor.Top, 0, 12, 0, 0)]
    [TestCase(ContentPlacement.TopRight, WaylandLayerAnchor.Top | WaylandLayerAnchor.Right, 0, 12, 12, 0)]
    [TestCase(ContentPlacement.MiddleLeft, WaylandLayerAnchor.Left, 12, 0, 0, 0)]
    [TestCase(ContentPlacement.MiddleCenter, WaylandLayerAnchor.None, 0, 0, 0, 0)]
    [TestCase(ContentPlacement.MiddleRight, WaylandLayerAnchor.Right, 0, 0, 12, 0)]
    [TestCase(ContentPlacement.BottomLeft, WaylandLayerAnchor.Bottom | WaylandLayerAnchor.Left, 12, 0, 0, 12)]
    [TestCase(ContentPlacement.BottomCenter, WaylandLayerAnchor.Bottom, 0, 0, 0, 12)]
    [TestCase(ContentPlacement.BottomRight, WaylandLayerAnchor.Bottom | WaylandLayerAnchor.Right, 0, 0, 12, 12)]
    public void Create_MapsPlacementAndOffsetToExpectedAnchorAndMargin(
        ContentPlacement placement,
        WaylandLayerAnchor expectedAnchor,
        double left,
        double top,
        double right,
        double bottom)
    {
        var layout = ToastWaylandLayerLayout.Create(placement, offset: 12);

        Assert.Multiple(() =>
        {
            Assert.That(layout.Layer, Is.EqualTo(WaylandLayer.Overlay));
            Assert.That(layout.Anchor, Is.EqualTo(expectedAnchor));
            Assert.That(layout.Margin, Is.EqualTo(new Thickness(left, top, right, bottom)));
            Assert.That(layout.ExclusiveZone, Is.EqualTo(0));
            Assert.That(layout.KeyboardInteractivity, Is.EqualTo(WaylandLayerKeyboardInteractivity.None));
            Assert.That(layout.Namespace, Is.EqualTo(ToastWaylandLayerLayout.DefaultNamespace));
        });
    }

    [TestCase(0)]
    [TestCase(-10)]
    public void Create_ClampsZeroAndNegativeOffsetsToZeroMargin(int offset)
    {
        var layout = ToastWaylandLayerLayout.Create(ContentPlacement.BottomRight, offset);

        Assert.That(layout.Margin, Is.EqualTo(default(Thickness)));
    }

    [AvaloniaTest]
    public void Apply_PopulatesAllWaylandLayerAttachedPropertiesOnToastWindow()
    {
        var window = new ToastWindow();
        var layout = ToastWaylandLayerLayout.Create(ContentPlacement.TopLeft, offset: 16);

        ToastWaylandLayerLayout.Apply(window, layout);

        Assert.Multiple(() =>
        {
            Assert.That(WaylandProperties.GetLayer(window), Is.EqualTo(WaylandLayer.Overlay));
            Assert.That(WaylandProperties.GetLayerAnchor(window), Is.EqualTo(WaylandLayerAnchor.Top | WaylandLayerAnchor.Left));
            Assert.That(WaylandProperties.GetLayerExclusiveZone(window), Is.EqualTo(0));
            Assert.That(WaylandProperties.GetLayerMargin(window), Is.EqualTo(new Thickness(16, 16, 0, 0)));
            Assert.That(WaylandProperties.GetLayerKeyboardInteractivity(window), Is.EqualTo(WaylandLayerKeyboardInteractivity.None));
            Assert.That(WaylandProperties.GetLayerNamespace(window), Is.EqualTo(ToastWaylandLayerLayout.DefaultNamespace));
            Assert.That(WaylandProperties.GetLayerOutputName(window), Is.Null);
        });
    }

    [AvaloniaTest]
    public void Initialize_SetsFixedToastDimensionsAndDoesNotActivateOnShow()
    {
        var window = new ToastWindow();
        var config = new ToastConfig
        {
            Title = "Test",
            Text = "Toast size bounds",
            Size = new SizeI(360, 220),
            Placement = ContentPlacement.BottomRight,
            Offset = 10,
            AutoHide = false
        };

        window.Initialize(config);

        Assert.Multiple(() =>
        {
            Assert.That(window.ShowActivated, Is.False);
            Assert.That(window.Width, Is.EqualTo(360));
            Assert.That(window.Height, Is.EqualTo(220));
            Assert.That(window.MinWidth, Is.EqualTo(360));
            Assert.That(window.MaxWidth, Is.EqualTo(360));
            Assert.That(window.MinHeight, Is.EqualTo(220));
            Assert.That(window.MaxHeight, Is.EqualTo(220));
        });

        window.Close();
    }
}
