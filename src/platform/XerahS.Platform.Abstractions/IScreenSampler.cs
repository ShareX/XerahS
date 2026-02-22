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

namespace XerahS.Platform.Abstractions
{
    /// <summary>
    /// Direct screen pixel sampling for ground truth verification.
    /// Implementations should use a different code path than IScreenCaptureService
    /// to provide truly independent verification.
    /// </summary>
    public interface IScreenSampler
    {
        /// <summary>
        /// Sample pixels directly from the screen at the specified global coordinates.
        /// This should use a different underlying API than the regular screen capture
        /// to provide independent verification (e.g., if screen capture uses DXGI,
        /// this might use GDI BitBlt, or vice versa).
        /// </summary>
        /// <param name="rect">Rectangle in global screen coordinates (physical pixels)</param>
        /// <returns>Bitmap of sampled pixels, or null on failure</returns>
        Task<SKBitmap?> SampleScreenAsync(SKRect rect);
    }
}
