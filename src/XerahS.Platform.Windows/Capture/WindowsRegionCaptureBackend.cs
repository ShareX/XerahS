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
/// Windows implementation of region capture using DXGI Desktop Duplication.
/// Falls back to WinRT Graphics Capture and then GDI+ if DXGI is unavailable.
/// </summary>
public sealed class WindowsRegionCaptureBackend : IRegionCaptureBackend
{
    private readonly ICaptureStrategy _strategy;
    private bool _disposed;

#pragma warning disable CS0067 // Event is never used - placeholder for future display change notifications
    public event EventHandler<MonitorConfigurationChangedEventArgs>? ConfigurationChanged;
#pragma warning restore CS0067

    public WindowsRegionCaptureBackend()
    {
        _strategy = SelectBestStrategy();

        // TODO: Monitor for display configuration changes
        // SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>
    /// Select the best available capture strategy based on OS version and capabilities.
    /// </summary>
    private static ICaptureStrategy SelectBestStrategy()
    {
        // Try strategies in order of preference (best to worst)

        // 1. DXGI Desktop Duplication (Windows 8+, hardware-accelerated)
        if (DxgiCaptureStrategy.IsSupported())
        {
            try
            {
                return new DxgiCaptureStrategy();
            }
            catch
            {
                // Fall through to next strategy
            }
        }

        // 2. WinRT Graphics Capture (Windows 10 1803+, modern API)
        if (WinRTCaptureStrategy.IsSupported())
        {
            try
            {
                return new WinRTCaptureStrategy();
            }
            catch
            {
                // Fall through to next strategy
            }
        }

        // 3. GDI+ BitBlt (fallback, works on all Windows versions)
        return new GdiCaptureStrategy();
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

        // Validate region
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

        // TODO: Unregister display change events
        // SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        _strategy?.Dispose();
    }

    /*
    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Notify listeners that monitor configuration changed
        var monitors = GetMonitors();
        ConfigurationChanged?.Invoke(this, new MonitorConfigurationChangedEventArgs(
            MonitorChangeType.LayoutChanged, monitors));
    }
    */
}
