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
using ShareX.Editor;
using ShareX.Editor.Annotations;
using ShareX.Editor.ImageEffects.Adjustments;
using SkiaSharp;

namespace XerahS.Tests.Editor;

/// <summary>
/// Tests for GitHub Issue #7: Annotations fail to rotate and disappear after complex Undo/Redo sequence.
/// Verifies that annotations transform correctly during rotate/flip operations and survive undo/redo cycles.
/// </summary>
[TestFixture]
public class EditorRotateAnnotationsTests
{
    private EditorCore _core = null!;

    [SetUp]
    public void SetUp()
    {
        _core = new EditorCore();
        // Load a non-square test image to make rotation dimension changes detectable
        var bitmap = new SKBitmap(200, 100);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
        }
        _core.LoadImage(bitmap);
    }

    [TearDown]
    public void TearDown()
    {
        _core.Dispose();
    }

    #region Annotations survive rotation

    [Test]
    public void Rotate90CW_AnnotationsTransformWithImage()
    {
        var rect = new RectangleAnnotation
        {
            StartPoint = new SKPoint(10, 20),
            EndPoint = new SKPoint(50, 40)
        };
        _core.AddAnnotation(rect);

        _core.PerformRotate90CW();

        // Annotations should still exist (not cleared)
        Assert.That(_core.Annotations.Count, Is.EqualTo(1));
        // Canvas should be 100x200 (swapped from 200x100)
        Assert.That(_core.SourceImage!.Width, Is.EqualTo(100));
        Assert.That(_core.SourceImage!.Height, Is.EqualTo(200));
    }

    [Test]
    public void Rotate90CCW_AnnotationsTransformWithImage()
    {
        var rect = new RectangleAnnotation
        {
            StartPoint = new SKPoint(10, 20),
            EndPoint = new SKPoint(50, 40)
        };
        _core.AddAnnotation(rect);

        _core.PerformRotate90CCW();

        Assert.That(_core.Annotations.Count, Is.EqualTo(1));
        Assert.That(_core.SourceImage!.Width, Is.EqualTo(100));
        Assert.That(_core.SourceImage!.Height, Is.EqualTo(200));
    }

    [Test]
    public void Rotate180_AnnotationsTransformWithImage()
    {
        var rect = new RectangleAnnotation
        {
            StartPoint = new SKPoint(10, 20),
            EndPoint = new SKPoint(50, 40)
        };
        _core.AddAnnotation(rect);

        _core.PerformRotate180();

        Assert.That(_core.Annotations.Count, Is.EqualTo(1));
        // 180° keeps same dimensions
        Assert.That(_core.SourceImage!.Width, Is.EqualTo(200));
        Assert.That(_core.SourceImage!.Height, Is.EqualTo(100));
    }

    [Test]
    public void FlipHorizontal_AnnotationsTransformWithImage()
    {
        var rect = new RectangleAnnotation
        {
            StartPoint = new SKPoint(10, 20),
            EndPoint = new SKPoint(50, 40)
        };
        _core.AddAnnotation(rect);

        _core.PerformFlipHorizontal();

        Assert.That(_core.Annotations.Count, Is.EqualTo(1));
    }

    [Test]
    public void FlipVertical_AnnotationsTransformWithImage()
    {
        var rect = new RectangleAnnotation
        {
            StartPoint = new SKPoint(10, 20),
            EndPoint = new SKPoint(50, 40)
        };
        _core.AddAnnotation(rect);

        _core.PerformFlipVertical();

        Assert.That(_core.Annotations.Count, Is.EqualTo(1));
    }

    #endregion

    #region Annotation coordinates transform correctly

    [Test]
    public void Rotate90CW_AnnotationCoordinatesAreCorrect()
    {
        // Image is 200x100. After 90° CW, it becomes 100x200.
        // Point (0, 0) should map to (100, 0) = (H, 0) in the new image
        // Point (200, 100) should map to (0, 200)
        var rect = new RectangleAnnotation
        {
            StartPoint = new SKPoint(0, 0),
            EndPoint = new SKPoint(200, 100)
        };
        _core.AddAnnotation(rect);

        _core.PerformRotate90CW();

        var ann = _core.Annotations[0];
        // After 90° CW, the full-canvas annotation should map to the full new canvas
        Assert.That(ann.StartPoint.X, Is.EqualTo(100).Within(1f));
        Assert.That(ann.StartPoint.Y, Is.EqualTo(0).Within(1f));
        Assert.That(ann.EndPoint.X, Is.EqualTo(0).Within(1f));
        Assert.That(ann.EndPoint.Y, Is.EqualTo(200).Within(1f));
    }

    [Test]
    public void Rotate180_AnnotationCoordinatesAreCorrect()
    {
        // 180° rotation: (x, y) → (W - x, H - y)
        var rect = new RectangleAnnotation
        {
            StartPoint = new SKPoint(10, 20),
            EndPoint = new SKPoint(50, 40)
        };
        _core.AddAnnotation(rect);

        _core.PerformRotate180();

        var ann = _core.Annotations[0];
        Assert.That(ann.StartPoint.X, Is.EqualTo(190).Within(1f));
        Assert.That(ann.StartPoint.Y, Is.EqualTo(80).Within(1f));
        Assert.That(ann.EndPoint.X, Is.EqualTo(150).Within(1f));
        Assert.That(ann.EndPoint.Y, Is.EqualTo(60).Within(1f));
    }

    [Test]
    public void FlipHorizontal_AnnotationCoordinatesAreCorrect()
    {
        // Horizontal flip: (x, y) → (W - x, y)
        var rect = new RectangleAnnotation
        {
            StartPoint = new SKPoint(10, 20),
            EndPoint = new SKPoint(50, 40)
        };
        _core.AddAnnotation(rect);

        _core.PerformFlipHorizontal();

        var ann = _core.Annotations[0];
        Assert.That(ann.StartPoint.X, Is.EqualTo(190).Within(1f));
        Assert.That(ann.StartPoint.Y, Is.EqualTo(20).Within(1f));
        Assert.That(ann.EndPoint.X, Is.EqualTo(150).Within(1f));
        Assert.That(ann.EndPoint.Y, Is.EqualTo(40).Within(1f));
    }

    [Test]
    public void FlipVertical_AnnotationCoordinatesAreCorrect()
    {
        // Vertical flip: (x, y) → (x, H - y)
        var rect = new RectangleAnnotation
        {
            StartPoint = new SKPoint(10, 20),
            EndPoint = new SKPoint(50, 40)
        };
        _core.AddAnnotation(rect);

        _core.PerformFlipVertical();

        var ann = _core.Annotations[0];
        Assert.That(ann.StartPoint.X, Is.EqualTo(10).Within(1f));
        Assert.That(ann.StartPoint.Y, Is.EqualTo(80).Within(1f));
        Assert.That(ann.EndPoint.X, Is.EqualTo(50).Within(1f));
        Assert.That(ann.EndPoint.Y, Is.EqualTo(60).Within(1f));
    }

    #endregion

    #region Issue #7 reproduction: Complex undo/redo after rotation

    [Test]
    public void Issue7_Annotations_SurviveRotateAndUndoRedoCycle()
    {
        // Reproduction steps from issue #7:
        // 1. Draw a rectangle annotation
        var rect = new RectangleAnnotation
        {
            StartPoint = new SKPoint(10, 10),
            EndPoint = new SKPoint(80, 50)
        };
        _core.AddAnnotation(rect);

        // 2. Apply an effect (invert)
        _core.AddEffect(new InvertImageEffect());

        // 3. Add ellipse annotation
        var ellipse = new EllipseAnnotation
        {
            StartPoint = new SKPoint(100, 30),
            EndPoint = new SKPoint(180, 80)
        };
        _core.AddAnnotation(ellipse);

        // 4. Apply another effect (invert again as stand-in)
        _core.AddEffect(new InvertImageEffect());

        Assert.That(_core.Annotations.Count, Is.EqualTo(2));
        Assert.That(_core.Effects.Count, Is.EqualTo(2));

        // 5. Rotate 90 degrees
        _core.PerformRotate90CW();

        // Annotations should still exist after rotation
        Assert.That(_core.Annotations.Count, Is.EqualTo(2), "Annotations should survive rotation");
        Assert.That(_core.SourceImage!.Width, Is.EqualTo(100));
        Assert.That(_core.SourceImage!.Height, Is.EqualTo(200));

        // 6. Undo all edits
        _core.Undo(); // Undo rotate
        Assert.That(_core.Annotations.Count, Is.EqualTo(2), "Annotations should be restored after undo rotate");
        Assert.That(_core.SourceImage!.Width, Is.EqualTo(200));

        _core.Undo(); // Undo effect 2
        Assert.That(_core.Annotations.Count, Is.EqualTo(2));
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        _core.Undo(); // Undo ellipse
        Assert.That(_core.Annotations.Count, Is.EqualTo(1));

        _core.Undo(); // Undo effect 1
        Assert.That(_core.Effects.Count, Is.EqualTo(0));

        _core.Undo(); // Undo rect
        Assert.That(_core.Annotations.Count, Is.EqualTo(0));

        // 7. Redo all edits
        _core.Redo(); // Redo rect
        Assert.That(_core.Annotations.Count, Is.EqualTo(1));

        _core.Redo(); // Redo effect 1
        Assert.That(_core.Effects.Count, Is.EqualTo(1));

        _core.Redo(); // Redo ellipse
        Assert.That(_core.Annotations.Count, Is.EqualTo(2));

        _core.Redo(); // Redo effect 2
        Assert.That(_core.Effects.Count, Is.EqualTo(2));

        _core.Redo(); // Redo rotate
        Assert.That(_core.Annotations.Count, Is.EqualTo(2), "Annotations should survive redo of rotation");
        Assert.That(_core.SourceImage!.Width, Is.EqualTo(100));
        Assert.That(_core.SourceImage!.Height, Is.EqualTo(200));
    }

    #endregion

    #region Multiple annotations with various types

    [Test]
    public void Rotate90CW_MultipleAnnotationTypes_AllTransformed()
    {
        _core.AddAnnotation(new RectangleAnnotation
        {
            StartPoint = new SKPoint(10, 10),
            EndPoint = new SKPoint(50, 50)
        });
        _core.AddAnnotation(new EllipseAnnotation
        {
            StartPoint = new SKPoint(60, 20),
            EndPoint = new SKPoint(120, 60)
        });

        _core.PerformRotate90CW();

        Assert.That(_core.Annotations.Count, Is.EqualTo(2));
        // Both annotations should have valid coordinates within the new canvas (100x200)
        foreach (var ann in _core.Annotations)
        {
            var bounds = ann.GetBounds();
            Assert.That(bounds.Left, Is.GreaterThanOrEqualTo(-1f));
            Assert.That(bounds.Top, Is.GreaterThanOrEqualTo(-1f));
            Assert.That(bounds.Right, Is.LessThanOrEqualTo(101f));
            Assert.That(bounds.Bottom, Is.LessThanOrEqualTo(201f));
        }
    }

    #endregion
}
