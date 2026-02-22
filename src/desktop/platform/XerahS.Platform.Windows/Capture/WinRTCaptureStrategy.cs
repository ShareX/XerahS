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
using System;
using System.Threading.Tasks;
using ShareX.Avalonia.Platform.Abstractions.Capture;

namespace ShareX.Avalonia.Platform.Windows.Capture;

/// <summary>
/// Capture strategy using Windows.Graphics.Capture API (WinRT).
/// Modern API available on Windows 10 1803+ with better HDR support.
/// Falls back to GDI+ on earlier Windows versions.
/// </summary>
internal sealed class WinRTCaptureStrategy : ICaptureStrategy
{
    public string Name => "WinRT Graphics Capture";

    public static bool IsSupported()
    {
        // Windows 10 1803+ (10.0.17134)
        var version = Environment.OSVersion.Version;
        return version.Major >= 10 && version.Build >= 17134;
    }

    public MonitorInfo[] GetMonitors()
    {
        // TODO: Implement Windows.Graphics.Capture monitor enumeration
        // For now, delegate to GDI strategy
        var gdiStrategy = new GdiCaptureStrategy();
        return gdiStrategy.GetMonitors();
    }

    public Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options)
    {
        // TODO: Implement Windows.Graphics.Capture region capture
        // This requires:
        // 1. GraphicsCaptureItem creation for the monitor
        // 2. Direct3D11CaptureFramePool setup
        // 3. Frame capture and extraction
        // 4. Conversion to SKBitmap
        //
        // For now, fall back to GDI strategy
        var gdiStrategy = new GdiCaptureStrategy();
        return gdiStrategy.CaptureRegionAsync(physicalRegion, options);
    }

    public BackendCapabilities GetCapabilities()
    {
        return new BackendCapabilities
        {
            BackendName = "WinRT Graphics Capture",
            Version = "10.0.17134+",
            SupportsHardwareAcceleration = true,
            SupportsCursorCapture = true,
            SupportsHDR = true,
            SupportsPerMonitorDpi = true,
            SupportsMonitorHotplug = true,
            MaxCaptureResolution = 16384,
            RequiresPermission = false
        };
    }

    public void Dispose()
    {
        // TODO: Cleanup WinRT resources
    }
}
