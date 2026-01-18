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

namespace ShareX.Avalonia.Platform.Linux.Capture;

/// <summary>
/// Linux implementation with automatic X11/Wayland detection.
/// Strategy selection: Wayland portal → X11 XGetImage → CLI tools
/// </summary>
public sealed class LinuxRegionCaptureBackend : IRegionCaptureBackend
{
    private readonly ICaptureStrategy _strategy;
    private readonly string _sessionType;
    private bool _disposed;

    public event EventHandler<MonitorConfigurationChangedEventArgs>? ConfigurationChanged
    {
        add { }
        remove { }
    }

    public LinuxRegionCaptureBackend()
    {
        _sessionType = DetectSessionType();
        _strategy = SelectBestStrategy();
    }

    private static string DetectSessionType()
    {
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        return sessionType?.ToLower() ?? "x11"; // Default to X11 if unknown
    }

    private ICaptureStrategy SelectBestStrategy()
    {
        if (_sessionType == "wayland")
        {
            // Wayland: try portal, fall back to XWayland/CLI
            if (WaylandPortalStrategy.IsSupported())
            {
                try
                {
                    return new WaylandPortalStrategy();
                }
                catch
                {
                    // Fall through
                }
            }

            // Try XWayland fallback
            if (X11GetImageStrategy.IsSupported())
            {
                try
                {
                    return new X11GetImageStrategy();
                }
                catch
                {
                    // Fall through
                }
            }
        }
        else // X11 or unknown
        {
            // X11: try direct capture
            if (X11GetImageStrategy.IsSupported())
            {
                try
                {
                    return new X11GetImageStrategy();
                }
                catch
                {
                    // Fall through
                }
            }
        }

        // Universal CLI fallback
        return new LinuxCliCaptureStrategy();
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
