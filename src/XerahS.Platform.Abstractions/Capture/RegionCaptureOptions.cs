using SkiaSharp;

namespace ShareX.Avalonia.Platform.Abstractions.Capture;

/// <summary>
/// Options for region capture operations.
/// </summary>
public sealed record RegionCaptureOptions
{
    /// <summary>
    /// Image quality/compression level (0-100).
    /// Only applicable for lossy formats. Default: 95.
    /// </summary>
    public int Quality { get; init; } = 95;

    /// <summary>
    /// Whether to include the mouse cursor in the capture.
    /// Default: false.
    /// </summary>
    public bool IncludeCursor { get; init; } = false;

    /// <summary>
    /// Preferred color type for the captured bitmap.
    /// Default: Bgra8888 (32-bit with alpha).
    /// </summary>
    public SKColorType ColorType { get; init; } = SKColorType.Bgra8888;

    /// <summary>
    /// Whether to use GPU acceleration if available.
    /// Default: true.
    /// </summary>
    public bool UseHardwareAcceleration { get; init; } = true;

    /// <summary>
    /// Timeout for capture operation in milliseconds.
    /// Default: 5000 (5 seconds).
    /// </summary>
    public int TimeoutMs { get; init; } = 5000;

    /// <summary>
    /// Default options for most capture scenarios.
    /// </summary>
    public static RegionCaptureOptions Default { get; } = new();
}
