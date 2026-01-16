using System;
using Avalonia;
using ShareX.Avalonia.Platform.Abstractions.Capture;
using ShareX.Avalonia.UI.Services;

namespace XerahS.UI.Views.RegionCapture;

internal sealed class RegionCaptureCoordinateMapper
{
    private readonly RegionCaptureService _captureService;
    private readonly LogicalPoint _windowLogicalOrigin;

    public RegionCaptureCoordinateMapper(
        RegionCaptureService captureService,
        LogicalPoint windowLogicalOrigin)
    {
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _windowLogicalOrigin = windowLogicalOrigin;
    }

    public PhysicalPoint WindowLogicalToPhysical(Point windowLogical)
    {
        var globalLogical = new LogicalPoint(
            windowLogical.X + _windowLogicalOrigin.X,
            windowLogical.Y + _windowLogicalOrigin.Y);

        return _captureService.LogicalToPhysical(globalLogical);
    }

    public Point PhysicalToWindowLogical(PhysicalPoint physical)
    {
        var globalLogical = _captureService.PhysicalToLogical(physical);

        return new Point(
            globalLogical.X - _windowLogicalOrigin.X,
            globalLogical.Y - _windowLogicalOrigin.Y);
    }
}
