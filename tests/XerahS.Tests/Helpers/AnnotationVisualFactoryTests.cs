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

using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using NUnit.Framework;
using ShareX.ImageEditor.Annotations;
using SkiaSharp;

namespace XerahS.Tests.Helpers;

[TestFixture]
public class AnnotationVisualFactoryTests
{
    [Test]
    public void ArrowHeadSize_UsesStrokeBasedDefaultWhenOverrideNotSet()
    {
        var arrow = new ArrowAnnotation
        {
            StartPoint = new SKPoint(10, 20),
            EndPoint = new SKPoint(140, 85),
            StrokeWidth = 6,
            StrokeColor = "#FF00AAFF",
            ArrowHeadSize = 0
        };

        var expected = (float)(arrow.StrokeWidth * ArrowAnnotation.ArrowHeadWidthMultiplier);
        Assert.That(arrow.GetEffectiveArrowHeadSize(), Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void ArrowHeadSize_RespectsExplicitOverride()
    {
        var arrow = new ArrowAnnotation
        {
            StartPoint = new SKPoint(20, 30),
            EndPoint = new SKPoint(160, 110),
            StrokeWidth = 4,
            StrokeColor = "#FFFF5500",
            ArrowHeadSize = 42
        };

        Assert.That(arrow.GetEffectiveArrowHeadSize(), Is.EqualTo(42).Within(0.001f));
    }

    [Test]
    public void TextPreviewMode_UsesPlaceholderWhilePersistedUsesTextBox()
    {
        var textAnnotation = new TextAnnotation
        {
            StartPoint = new SKPoint(50, 60),
            EndPoint = new SKPoint(170, 120),
            StrokeColor = "#FFFFFFFF"
        };

        var previewControl = AnnotationVisualFactory.CreateVisualControl(textAnnotation, AnnotationVisualMode.Preview);
        var persistedControl = AnnotationVisualFactory.CreateVisualControl(textAnnotation, AnnotationVisualMode.Persisted);

        Assert.That(previewControl, Is.TypeOf<Rectangle>());
        Assert.That(persistedControl, Is.TypeOf<TextBox>());
    }
}
