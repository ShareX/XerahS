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

using SkiaSharp;
using XerahS.Common;

namespace XerahS.Platform.Windows.Capture.Engine;

/// <summary>
/// Runs a capture request through the backend chain chosen by <see cref="CaptureBackendPolicy"/>.
/// The first backend that returns a bitmap wins. A backend that throws or returns null hands over to the next one.
/// </summary>
internal sealed class ScreenCaptureEngine
{
    private readonly Dictionary<CaptureBackendKind, IScreenCaptureBackend> _backends;

    public ScreenCaptureEngine(IEnumerable<IScreenCaptureBackend> backends)
    {
        ArgumentNullException.ThrowIfNull(backends);

        _backends = new Dictionary<CaptureBackendKind, IScreenCaptureBackend>();
        foreach (IScreenCaptureBackend backend in backends)
        {
            if (!_backends.TryAdd(backend.Kind, backend))
            {
                throw new ArgumentException($"More than one {backend.Kind} backend was supplied.", nameof(backends));
            }
        }
    }

    public IReadOnlyCollection<CaptureBackendKind> AvailableBackends => _backends.Keys;

    public Task<SKBitmap?> CaptureAsync(ScreenCaptureRequest request) => Task.Run(() => Capture(request));

    internal SKBitmap? Capture(ScreenCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<CaptureBackendKind> chain = CaptureBackendPolicy.ResolveChain(
            request.ModernCaptureRequested,
            AvailableBackends);

        if (!request.ModernCaptureRequested)
        {
            DebugHelper.WriteLine("Screen capture: modern capture disabled (UseModernCapture=false); using GDI.");
        }

        for (int i = 0; i < chain.Count; i++)
        {
            IScreenCaptureBackend backend = _backends[chain[i]];
            SKBitmap? bitmap = TryCaptureWith(backend, request);
            if (bitmap != null)
            {
                return bitmap;
            }

            string next = i + 1 < chain.Count ? _backends[chain[i + 1]].Name : "no backend left";
            DebugHelper.WriteLine($"Screen capture: {backend.Name} returned no image; falling back to {next}.");
        }

        return null;
    }

    private static SKBitmap? TryCaptureWith(IScreenCaptureBackend backend, ScreenCaptureRequest request)
    {
        try
        {
            return backend.TryCapture(request);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"Screen capture: {backend.Name} failed");
            return null;
        }
    }
}
