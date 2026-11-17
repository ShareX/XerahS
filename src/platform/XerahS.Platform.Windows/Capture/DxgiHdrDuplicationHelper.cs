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
using Vortice.Direct3D11;
using Vortice.DXGI;
using XerahS.Common;

namespace XerahS.Platform.Windows.Capture;

/// <summary>
/// Shared DXGI HDR helpers used by both the legacy region-capture strategy and the main
/// full-screen capture service. Centralizes HDR10 detection and fp16 scRGB duplication with an
/// 8-bit fallback (HDR_CAPTURE_PLAN RISK-06).
/// </summary>
internal static class DxgiHdrDuplicationHelper
{
    /// <summary>
    /// Whether the given DXGI output currently reports an HDR10 color space (PQ/P2020).
    /// QueryInterface/GetDesc1 failures are treated as SDR.
    /// </summary>
    public static bool IsOutputHdr(IDXGIOutput output)
    {
        try
        {
            using var output6 = output.QueryInterface<IDXGIOutput6>();
            return DxgiCapabilitiesHelper.IsHdrColorSpace(output6.Description1.ColorSpace);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates an output duplication, requesting fp16 scRGB frames when the output is HDR.
    /// Falls back to the ordinary 8-bit duplication when the driver rejects or ignores the fp16
    /// format hint, setting <paramref name="isHdr"/> to reflect the final mode.
    /// </summary>
    public static IDXGIOutputDuplication CreateDuplication(IDXGIOutput1 output, ID3D11Device device, out bool isHdr)
    {
        isHdr = IsOutputHdr(output);
        DebugHelper.WriteLine($"HDR: output detected as {(isHdr ? "HDR (requesting fp16)" : "SDR")}");

        if (isHdr)
        {
            IDXGIOutputDuplication? hdrDup = null;
            try
            {
                using var output5 = output.QueryInterface<IDXGIOutput5>();
                hdrDup = output5.DuplicateOutput1(device, new[] { Format.R16G16B16A16_Float });
                DebugHelper.WriteLine($"HDR: DuplicateOutput1 format={hdrDup.Description.ModeDescription.Format}");
                if (hdrDup.Description.ModeDescription.Format == Format.R16G16B16A16_Float)
                {
                    return hdrDup;
                }

                hdrDup.Dispose();
                hdrDup = null;
                DebugHelper.WriteLine("HDR: driver ignored the fp16 format hint; falling back to 8-bit duplication.");
            }
            catch (Exception ex)
            {
                hdrDup?.Dispose();
                DebugHelper.WriteLine($"HDR: DuplicateOutput1 failed ({ex.Message}); falling back to 8-bit duplication.");
            }

            isHdr = false;
        }

        return output.DuplicateOutput(device);
    }

    /// <summary>
    /// Creates an 8-bit or fp16 readback bitmap for a mapped DXGI staging texture.
    /// </summary>
    public static SKBitmap CreateReadbackBitmap(int width, int height, bool isHdr)
    {
        return isHdr
            ? new SKBitmap(new SKImageInfo(width, height, SKColorType.RgbaF16, SKAlphaType.Premul, SKColorSpace.CreateSrgbLinear()))
            : new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb()));
    }

    /// <summary>
    /// Bytes per pixel for the readback bitmap matching <see cref="CreateReadbackBitmap"/>.
    /// </summary>
    public static int BytesPerPixel(bool isHdr) => isHdr ? 8 : 4;
}
