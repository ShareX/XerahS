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
using System.Buffers.Binary;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using ShareX.ImageEditor.Presentation.Rendering;
using XerahS.Common;
using XerahS.Common.Helpers;
using XerahS.Core;
using XerahS.Core.Helpers;
using XerahS.Services.Abstractions;

namespace XerahS.UI.ViewModels
{
    public partial class TaskSettingsViewModel
    {
        #region Image Settings

        public EImageFormat ImageFormat
        {
            get => _settings.ImageSettings.ImageFormat;
            set
            {
                if (_settings.ImageSettings.ImageFormat != value)
                {
                    _settings.ImageSettings.ImageFormat = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ImageJPEGQuality
        {
            get => _settings.ImageSettings.ImageJPEGQuality;
            set
            {
                if (_settings.ImageSettings.ImageJPEGQuality != value)
                {
                    _settings.ImageSettings.ImageJPEGQuality = value;
                    OnPropertyChanged();
                }
            }
        }

        public IReadOnlyList<PNGBitDepth> PngBitDepthOptions { get; } =
            new[] { PNGBitDepth.Default, PNGBitDepth.Bit16 };

        public PNGBitDepth ImagePNGBitDepth
        {
            get => _settings.ImageSettings.ImagePNGBitDepth;
            set
            {
                if (_settings.ImageSettings.ImagePNGBitDepth != value)
                {
                    _settings.ImageSettings.ImagePNGBitDepth = value;
                    OnPropertyChanged();
                    // The saved file ignores the tone curve at 16-bit depth (it is written
                    // display-referred), so the "Save to file" selector must follow the depth.
                    OnPropertyChanged(nameof(IsHdrSaveToneMappingEnabled));
                }
            }
        }

        /// <summary>
        /// False when the selected PNG depth ("SDR+HDR" = 16-bit) makes the file tone curve
        /// irrelevant: the encoder writes display-referred sRGB samples without applying it.
        /// </summary>
        public bool IsHdrSaveToneMappingEnabled => ImagePNGBitDepth != PNGBitDepth.Bit16;

        public int ThumbnailWidth
        {
            get => _settings.ImageSettings.ThumbnailWidth;
            set
            {
                if (_settings.ImageSettings.ThumbnailWidth != value)
                {
                    _settings.ImageSettings.ThumbnailWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ThumbnailHeight
        {
            get => _settings.ImageSettings.ThumbnailHeight;
            set
            {
                if (_settings.ImageSettings.ThumbnailHeight != value)
                {
                    _settings.ImageSettings.ThumbnailHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ThumbnailName
        {
            get => _settings.ImageSettings.ThumbnailName;
            set
            {
                if (_settings.ImageSettings.ThumbnailName != value)
                {
                    _settings.ImageSettings.ThumbnailName = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ThumbnailCheckSize
        {
            get => _settings.ImageSettings.ThumbnailCheckSize;
            set
            {
                if (_settings.ImageSettings.ThumbnailCheckSize != value)
                {
                    _settings.ImageSettings.ThumbnailCheckSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public IReadOnlyList<HdrToneMappingMode> HdrToneMappingOptions =>
            Array.FindAll(Enum.GetValues<HdrToneMappingMode>(), m => m != HdrToneMappingMode.Auto);

        private Bitmap? _hdrPreviewBitmap;

        private const string HdrPhotoAssetUri = "avares://XerahS.UI/Assets/hdr/the_sky_is_on_fire_1k.hdr";
        private const int HdrPhotoPreviewWidth = 640;

        /// <summary>
        /// The bundled CC0 photograph (Polyhaven "the sky is on fire") rendered through each tone
        /// curve of the capture pipeline. A real photograph shows the highlight roll-off far better
        /// than the synthetic bar chart, which only shades luminance.
        /// </summary>
        public Bitmap? HdrPhotoNone => GetHdrPhotoPreview(HdrToneMappingMode.None);
        public Bitmap? HdrPhotoReinhard => GetHdrPhotoPreview(HdrToneMappingMode.TonemapReinhard);
        public Bitmap? HdrPhotoAces => GetHdrPhotoPreview(HdrToneMappingMode.TonemapAces);
        public Bitmap? HdrPhotoBt2446C => GetHdrPhotoPreview(HdrToneMappingMode.TonemapBt2446C);
        public Bitmap? HdrPhotoHable => GetHdrPhotoPreview(HdrToneMappingMode.TonemapHable);

        // Decoding and tonemapping the photo once for the whole process keeps every settings page
        // cheap; the resulting Avalonia bitmaps are immutable and safe to share.
        private static readonly Lazy<IReadOnlyDictionary<HdrToneMappingMode, Bitmap?>> HdrPhotoPreviews =
            new(BuildHdrPhotoPreviews, LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly HdrToneMappingMode[] PhotoPreviewModes =
        {
            HdrToneMappingMode.None,
            HdrToneMappingMode.TonemapReinhard,
            HdrToneMappingMode.TonemapAces,
            HdrToneMappingMode.TonemapBt2446C,
            HdrToneMappingMode.TonemapHable,
        };

        private static Bitmap? GetHdrPhotoPreview(HdrToneMappingMode mode)
        {
            return HdrPhotoPreviews.Value.TryGetValue(mode, out Bitmap? preview) ? preview : null;
        }

        private IRelayCommand? _openHdrPhotoCommand;

        /// <summary>
        /// Writes the bundled photo as an Ultra HDR JPEG and hands it to the system viewer. The app
        /// window is an SDR surface, so an external HDR-capable viewer is the only way to see the
        /// image's real highlights.
        /// </summary>
        public IRelayCommand OpenHdrPhotoCommand => _openHdrPhotoCommand ??= new RelayCommand(OpenBundledHdrPhotoExternally);

        private void OpenBundledHdrPhotoExternally()
        {
            try
            {
                using SKBitmap? photo = LoadBundledHdrPhoto();
                if (photo == null)
                {
                    return;
                }

                string path = Path.Combine(Path.GetTempPath(), "xerahs-hdr-preview.jpg");
                if (!UltraHdrJpegWriter.TrySave(photo, path))
                {
                    DebugHelper.WriteLine("[HDR] Ultra HDR preview could not be written.");
                    return;
                }

                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "[HDR] opening the HDR preview externally failed");
            }
        }

        private static SKBitmap? LoadBundledHdrPhoto()
        {
            try
            {
                using Stream stream = Avalonia.Platform.AssetLoader.Open(new Uri(HdrPhotoAssetUri));
                return RadianceHdrDecoder.TryDecode(stream);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"[HDR] bundled preview photo failed to load: {ex.Message}");
                return null;
            }
        }

        private static IReadOnlyDictionary<HdrToneMappingMode, Bitmap?> BuildHdrPhotoPreviews()
        {
            var previews = new Dictionary<HdrToneMappingMode, Bitmap?>();

            using SKBitmap? photo = LoadBundledHdrPhoto();
            if (photo == null)
            {
                return previews;
            }

            // Downscale before tonemapping: the asset is a 2048x1024 panorama and the panel only
            // shows a thumbnail, so 300k pixels per variant beat 2M for no visible gain.
            int targetHeight = Math.Max(1, photo.Height * HdrPhotoPreviewWidth / photo.Width);
            using SKBitmap thumbnail = ImageHelpers.ResizeImage(photo, HdrPhotoPreviewWidth, targetHeight);

            foreach (var mode in PhotoPreviewModes)
            {
                using var sdr = HdrTonemapper.TonemapToBgra8888(thumbnail, mode);
                previews[mode] = sdr != null ? BitmapConversionHelpers.ToAvaloniBitmap(sdr) : null;
            }

            return previews;
        }

        /// <summary>
        /// Live preview of the selected general HDR → SDR tone mapping, rendered from a synthetic
        /// HDR test image so the difference between methods is visible without taking a capture.
        /// </summary>
        public Bitmap? HdrPreviewBitmap => _hdrPreviewBitmap ??= BuildHdrPreview();

        public HdrToneMappingMode ImageHdrToneMapping
        {
            get => _settings.ImageSettings.ImageHdrToneMapping;
            set
            {
                if (_settings.ImageSettings.ImageHdrToneMapping != value)
                {
                    _settings.ImageSettings.ImageHdrToneMapping = value;
                    OnPropertyChanged();
                    RefreshHdrPreview();
                }
            }
        }

        public HdrToneMappingMode ImageHdrSaveToneMapping
        {
            get => _settings.ImageSettings.ImageHdrSaveToneMapping;
            set
            {
                if (_settings.ImageSettings.ImageHdrSaveToneMapping != value)
                {
                    _settings.ImageSettings.ImageHdrSaveToneMapping = value;
                    OnPropertyChanged();
                }
            }
        }

        public HdrToneMappingMode ImageHdrClipboardToneMapping
        {
            get => _settings.ImageSettings.ImageHdrClipboardToneMapping;
            set
            {
                if (_settings.ImageSettings.ImageHdrClipboardToneMapping != value)
                {
                    _settings.ImageSettings.ImageHdrClipboardToneMapping = value;
                    OnPropertyChanged();
                }
            }
        }

        private Bitmap? BuildHdrPreview()
        {
            using var source = CreateHdrPreviewSource();
            using var sdr = HdrTonemapper.TonemapToBgra8888(source, ImageHdrToneMapping);
            return sdr != null ? BitmapConversionHelpers.ToAvaloniBitmap(sdr) : null;
        }

        private void RefreshHdrPreview()
        {
            var previous = _hdrPreviewBitmap;
            _hdrPreviewBitmap = BuildHdrPreview();
            previous?.Dispose();
            OnPropertyChanged(nameof(HdrPreviewBitmap));
        }

        private static SKBitmap CreateHdrPreviewSource()
        {
            const int width = 440;
            const int height = 96;

            // scRGB-linear luminance stops (1.0 == 80 cd/m²). Left bars are SDR content, right
            // bars are HDR highlights, making each tonemapper's rolloff immediately visible.
            float[] stops = { 0f, 0.1f, 0.25f, 0.5f, 0.8f, 1f, 2f, 4f, 8f, 16f, 32f };

            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.RgbaF16, SKAlphaType.Premul, SKColorSpace.CreateSrgbLinear()));
            Span<byte> span = bitmap.GetPixelSpan();
            int rowBytes = bitmap.RowBytes;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = Math.Min((int)((float)x / width * stops.Length), stops.Length - 1);
                    float v = stops[index];
                    int offset = y * rowBytes + x * 8;

                    WriteHalf(span, offset, v);
                    WriteHalf(span, offset + 2, v);
                    WriteHalf(span, offset + 4, v);
                    WriteHalf(span, offset + 6, 1f);
                }
            }

            return bitmap;
        }

        private static void WriteHalf(Span<byte> span, int offset, float value)
        {
            ushort bits = BitConverter.HalfToUInt16Bits((Half)value);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset, 2), bits);
        }

        #endregion
    }
}
