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

using System.Drawing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.Models;
using DrawingPoint = System.Drawing.Point;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;
using DrawingSizeF = System.Drawing.SizeF;
using PixelRect = XerahS.RegionCapture.Models.PixelRect;

namespace XerahS.RegionCapture.Services;

/// <summary>Snapshot of a capture overlay window measured once layout and scaling settle.</summary>
public sealed class OverlayWindowGeometryEntry
{
    public DateTime Time { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Backend { get; init; } = string.Empty;
    public string MonitorDeviceName { get; init; } = string.Empty;
    public DrawingRectangle ExpectedPhysicalBounds { get; init; }
    public DrawingRectangle ExpectedOverlayBounds { get; init; }
    public DrawingRectangle ActualBounds { get; init; }
    public DrawingPoint ClientOrigin { get; init; }
    public bool HasClientOrigin { get; init; }
    public DrawingSizeF ClientSize { get; init; }
    public double RenderScaling { get; init; }
    public double MonitorScaleFactor { get; init; }
    public uint WindowDpi { get; init; }
    public DrawingSize ImageSize { get; init; }
    public DrawingPoint? CursorScreen { get; init; }
    public DrawingPointF? CursorAvaloniaClientPixels { get; init; }
}

/// <summary>
/// Retains the most recent capture overlay geometry measurements for Display Diagnostics.
/// </summary>
public static class OverlayWindowGeometryLog
{
    private const int MaximumEntries = 10;
    private static readonly Lock EntriesLock = new();
    private static readonly List<OverlayWindowGeometryEntry> EntriesList = [];

    public static IReadOnlyList<OverlayWindowGeometryEntry> Entries
    {
        get
        {
            lock (EntriesLock)
            {
                return EntriesList.ToArray();
            }
        }
    }

    public static void Record(
        Window window,
        string source,
        MonitorInfo monitor,
        SKBitmap? backgroundBitmap,
        bool isAvaloniaWayland)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(monitor);

        DateTime openedTime = DateTime.Now;
        DrawingSize imageSize = backgroundBitmap is { Width: > 0, Height: > 0 }
            ? new DrawingSize(backgroundBitmap.Width, backgroundBitmap.Height)
            : DrawingSize.Empty;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                double renderScaling = double.IsFinite(window.RenderScaling) && window.RenderScaling > 0
                    ? window.RenderScaling
                    : 1.0;

                double clientWidth = window.ClientSize.Width > 0 ? window.ClientSize.Width : window.Bounds.Width;
                double clientHeight = window.ClientSize.Height > 0 ? window.ClientSize.Height : window.Bounds.Height;

                string backend = OperatingSystem.IsWindows()
                    ? "Win32"
                    : OperatingSystem.IsMacOS()
                        ? "macOS (AppKit)"
                        : isAvaloniaWayland
                            ? (WaylandProperties.IsLayerShellSupported(window) ? "Wayland (zwlr_layer_shell_v1)" : "Wayland")
                            : "X11";

                var expectedPhysical = ToDrawingRect(monitor.PhysicalBounds);
                var expectedOverlay = ToDrawingRect(monitor.OverlayBounds);

                IntPtr handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                NativeWindowGeometry? native = handle != IntPtr.Zero && PlatformServices.DisplayDiagnostics.IsWindowsNative
                    ? PlatformServices.DisplayDiagnostics.ReadNativeWindowGeometry(handle)
                    : null;

                DrawingRectangle actualBounds;
                DrawingPoint clientOrigin = DrawingPoint.Empty;
                bool hasClientOrigin = false;
                uint windowDpi = 0;
                DrawingPoint? cursorScreen = null;
                DrawingPointF? cursorAvaloniaClientPixels = null;
                string monitorName = monitor.DeviceName;

                if (native is NativeWindowGeometry nativeGeom)
                {
                    actualBounds = nativeGeom.Bounds;
                    clientOrigin = nativeGeom.ClientOrigin;
                    hasClientOrigin = nativeGeom.HasClientOrigin;
                    windowDpi = nativeGeom.Dpi;
                    if (!string.IsNullOrWhiteSpace(nativeGeom.MonitorDeviceName))
                    {
                        monitorName = nativeGeom.MonitorDeviceName;
                    }

                    if (nativeGeom.HasCursor)
                    {
                        cursorScreen = nativeGeom.Cursor;
                        global::Avalonia.Point clientPt = window.PointToClient(new global::Avalonia.PixelPoint(nativeGeom.Cursor.X, nativeGeom.Cursor.Y));
                        cursorAvaloniaClientPixels = new DrawingPointF(
                            (float)(clientPt.X * renderScaling),
                            (float)(clientPt.Y * renderScaling));
                    }
                }
                else
                {
                    int actualOriginX = isAvaloniaWayland
                        ? (int)Math.Round(monitor.OverlayBounds.X)
                        : window.Position.X;
                    int actualOriginY = isAvaloniaWayland
                        ? (int)Math.Round(monitor.OverlayBounds.Y)
                        : window.Position.Y;
                    int actualPhysW = Math.Max(0, (int)Math.Round(clientWidth * renderScaling));
                    int actualPhysH = Math.Max(0, (int)Math.Round(clientHeight * renderScaling));
                    actualBounds = new DrawingRectangle(actualOriginX, actualOriginY, actualPhysW, actualPhysH);
                }

                AddEntry(new OverlayWindowGeometryEntry
                {
                    Time = openedTime,
                    Source = source,
                    Backend = backend,
                    MonitorDeviceName = monitorName,
                    ExpectedPhysicalBounds = expectedPhysical,
                    ExpectedOverlayBounds = expectedOverlay,
                    ActualBounds = actualBounds,
                    ClientOrigin = clientOrigin,
                    HasClientOrigin = hasClientOrigin,
                    ClientSize = new DrawingSizeF((float)clientWidth, (float)clientHeight),
                    RenderScaling = renderScaling,
                    MonitorScaleFactor = monitor.ScaleFactor,
                    WindowDpi = windowDpi,
                    ImageSize = imageSize,
                    CursorScreen = cursorScreen,
                    CursorAvaloniaClientPixels = cursorAvaloniaClientPixels
                });
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to record overlay window geometry.");
            }
        }, DispatcherPriority.Background);
    }

    internal static void AddEntry(OverlayWindowGeometryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (EntriesLock)
        {
            EntriesList.Add(entry);
            if (EntriesList.Count > MaximumEntries)
            {
                EntriesList.RemoveAt(0);
            }
        }
    }

    internal static void ClearForTests()
    {
        lock (EntriesLock)
        {
            EntriesList.Clear();
        }
    }

    private static DrawingRectangle ToDrawingRect(PixelRect rect) => new(
        (int)Math.Round(rect.X),
        (int)Math.Round(rect.Y),
        (int)Math.Round(rect.Width),
        (int)Math.Round(rect.Height));
}
