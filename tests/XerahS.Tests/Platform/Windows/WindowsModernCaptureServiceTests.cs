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
using SkiaSharp;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Windows;
using XerahS.Platform.Windows.Capture;

namespace XerahS.Tests.Platform.Windows;

public class WindowsModernCaptureServiceTests
{
#if WINDOWS
    [TestCase(false, false)]
    [TestCase(true, true)]
    public void ShouldUseModernCapture_UsesResolvedCaptureOption(bool configuredValue, bool expected)
    {
        var options = new CaptureOptions { UseModernCapture = configuredValue };

        Assert.That(WindowsModernCaptureService.ShouldUseModernCapture(options), Is.EqualTo(expected));
    }

    [Test]
    public void ShouldUseModernCapture_WithoutOptions_PrefersDxgi()
    {
        Assert.That(WindowsModernCaptureService.ShouldUseModernCapture(null), Is.True);
    }
#endif

    [Test]
    public void DisposableContextDictionary_ReplaceDisposesPreviousContextForSameKey()
    {
        var contexts = new Dictionary<string, TrackingDisposable>
        {
            ["monitor-1"] = new TrackingDisposable()
        };
        var previous = contexts["monitor-1"];
        var replacement = new TrackingDisposable();

        DisposableContextDictionary.Replace(contexts, "monitor-1", replacement);

        Assert.Multiple(() =>
        {
            Assert.That(previous.Disposed, Is.True);
            Assert.That(replacement.Disposed, Is.False);
            Assert.That(contexts["monitor-1"], Is.SameAs(replacement));
        });
    }

    [Test]
    public void DisposableContextDictionary_ReplaceKeepsCurrentContextWhenReferenceIsSame()
    {
        var context = new TrackingDisposable();
        var contexts = new Dictionary<string, TrackingDisposable>
        {
            ["monitor-1"] = context
        };

        DisposableContextDictionary.Replace(contexts, "monitor-1", context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Disposed, Is.False);
            Assert.That(contexts["monitor-1"], Is.SameAs(context));
        });
    }

    [Test]
    public void DisposeOutputsAndAdapters_DisposesEveryOutputAndEachAdapterOnce()
    {
        var adapter = new TrackingDisposable();
        var otherAdapter = new TrackingDisposable();
        var output1 = new TrackingDisposable();
        var output2 = new TrackingDisposable();
        var output3 = new TrackingDisposable();
        var outputs = new[]
        {
            (Output: output1, Adapter: adapter),
            (Output: output2, Adapter: adapter),
            (Output: output3, Adapter: otherAdapter)
        };

        DxgiOutputEnumerationCleanupHelper.DisposeOutputsAndAdapters(
            outputs,
            item => item.Output,
            item => item.Adapter);

        Assert.Multiple(() =>
        {
            Assert.That(output1.DisposeCount, Is.EqualTo(1));
            Assert.That(output2.DisposeCount, Is.EqualTo(1));
            Assert.That(output3.DisposeCount, Is.EqualTo(1));
            Assert.That(adapter.DisposeCount, Is.EqualTo(1));
            Assert.That(otherAdapter.DisposeCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void DxgiCapabilities_AdvertiseCursorCaptureWhenCursorCompositionIsAvailable()
    {
        var capabilities = DxgiCapabilitiesHelper.Create();

        Assert.That(capabilities.SupportsCursorCapture, Is.True);
    }

    [TestCase(false, false, true)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, true, false)]
    public void ShouldRetryFrameAcquisition_RetriesUntilResultAndDesktopResourceAreAvailable(
        bool acquireSucceeded,
        bool desktopResourceAvailable,
        bool expectedRetry)
    {
        bool shouldRetry = DxgiFrameAcquisitionHelper.ShouldRetryFrameAcquisition(
            acquireSucceeded,
            desktopResourceAvailable);

        Assert.That(shouldRetry, Is.EqualTo(expectedRetry));
    }

    [TestCase(0, 0, true)]
    [TestCase(2, 0, true)]
    [TestCase(2, 1, true)]
    [TestCase(2, 2, false)]
    public void ShouldFallbackToGdi_WhenDxgiDoesNotCaptureEveryExpectedOutput(
        int expectedOutputCount,
        int capturedOutputCount,
        bool expectedFallback)
    {
        bool shouldFallback = DxgiFrameAcquisitionHelper.ShouldFallbackToGdi(
            expectedOutputCount,
            capturedOutputCount);

        Assert.That(shouldFallback, Is.EqualTo(expectedFallback));
    }

    [Test]
    public void TryReplaceSystemCursors_DisposesCopiesWhenReplacementFails()
    {
        var destroyed = new List<IntPtr>();

        bool replacedAny = CursorReplacementHelper.TryReplaceSystemCursors(
            new uint[] { 32512, 32513 },
            copyCursor: () => new IntPtr(42),
            setSystemCursor: (_, _) => false,
            destroyCursor: destroyed.Add);

        Assert.Multiple(() =>
        {
            Assert.That(replacedAny, Is.False);
            Assert.That(destroyed, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void TryReplaceSystemCursors_SkipsZeroCopiesWithoutReportingReplacement()
    {
        bool replacedAny = CursorReplacementHelper.TryReplaceSystemCursors(
            new uint[] { 32512, 32513 },
            copyCursor: () => IntPtr.Zero,
            setSystemCursor: (_, _) => true,
            destroyCursor: _ => Assert.Fail("Zero cursor handles must not be destroyed."));

        Assert.That(replacedAny, Is.False);
    }

    [Test]
    public void TryReplaceSystemCursors_ReportsReplacementOnlyAfterSuccessfulSet()
    {
        int calls = 0;
        var destroyed = new List<IntPtr>();

        bool replacedAny = CursorReplacementHelper.TryReplaceSystemCursors(
            new uint[] { 32512, 32513 },
            copyCursor: () => new IntPtr(++calls),
            setSystemCursor: (_, id) => id == 32513,
            destroyCursor: destroyed.Add);

        Assert.Multiple(() =>
        {
            Assert.That(replacedAny, Is.True);
            Assert.That(destroyed, Is.EqualTo(new[] { new IntPtr(1) }));
        });
    }

    [Test]
    public void CreateDxgiCursorPlacement_MapsScreenCursorToCapturedBitmapCoordinates()
    {
        var captureRegion = new ShareX.Avalonia.Platform.Abstractions.Capture.PhysicalRectangle(100, 50, 200, 100);

        var placement = DxgiCursorCompositionHelper.CreatePlacement(
            includeCursor: true,
            cursorVisible: true,
            cursorPosition: new Point(125, 80),
            hotspot: new Point(5, 10),
            cursorSize: new Size(32, 32),
            captureRegion);

        Assert.Multiple(() =>
        {
            Assert.That(placement.ShouldDraw, Is.True);
            Assert.That(placement.DrawOffset, Is.EqualTo(new Point(20, 20)));
        });
    }

    [Test]
    public void CreateDxgiCursorPlacement_RejectsCursorOutsideCapturedBitmap()
    {
        var captureRegion = new ShareX.Avalonia.Platform.Abstractions.Capture.PhysicalRectangle(100, 50, 200, 100);

        var placement = DxgiCursorCompositionHelper.CreatePlacement(
            includeCursor: true,
            cursorVisible: true,
            cursorPosition: new Point(350, 80),
            hotspot: Point.Empty,
            cursorSize: new Size(32, 32),
            captureRegion);

        Assert.That(placement.ShouldDraw, Is.False);
    }

    [Test]
    public void CreateDxgiCursorPlacement_UsesDefaultExtentForSystemSizedCursor()
    {
        var captureRegion = new ShareX.Avalonia.Platform.Abstractions.Capture.PhysicalRectangle(100, 50, 200, 100);

        var placement = DxgiCursorCompositionHelper.CreatePlacement(
            includeCursor: true,
            cursorVisible: true,
            cursorPosition: new Point(90, 70),
            hotspot: Point.Empty,
            cursorSize: Size.Empty,
            captureRegion);

        Assert.That(placement.ShouldDraw, Is.True);
    }

    [Test]
    public void CreateDxgiCursorPlacement_MapsNegativeVirtualDesktopCursorToFullScreenBitmap()
    {
        var captureRegion = new ShareX.Avalonia.Platform.Abstractions.Capture.PhysicalRectangle(-1920, -200, 3840, 1280);

        var placement = DxgiCursorCompositionHelper.CreatePlacement(
            includeCursor: true,
            cursorVisible: true,
            cursorPosition: new Point(-1900, -180),
            hotspot: new Point(10, 8),
            cursorSize: new Size(32, 32),
            captureRegion);

        Assert.Multiple(() =>
        {
            Assert.That(placement.ShouldDraw, Is.True);
            Assert.That(placement.DrawOffset, Is.EqualTo(new Point(10, 12)));
        });
    }

    [Test]
    public void CreateDxgiCursorCaptureRegion_UsesCapturedDxgiBounds()
    {
        var captureRegion = DxgiCursorCompositionHelper.CreateCaptureRegion(
            left: -1920,
            top: -200,
            right: 2560,
            bottom: 1440);

        var placement = DxgiCursorCompositionHelper.CreatePlacement(
            includeCursor: true,
            cursorVisible: true,
            cursorPosition: new Point(-1900, -180),
            hotspot: new Point(10, 8),
            cursorSize: new Size(32, 32),
            captureRegion);

        Assert.Multiple(() =>
        {
            Assert.That(captureRegion.X, Is.EqualTo(-1920));
            Assert.That(captureRegion.Width, Is.EqualTo(4480));
            Assert.That(placement.ShouldDraw, Is.True);
            Assert.That(placement.DrawOffset, Is.EqualTo(new Point(10, 12)));
        });
    }

    [TestCase(0, 20, 30, 60, 80)]
    [TestCase(90, 30, 180, 80, 220)]
    [TestCase(180, 140, 160, 180, 210)]
    [TestCase(270, 120, 20, 170, 60)]
    public void CreateDxgiSourceBox_MapsDesktopRegionToUnrotatedDuplicationTexture(
        int rotation,
        int expectedLeft,
        int expectedTop,
        int expectedRight,
        int expectedBottom)
    {
        var desktopLocalRegion = new ShareX.Avalonia.Platform.Abstractions.Capture.PhysicalRectangle(20, 30, 40, 50);
        var sourceBox = DxgiRotationHelper.CreateSourceBox(desktopLocalRegion, rotation, sourceWidth: 200, sourceHeight: 240);

        Assert.Multiple(() =>
        {
            Assert.That(sourceBox.Left, Is.EqualTo(expectedLeft));
            Assert.That(sourceBox.Top, Is.EqualTo(expectedTop));
            Assert.That(sourceBox.Right, Is.EqualTo(expectedRight));
            Assert.That(sourceBox.Bottom, Is.EqualTo(expectedBottom));
            Assert.That(sourceBox.Front, Is.EqualTo(0));
            Assert.That(sourceBox.Back, Is.EqualTo(1));
        });
    }

    [TestCase(0, 40, 50)]
    [TestCase(90, 50, 40)]
    [TestCase(180, 40, 50)]
    [TestCase(270, 50, 40)]
    public void CreateDxgiSourceBox_ReportsSourceDimensionsForRotation(int rotation, int expectedWidth, int expectedHeight)
    {
        var desktopLocalRegion = new ShareX.Avalonia.Platform.Abstractions.Capture.PhysicalRectangle(20, 30, 40, 50);
        var sourceBox = DxgiRotationHelper.CreateSourceBox(desktopLocalRegion, rotation, sourceWidth: 200, sourceHeight: 240);

        Assert.Multiple(() =>
        {
            Assert.That(DxgiRotationHelper.GetSourceWidth(sourceBox), Is.EqualTo(expectedWidth));
            Assert.That(DxgiRotationHelper.GetSourceHeight(sourceBox), Is.EqualTo(expectedHeight));
        });
    }

    [Test]
    public void TryCreateDxgiCropRect_TranslatesAndClampsFractionalScreenCoordinates()
    {
        var virtualBounds = new Rectangle(-100, -50, 300, 200);
        var rect = new SKRect(-99.6f, -49.2f, 25.1f, 80.9f);

        bool created = DxgiCropRectHelper.TryCreateCropRect(rect, virtualBounds, 300, 200, out var cropRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(cropRect, Is.EqualTo(new SKRectI(0, 0, 126, 131)));
        });
    }

    [Test]
    public void TryCreateDxgiCropRect_ScalesVirtualDesktopCoordinatesToBitmapPixels()
    {
        var virtualBounds = new Rectangle(-100, -50, 150, 100);
        var rect = new SKRect(-99.6f, -49.2f, 25.1f, 20.4f);

        bool created = DxgiCropRectHelper.TryCreateCropRect(rect, virtualBounds, 300, 200, out var cropRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(cropRect, Is.EqualTo(new SKRectI(0, 1, 251, 141)));
        });
    }

    [Test]
    public void TryCreateDxgiCropRect_RejectsEmptyVirtualBounds()
    {
        var virtualBounds = new Rectangle(0, 0, 0, 100);
        var rect = new SKRect(0, 0, 10, 10);

        bool created = DxgiCropRectHelper.TryCreateCropRect(rect, virtualBounds, 100, 100, out var cropRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(cropRect, Is.EqualTo(default(SKRectI)));
        });
    }

    [Test]
    public void TryCreateDxgiCropRect_RejectsNonFiniteCoordinatesBeforeCasting()
    {
        var virtualBounds = new Rectangle(0, 0, 100, 100);
        var rect = new SKRect(0, 0, float.PositiveInfinity, 10);

        bool created = DxgiCropRectHelper.TryCreateCropRect(rect, virtualBounds, 100, 100, out var cropRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(cropRect, Is.EqualTo(default(SKRectI)));
        });
    }

    [Test]
    public void TryCreateDxgiCropRect_ClampsHugeFiniteCoordinatesWithoutOverflowing()
    {
        var virtualBounds = new Rectangle(int.MinValue, int.MinValue, 100, 100);
        var rect = new SKRect(int.MaxValue - 10f, int.MaxValue - 10f, float.MaxValue, float.MaxValue);

        bool created = DxgiCropRectHelper.TryCreateCropRect(rect, virtualBounds, 100, 100, out var cropRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(cropRect, Is.EqualTo(default(SKRectI)));
        });
    }

    [Test]
    public void TryCreateGdiCaptureRect_RoundsOutwardAndClampsToVirtualScreen()
    {
        var screenBounds = new Rectangle(-100, -50, 300, 200);
        var rect = new SKRect(-99.6f, -49.2f, 25.1f, 80.9f);

        bool created = GdiCaptureRectHelper.TryCreateCaptureRect(rect, screenBounds, out var captureRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(captureRect, Is.EqualTo(new Rectangle(-100, -50, 126, 131)));
        });
    }

    [Test]
    public void TryCreateGdiCaptureRect_RejectsNonFiniteCoordinatesBeforeCasting()
    {
        var screenBounds = new Rectangle(0, 0, 100, 100);
        var rect = new SKRect(0, 0, float.PositiveInfinity, 10);

        bool created = GdiCaptureRectHelper.TryCreateCaptureRect(rect, screenBounds, out var captureRect);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(captureRect, Is.EqualTo(default(Rectangle)));
        });
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public bool Disposed { get; private set; }
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            Disposed = true;
            DisposeCount++;
        }
    }
}
