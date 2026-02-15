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

namespace XerahS.Platform.Linux.Capture.Contracts;

internal sealed class LinuxCaptureResult
{
    private LinuxCaptureResult(string providerId, SKBitmap? bitmap, bool isCancelled)
    {
        ProviderId = providerId;
        Bitmap = bitmap;
        IsCancelled = isCancelled;
    }

    public string ProviderId { get; }

    public SKBitmap? Bitmap { get; }

    public bool IsCancelled { get; }

    public static LinuxCaptureResult Success(string providerId, SKBitmap bitmap)
    {
        return new LinuxCaptureResult(providerId, bitmap, isCancelled: false);
    }

    public static LinuxCaptureResult Cancelled(string providerId)
    {
        return new LinuxCaptureResult(providerId, null, isCancelled: true);
    }

    public static LinuxCaptureResult Failure(string providerId)
    {
        return new LinuxCaptureResult(providerId, null, isCancelled: false);
    }
}

