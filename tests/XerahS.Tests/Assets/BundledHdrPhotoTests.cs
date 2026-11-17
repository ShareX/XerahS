using System.Buffers.Binary;
using Avalonia.Headless.NUnit;
using Avalonia.Platform;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Common.Helpers;

namespace XerahS.Tests.Assets;

/// <summary>
/// The HDR settings block previews a bundled .hdr asset. If the asset stops shipping or loses its
/// highlights, the five previews collapse into five identical pictures and nobody notices.
/// </summary>
[TestFixture]
public class BundledHdrPhotoTests
{
    private const string AssetUri = "avares://XerahS.UI/Assets/hdr/the_sky_is_on_fire_1k.hdr";

    [AvaloniaTest]
    public void BundledHdrPhotoShipsAndCarriesHeadroomAboveSdrWhite()
    {
        using Stream stream = AssetLoader.Open(new Uri(AssetUri));
        using SKBitmap? photo = RadianceHdrDecoder.TryDecode(stream);

        Assert.That(photo, Is.Not.Null);
        Assert.That(photo!.Width, Is.GreaterThan(0));
        Assert.That(photo.Height, Is.GreaterThan(0));

        float peak = 0f;
        int pixelsAboveWhite = 0;
        Span<byte> pixels = photo.GetPixelSpan();

        for (int row = 0; row < photo.Height; row++)
        {
            int rowOffset = row * photo.RowBytes;
            for (int column = 0; column < photo.Width; column++)
            {
                int offset = rowOffset + column * 8;
                float value = (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(pixels.Slice(offset, 2)));

                if (value > 1f)
                {
                    pixelsAboveWhite++;
                }

                if (value > peak)
                {
                    peak = value;
                }
            }
        }

        Assert.That(peak, Is.GreaterThan(1.5f), "The preview photo needs real highlights, or every tone curve renders the same.");
        Assert.That(pixelsAboveWhite, Is.GreaterThan(1000), "Too few pixels above SDR white to show a highlight roll-off.");
    }
}
