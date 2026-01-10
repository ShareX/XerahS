namespace ShareX.Avalonia.Platform.Abstractions.Capture;

/// <summary>
/// Describes the capabilities of a region capture backend.
/// Used for feature detection and UI adaptation.
/// </summary>
public sealed record BackendCapabilities
{
    /// <summary>
    /// Whether the backend supports hardware-accelerated capture.
    /// </summary>
    public bool SupportsHardwareAcceleration { get; init; }

    /// <summary>
    /// Whether the backend can capture the mouse cursor.
    /// </summary>
    public bool SupportsCursorCapture { get; init; }

    /// <summary>
    /// Whether the backend can capture HDR content (high dynamic range).
    /// </summary>
    public bool SupportsHDR { get; init; }

    /// <summary>
    /// Whether the backend supports per-monitor DPI awareness.
    /// </summary>
    public bool SupportsPerMonitorDpi { get; init; }

    /// <summary>
    /// Whether the backend can detect monitor configuration changes at runtime.
    /// </summary>
    public bool SupportsMonitorHotplug { get; init; }

    /// <summary>
    /// Maximum capture resolution (width or height).
    /// 0 means unlimited (system memory is the limit).
    /// </summary>
    public int MaxCaptureResolution { get; init; }

    /// <summary>
    /// Backend implementation name for diagnostics.
    /// Examples: "DXGI", "ScreenCaptureKit", "X11 XGetImage", "Wayland Portal"
    /// </summary>
    public required string BackendName { get; init; }

    /// <summary>
    /// Backend version or API level.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Whether this backend requires user permissions/approval for capture.
    /// Common on Wayland and macOS.
    /// </summary>
    public bool RequiresPermission { get; init; }

    public override string ToString() => $"{BackendName} {Version}".Trim();
}
