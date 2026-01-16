using SkiaSharp;
using System;

namespace ShareX.Avalonia.Platform.Abstractions.Capture;

/// <summary>
/// Represents a captured bitmap with metadata about the capture.
/// </summary>
public sealed class CapturedBitmap : IDisposable
{
    /// <summary>
    /// The captured bitmap data.
    /// </summary>
    public SKBitmap Bitmap { get; }

    /// <summary>
    /// The physical region that was captured.
    /// </summary>
    public PhysicalRectangle Region { get; }

    /// <summary>
    /// The DPI scale factor of the monitor where the capture occurred.
    /// </summary>
    public double ScaleFactor { get; }

    /// <summary>
    /// Timestamp when the capture occurred.
    /// </summary>
    public DateTime CapturedAt { get; }

    public CapturedBitmap(SKBitmap bitmap, PhysicalRectangle region, double scaleFactor)
    {
        Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        Region = region;
        ScaleFactor = scaleFactor;
        CapturedAt = DateTime.UtcNow;
    }

    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;
    public SKColorType ColorType => Bitmap.ColorType;

    public void Dispose()
    {
        Bitmap?.Dispose();
    }
}
