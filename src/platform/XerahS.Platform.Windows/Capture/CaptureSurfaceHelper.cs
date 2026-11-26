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

/// <summary>
/// Creates the surface a capture is composed into. fp16 scRGB is the only format that can carry
/// values above 1.0, so it is used whenever an HDR output takes part in the capture.
/// </summary>
internal static class CaptureSurfaceHelper
{
    public static SKBitmap Create(Rectangle bounds, bool hdrSurface) =>
        hdrSurface
            ? new SKBitmap(new SKImageInfo(bounds.Width, bounds.Height, SKColorType.RgbaF16, SKAlphaType.Premul,
                SKColorSpace.CreateSrgbLinear()))
            : new SKBitmap(new SKImageInfo(bounds.Width, bounds.Height, SKColorType.Bgra8888, SKAlphaType.Premul,
                SKColorSpace.CreateSrgb()));
}
