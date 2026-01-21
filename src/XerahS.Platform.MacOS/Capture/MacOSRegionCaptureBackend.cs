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

namespace ShareX.Avalonia.Platform.macOS.Capture;

/// <summary>
/// macOS implementation of region capture using ScreenCaptureKit framework.
/// Falls back to Quartz/CoreGraphics and then CLI screencapture tool.
/// </summary>
public sealed class MacOSRegionCaptureBackend : IRegionCaptureBackend
{
    private readonly ICaptureStrategy _strategy;
    private bool _disposed;

    public event EventHandler<MonitorConfigurationChangedEventArgs>? ConfigurationChanged
    {
        add { }
        remove { }
    }

    public MacOSRegionCaptureBackend()
    {
        _strategy = SelectBestStrategy();
    }

    /// <summary>
    /// Select the best available capture strategy based on macOS version.
    /// </summary>
    private static ICaptureStrategy SelectBestStrategy()
    {
        // Try strategies in order of preference

        // 1. ScreenCaptureKit (macOS 12.3+, modern API with HDR support)
        if (ScreenCaptureKitStrategy.IsSupported())
        {
            try
            {
                return new ScreenCaptureKitStrategy();
            }
            catch
            {
                // Fall through to next strategy
            }
        }

        // 2. Quartz/CoreGraphics (macOS 10.6+, reliable GPU capture)
        if (QuartzCaptureStrategy.IsSupported())
        {
            try
            {
                return new QuartzCaptureStrategy();
            }
            catch
            {
                // Fall through to next strategy
            }
        }

        // 3. CLI screencapture (universal fallback)
        return new CliCaptureStrategy();
    }

    public MonitorInfo[] GetMonitors()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _strategy.GetMonitors();
    }

    public Task<CapturedBitmap> CaptureRegionAsync(
        PhysicalRectangle physicalRegion,
        RegionCaptureOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (physicalRegion.Width <= 0 || physicalRegion.Height <= 0)
            throw new ArgumentException($"Invalid region size: {physicalRegion.Width}×{physicalRegion.Height}");

        return _strategy.CaptureRegionAsync(physicalRegion, options);
    }

    public BackendCapabilities GetCapabilities()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _strategy.GetCapabilities();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _strategy?.Dispose();
    }
}
