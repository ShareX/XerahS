using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using SkiaSharp;

namespace XerahS.Platform.Windows.Capture;

internal readonly record struct CursorOverlayPlacement(bool ShouldDraw, Point DrawOffset);

internal static class DxgiCursorCompositionHelper
{
    private const int DefaultCursorExtent = 32;

    /// <summary>Largest cursor Windows draws (accessibility size 15 of a 256px cursor).</summary>
    private const int MaxSystemCursorExtent = 256;

    public static CursorOverlayPlacement CreatePlacement(
        bool includeCursor,
        bool cursorVisible,
        Point cursorPosition,
        Point hotspot,
        Size cursorSize,
        Rectangle captureRegion)
    {
        if (!includeCursor || !cursorVisible || captureRegion.Width <= 0 || captureRegion.Height <= 0)
            return default;

        int width = cursorSize.Width > 0 ? cursorSize.Width : DefaultCursorExtent;
        int height = cursorSize.Height > 0 ? cursorSize.Height : DefaultCursorExtent;

        int drawX = cursorPosition.X - hotspot.X - captureRegion.X;
        int drawY = cursorPosition.Y - hotspot.Y - captureRegion.Y;

        if (drawX >= captureRegion.Width || drawY >= captureRegion.Height ||
            drawX + width <= 0 || drawY + height <= 0)
        {
            return default;
        }

        return new CursorOverlayPlacement(true, new Point(drawX, drawY));
    }

    [SupportedOSPlatform("windows")]
    public static bool TryCompositeCursor(
        SKBitmap bitmap,
        bool cursorVisible,
        Point cursorPosition,
        Point hotspot,
        Size cursorSize,
        Rectangle captureRegion,
        Action<IntPtr, Point> drawCursor)
    {
        var placement = CreatePlacement(
            includeCursor: true,
            cursorVisible,
            cursorPosition,
            hotspot,
            cursorSize,
            captureRegion);

        if (!placement.ShouldDraw)
            return false;

        // Draw into a cursor-sized overlay rather than a capture-sized one: the cost no longer scales with the
        // capture area, and no PNG round trip is needed to hand the pixels to Skia.
        int overlayWidth = cursorSize.Width > 0 ? cursorSize.Width : MaxSystemCursorExtent;
        int overlayHeight = cursorSize.Height > 0 ? cursorSize.Height : MaxSystemCursorExtent;
        var cursorOrigin = new Point(
            captureRegion.X + placement.DrawOffset.X,
            captureRegion.Y + placement.DrawOffset.Y);

        using var overlay = new Bitmap(overlayWidth, overlayHeight, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(overlay))
        {
            graphics.Clear(Color.Transparent);
            IntPtr hdc = graphics.GetHdc();
            try
            {
                drawCursor(hdc, cursorOrigin);
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
        }

        using var cursorBitmap = new SKBitmap(new SKImageInfo(overlayWidth, overlayHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        BitmapData data = overlay.LockBits(
            new Rectangle(0, 0, overlayWidth, overlayHeight),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            BgraRowCopyHelper.CopyRows(
                data.Scan0,
                data.Stride,
                cursorBitmap.GetPixels(),
                cursorBitmap.RowBytes,
                overlayWidth * 4,
                overlayHeight);
        }
        finally
        {
            overlay.UnlockBits(data);
        }
        cursorBitmap.NotifyPixelsChanged();

        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { BlendMode = SKBlendMode.SrcOver };
        canvas.DrawBitmap(cursorBitmap, placement.DrawOffset.X, placement.DrawOffset.Y, SKSamplingOptions.Default, paint);

        return true;
    }
}
