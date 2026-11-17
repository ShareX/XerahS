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

using System.Diagnostics;
using Avalonia.Media.Imaging;
using SkiaSharp;
using XerahS.Common;
using XerahS.Common.Helpers;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.Core.Helpers;

namespace XerahS.UI.Services;

/// <summary>
/// Decodes the bundled CC0 HDR photo once and renders it through every HDR to SDR curve, so the
/// comparison window and any other surface show the same pixels. A real photograph reveals how a
/// curve treats highlights, which the synthetic luminance ramp cannot.
/// </summary>
internal static class HdrPhotoPreviewCache
{
    private const string PhotoAssetUri = "avares://XerahS.UI/Assets/hdr/the_sky_is_on_fire_1k.hdr";
    private const int PreviewWidth = 1024;

    public static IReadOnlyList<HdrToneMappingMode> Modes { get; } = new[]
    {
        HdrToneMappingMode.None,
        HdrToneMappingMode.TonemapReinhard,
        HdrToneMappingMode.TonemapAces,
        HdrToneMappingMode.TonemapBt2446C,
        HdrToneMappingMode.TonemapHable,
    };

    private static readonly Lazy<IReadOnlyDictionary<HdrToneMappingMode, Bitmap?>> Previews =
        new(BuildPreviews, LazyThreadSafetyMode.ExecutionAndPublication);

    public static Bitmap? Get(HdrToneMappingMode mode)
    {
        return Previews.Value.TryGetValue(mode, out Bitmap? preview) ? preview : null;
    }

    public static string GetLabel(HdrToneMappingMode mode) => Resources.GetString($"HdrToneMappingMode_{mode}");

    /// <summary>
    /// Shows the original fp16 photo with its real highlights: a native HDR window when the platform
    /// has one, otherwise an Ultra HDR JPEG handed to the system viewer.
    /// </summary>
    public static bool OpenInHdrViewer(string windowTitle)
    {
        try
        {
            using SKBitmap? photo = LoadSource();
            if (photo == null)
            {
                return false;
            }

            if (XerahS.Platform.Abstractions.PlatformServices.HdrPreview is { IsSupported: true } hdrPreview &&
                hdrPreview.TryShow(photo.GetPixelSpan(), photo.Width, photo.Height, photo.RowBytes, windowTitle))
            {
                return true;
            }

            string path = Path.Combine(Path.GetTempPath(), "xerahs-hdr-preview.jpg");
            if (!UltraHdrJpegWriter.TrySave(photo, path))
            {
                DebugHelper.WriteLine("[HDR] Ultra HDR preview could not be written.");
                return false;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "[HDR] opening the HDR preview externally failed");
            return false;
        }
    }

    /// <summary>Original fp16 pixels, for an embedded or external HDR surface.</summary>
    public static bool TryGetSourcePixels(out byte[] pixels, out int width, out int height, out int rowBytes)
    {
        pixels = Array.Empty<byte>();
        width = 0;
        height = 0;
        rowBytes = 0;

        using SKBitmap? photo = LoadSource();
        if (photo == null)
        {
            return false;
        }

        width = photo.Width;
        height = photo.Height;
        rowBytes = photo.RowBytes;
        pixels = photo.GetPixelSpan().ToArray();
        return true;
    }

    private static SKBitmap? LoadSource()
    {
        try
        {
            using Stream stream = Avalonia.Platform.AssetLoader.Open(new Uri(PhotoAssetUri));
            return RadianceHdrDecoder.TryDecode(stream);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"[HDR] bundled preview photo failed to load: {ex.Message}");
            return null;
        }
    }

    private static IReadOnlyDictionary<HdrToneMappingMode, Bitmap?> BuildPreviews()
    {
        var previews = new Dictionary<HdrToneMappingMode, Bitmap?>();

        using SKBitmap? photo = LoadSource();
        if (photo == null)
        {
            return previews;
        }

        // The asset is a 2048x1024 panorama; 1024 px is sharp at the comparison window's size and
        // keeps the five renders cheap enough to build on demand.
        int targetHeight = Math.Max(1, photo.Height * PreviewWidth / photo.Width);
        using SKBitmap thumbnail = ImageHelpers.ResizeImage(photo, PreviewWidth, targetHeight);

        foreach (HdrToneMappingMode mode in Modes)
        {
            using SKBitmap? sdr = HdrTonemapper.TonemapToBgra8888(thumbnail, mode);
            previews[mode] = sdr != null ? ShareX.ImageEditor.Presentation.Rendering.BitmapConversionHelpers.ToAvaloniBitmap(sdr) : null;
        }

        return previews;
    }
}
