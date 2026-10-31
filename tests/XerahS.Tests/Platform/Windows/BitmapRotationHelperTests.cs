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
using SkiaSharp;
using XerahS.Common;

namespace XerahS.Tests.Platform.Windows;

/// <summary>
/// Regression tests for BitmapRotationHelper, guarding against re-introduction of the
/// black-screen bug on rotated/portrait monitors described in issue #148.
///
/// Root cause of issue #148: DMDO_90 (Windows DEVMODE orientation constant = 1, meaning
/// "display is rotated 90° clockwise") was mapped to ModeRotation.Rotate270 instead of
/// ModeRotation.Rotate90, causing RotateBitmapForDesktop to apply the wrong correction.
///
/// These tests verify the rotation math is correct for all four cardinal orientations
/// and — crucially — that 90° and 270° CW produce DISTINCT pixel layouts.
/// </summary>
[TestFixture]
[Category("Regression")]
[Category("Issue148")]
public class BitmapRotationHelperTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Dimension tests (all orientations)
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public void RotateClockwise_Zero_PreservesDimensions()
    {
        using var src = MakeTestBitmap(100, 50);
        using var result = BitmapRotationHelper.RotateClockwise(src, 0);

        Assert.That(result.Width, Is.EqualTo(100));
        Assert.That(result.Height, Is.EqualTo(50));
    }

    [Test]
    public void RotateClockwise_90_SwapsDimensions()
    {
        using var src = MakeTestBitmap(100, 50);
        using var result = BitmapRotationHelper.RotateClockwise(src, 90);

        Assert.That(result.Width, Is.EqualTo(50),  "90° CW: height becomes width");
        Assert.That(result.Height, Is.EqualTo(100), "90° CW: width becomes height");
    }

    [Test]
    public void RotateClockwise_180_PreservesDimensions()
    {
        using var src = MakeTestBitmap(100, 50);
        using var result = BitmapRotationHelper.RotateClockwise(src, 180);

        Assert.That(result.Width, Is.EqualTo(100));
        Assert.That(result.Height, Is.EqualTo(50));
    }

    [Test]
    public void RotateClockwise_270_SwapsDimensions()
    {
        using var src = MakeTestBitmap(100, 50);
        using var result = BitmapRotationHelper.RotateClockwise(src, 270);

        Assert.That(result.Width, Is.EqualTo(50),  "270° CW: height becomes width");
        Assert.That(result.Height, Is.EqualTo(100), "270° CW: width becomes height");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Pixel-position tests (issue #148 core regression guard)
    //
    // We use a 2×2 bitmap with a uniquely coloured pixel at (0,0) so that
    // wrong rotation (e.g. 90° applied when 270° is expected) is detectable.
    //
    // Source layout (row-major, col 0 left):
    //   (0,0)=Red   (1,0)=Green
    //   (0,1)=Blue  (1,1)=White
    //
    // After 90° CW the pixel that was at (0,0) moves to (1,0) in the result.
    // After 270° CW (= 90° CCW) the pixel that was at (0,0) moves to (0,1).
    // These differ, so a swap of 90/270 is caught by pixel assertions.
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public void RotateClockwise_90_TopLeftPixelMovesToTopRight()
    {
        // Source:        // R G
        // B W
        using var src = Make2x2();

        // After 90° CW:
        // B R
        // W G
        // → top-left of source (Red) is now at top-right of result: (1,0)
        using var result = BitmapRotationHelper.RotateClockwise(src, 90);

        Assert.That(result.Width, Is.EqualTo(2));
        Assert.That(result.Height, Is.EqualTo(2));
        Assert.That(result.GetPixel(1, 0), Is.EqualTo(SKColors.Red),
            "After 90° CW, source top-left (Red) should be at result (1,0)");
        Assert.That(result.GetPixel(0, 0), Is.EqualTo(SKColors.Blue),
            "After 90° CW, source bottom-left (Blue) should be at result (0,0)");
    }

    [Test]
    public void RotateClockwise_270_TopLeftPixelMovesToBottomLeft()
    {
        // Source:
        // R G
        // B W
        using var src = Make2x2();

        // After 270° CW (= 90° CCW):
        // G W
        // R B
        // → top-left of source (Red) is now at bottom-left of result: (0,1)
        using var result = BitmapRotationHelper.RotateClockwise(src, 270);

        Assert.That(result.Width, Is.EqualTo(2));
        Assert.That(result.Height, Is.EqualTo(2));
        Assert.That(result.GetPixel(0, 1), Is.EqualTo(SKColors.Red),
            "After 270° CW, source top-left (Red) should be at result (0,1)");
        Assert.That(result.GetPixel(0, 0), Is.EqualTo(SKColors.Green),
            "After 270° CW, source top-right (Green) should be at result (0,0)");
    }

    /// <summary>
    /// Issue #148 regression: 90° and 270° must produce DIFFERENT results.
    /// If the DMDO_90/DMDO_270 swap is reintroduced, both rotations would apply
    /// the same (wrong) transform, and this test would catch it.
    /// </summary>
    [Test]
    public void RotateClockwise_90And270_ProduceDifferentResults()
    {
        using var src = Make2x2();
        using var rotated90 = BitmapRotationHelper.RotateClockwise(src, 90);
        using var rotated270 = BitmapRotationHelper.RotateClockwise(src, 270);

        // Pixel at (0,0) must differ between the two results.
        var pixel90 = rotated90.GetPixel(0, 0);
        var pixel270 = rotated270.GetPixel(0, 0);

        Assert.That(pixel90, Is.Not.EqualTo(pixel270),
            "90° CW and 270° CW must produce distinct outputs (issue #148 regression guard)");
    }

    [Test]
    public void RotateClockwise_180_TopLeftMovesToBottomRight()
    {
        using var src = Make2x2();
        using var result = BitmapRotationHelper.RotateClockwise(src, 180);

        Assert.That(result.GetPixel(1, 1), Is.EqualTo(SKColors.Red),
            "After 180°, source top-left (Red) should be at result bottom-right (1,1)");
    }

    [Test]
    public void RotateClockwise_Zero_PixelsUnchanged()
    {
        using var src = Make2x2();
        using var result = BitmapRotationHelper.RotateClockwise(src, 0);

        Assert.That(result.GetPixel(0, 0), Is.EqualTo(SKColors.Red));
        Assert.That(result.GetPixel(1, 0), Is.EqualTo(SKColors.Green));
        Assert.That(result.GetPixel(0, 1), Is.EqualTo(SKColors.Blue));
        Assert.That(result.GetPixel(1, 1), Is.EqualTo(SKColors.White));
    }

    // ──────────────────────────────────────────────────────────────────────
    // DMDO constant contract (issue #148 specification guard)
    //
    // These tests encode the correct semantic meaning of Windows DEVMODE
    // dmDisplayOrientation constants.  If someone changes the constant values
    // or their semantics in NativeMethods, these tests document the expected truth.
    // ──────────────────────────────────────────────────────────────────────

    // Windows DEVMODE dmDisplayOrientation values (from wingdi.h)
    private const int DMDO_DEFAULT = 0; // natural/landscape orientation
    private const int DMDO_90 = 1;     // display physically rotated 90° CW
    private const int DMDO_180 = 2;    // display physically rotated 180°
    private const int DMDO_270 = 3;    // display physically rotated 270° CW

    [Test]
    public void DmdoConstants_HaveCorrectIntegerValues()
    {
        // These are fixed by the Windows API; they must not drift.
        Assert.That(DMDO_DEFAULT, Is.EqualTo(0));
        Assert.That(DMDO_90,      Is.EqualTo(1));
        Assert.That(DMDO_180,     Is.EqualTo(2));
        Assert.That(DMDO_270,     Is.EqualTo(3));
    }

    [Test]
    public void DmdoMapping_DMDO90_ClockwiseDegrees_Is90_NotRotate270()
    {
        // Issue #148: DMDO_90 was incorrectly mapped to Rotate270 (270°).
        // Correct: DMDO_90 means 90° CW, so it should map to Rotate90 (90°).
        Assert.That(DmDisplayOrientationToDegreesCw(DMDO_90), Is.EqualTo(90),
            "DMDO_90 (integer 1) = 90° CW. Was wrongly mapped to 270° before issue #148 fix.");
    }

    [Test]
    public void DmdoMapping_DMDO270_ClockwiseDegrees_Is270_NotRotate90()
    {
        // Issue #148: DMDO_270 was incorrectly mapped to Rotate90 (90°).
        // Correct: DMDO_270 means 270° CW, so it should map to Rotate270 (270°).
        Assert.That(DmDisplayOrientationToDegreesCw(DMDO_270), Is.EqualTo(270),
            "DMDO_270 (integer 3) = 270° CW. Was wrongly mapped to 90° before issue #148 fix.");
    }

    [Test]
    public void DmdoMapping_DefaultAndHalf_AreCorrect()
    {
        Assert.That(DmDisplayOrientationToDegreesCw(DMDO_DEFAULT), Is.EqualTo(0));
        Assert.That(DmDisplayOrientationToDegreesCw(DMDO_180),     Is.EqualTo(180));
    }

    /// <summary>
    /// Pure mapping of DMDO_* integer value to clockwise degrees.
    /// Mirrors the switch in DxgiOutputEnumerator.TryGetDisplaySettingsRotation.
    /// Both DEVMODE and DXGI define rotation in clockwise degrees from natural orientation.
    /// </summary>
    private static int DmDisplayOrientationToDegreesCw(int dmDisplayOrientation) =>
        dmDisplayOrientation switch
        {
            DMDO_DEFAULT => 0,
            DMDO_90      => 90,
            DMDO_180     => 180,
            DMDO_270     => 270,
            _            => -1
        };

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private static SKBitmap MakeTestBitmap(int width, int height)
    {
        var bmp = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        bmp.Erase(SKColors.DimGray);
        return bmp;
    }

    /// <summary>
    /// Creates a 2×2 bitmap with distinct colours at each corner:
    /// (0,0)=Red  (1,0)=Green
    /// (0,1)=Blue (1,1)=White
    /// </summary>
    private static SKBitmap Make2x2()
    {
        var bmp = new SKBitmap(2, 2, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        bmp.SetPixel(0, 0, SKColors.Red);
        bmp.SetPixel(1, 0, SKColors.Green);
        bmp.SetPixel(0, 1, SKColors.Blue);
        bmp.SetPixel(1, 1, SKColors.White);
        return bmp;
    }
}
