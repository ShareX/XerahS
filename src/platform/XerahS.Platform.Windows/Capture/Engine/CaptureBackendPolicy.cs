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

using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Windows.Capture.Engine;

/// <summary>
/// Native Windows capture APIs, from most to least preferred.
/// </summary>
internal enum CaptureBackendKind
{
    /// <summary>DXGI Desktop Duplication (Windows 8+).</summary>
    DesktopDuplication,

    /// <summary>Windows.Graphics.Capture without the capture border (Windows 11 / Server 2022+).</summary>
    GraphicsCapture,

    /// <summary>GDI BitBlt. Always available.</summary>
    Gdi
}

/// <summary>
/// Decides which backends serve a request and in which order. Pure policy with no Windows calls.
/// </summary>
internal static class CaptureBackendPolicy
{
    private static readonly CaptureBackendKind[] PreferenceOrder =
    {
        CaptureBackendKind.DesktopDuplication,
        CaptureBackendKind.GraphicsCapture,
        CaptureBackendKind.Gdi
    };

    /// <summary>
    /// Resolves the capture backend policy supplied by the application layer.
    /// </summary>
    internal static bool ShouldUseModernCapture(CaptureOptions? options) =>
        options?.UseModernCapture ?? true;

    /// <summary>
    /// Returns the available backends to try, in order. GDI is the only backend used when
    /// modern capture is turned off, and the last resort otherwise.
    /// </summary>
    internal static IReadOnlyList<CaptureBackendKind> ResolveChain(
        bool modernCaptureRequested,
        IReadOnlyCollection<CaptureBackendKind> availableBackends)
    {
        var chain = new List<CaptureBackendKind>(PreferenceOrder.Length);

        foreach (CaptureBackendKind kind in PreferenceOrder)
        {
            if (!availableBackends.Contains(kind))
            {
                continue;
            }

            if (!modernCaptureRequested && kind != CaptureBackendKind.Gdi)
            {
                continue;
            }

            chain.Add(kind);
        }

        return chain;
    }
}
