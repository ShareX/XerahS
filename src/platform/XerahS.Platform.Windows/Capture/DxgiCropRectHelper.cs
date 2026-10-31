#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using System.Drawing;
using SkiaSharp;

namespace XerahS.Platform.Windows.Capture;

internal static class DxgiCropRectHelper
{
    public static bool TryCreateCropRect(SKRect rect, Rectangle virtualBounds, int bitmapWidth, int bitmapHeight, out SKRectI cropRect)
    {
        cropRect = default;

        if (bitmapWidth <= 0 || bitmapHeight <= 0 ||
            virtualBounds.Width <= 0 || virtualBounds.Height <= 0 ||
            !IsFinite(rect.Left) || !IsFinite(rect.Top) || !IsFinite(rect.Right) || !IsFinite(rect.Bottom))
        {
            return false;
        }

        double scaleX = bitmapWidth / (double)virtualBounds.Width;
        double scaleY = bitmapHeight / (double)virtualBounds.Height;
        double left = Math.Floor((rect.Left - virtualBounds.X) * scaleX);
        double top = Math.Floor((rect.Top - virtualBounds.Y) * scaleY);
        double right = Math.Ceiling((rect.Right - virtualBounds.X) * scaleX);
        double bottom = Math.Ceiling((rect.Bottom - virtualBounds.Y) * scaleY);

        if (right <= left || bottom <= top)
        {
            return false;
        }

        left = Math.Clamp(left, 0, bitmapWidth);
        top = Math.Clamp(top, 0, bitmapHeight);
        right = Math.Clamp(right, 0, bitmapWidth);
        bottom = Math.Clamp(bottom, 0, bitmapHeight);

        if (right <= left || bottom <= top)
        {
            return false;
        }

        cropRect = new SKRectI((int)left, (int)top, (int)right, (int)bottom);
        return cropRect.Width > 0 && cropRect.Height > 0;
    }

    /// <summary>
    /// Maps a crop rectangle made by <see cref="TryCreateCropRect"/> back to desktop coordinates, so only the
    /// outputs under it need to be captured. This is exact only when the duplicated outputs cover the same size
    /// as the reported virtual screen (crop scale 1:1). Otherwise the caller must capture the whole desktop and crop.
    /// </summary>
    /// <param name="cropRect">Crop rectangle in the pixel space of a whole-desktop capture.</param>
    /// <param name="desktopBounds">Union of the duplicated outputs, in desktop coordinates.</param>
    /// <param name="virtualBounds">Virtual screen bounds used to make <paramref name="cropRect"/>.</param>
    public static bool TryMapCropToDesktop(SKRectI cropRect, Rectangle desktopBounds, Rectangle virtualBounds, out Rectangle desktopRect)
    {
        desktopRect = Rectangle.Empty;

        if (desktopBounds.Width != virtualBounds.Width || desktopBounds.Height != virtualBounds.Height ||
            cropRect.Width <= 0 || cropRect.Height <= 0)
        {
            return false;
        }

        desktopRect = new Rectangle(
            desktopBounds.X + cropRect.Left,
            desktopBounds.Y + cropRect.Top,
            cropRect.Width,
            cropRect.Height);
        return true;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
