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

using ShareX.ImageEditor.Core.ImageEffects.Filters;
using SkiaSharp;

namespace XerahS.Core.Services.Privacy;

/// <summary>Headless region effects shared by upload privacy and MCP annotations.</summary>
public static class RegionRedactionRenderer
{
    public static byte[] EncodePng(SKBitmap bitmap)
    {
        using var colorSpace = SKColorSpace.CreateSrgb();
        using var normalized = new SKBitmap(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul, colorSpace));
        using (var canvas = new SKCanvas(normalized))
        using (var paint = new SKPaint { BlendMode = SKBlendMode.Src })
            canvas.DrawBitmap(bitmap, 0, 0, SKSamplingOptions.Default, paint);
        using var image = SKImage.FromBitmap(normalized);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100) ?? throw new InvalidOperationException("Could not encode privacy image.");
        return data.ToArray();
    }

    public static SKBitmap Render(SKBitmap source, IReadOnlyList<PrivacyFinding> regions, TaskSettingsPrivacy policy)
    {
        if (source.Width <= 0 || source.Height <= 0 || regions.Count > 512)
            throw new ArgumentException("Invalid redaction image or region count.");
        var output = source.Copy() ?? throw new InvalidOperationException("Could not copy image.");
        try
        {
            foreach (var region in regions)
            {
                Validate(region);
                var bounds = new SKRectI(
                    Math.Max(0, (int)Math.Floor(region.X * source.Width) - Math.Clamp(policy.MarginPixels, 0, 64)),
                    Math.Max(0, (int)Math.Floor(region.Y * source.Height) - Math.Clamp(policy.MarginPixels, 0, 64)),
                    Math.Min(source.Width, (int)Math.Ceiling((region.X + region.Width) * source.Width) + Math.Clamp(policy.MarginPixels, 0, 64)),
                    Math.Min(source.Height, (int)Math.Ceiling((region.Y + region.Height) * source.Height) + Math.Clamp(policy.MarginPixels, 0, 64)));
                Apply(output, bounds, policy.Effect, Math.Clamp(policy.BlurRadius, 1, 100));
            }
            return output;
        }
        catch { output.Dispose(); throw; }
    }

    public static void Validate(PrivacyFinding region)
    {
        if (!Enum.IsDefined(region.Category) || !double.IsFinite(region.X) || !double.IsFinite(region.Y) ||
            !double.IsFinite(region.Width) || !double.IsFinite(region.Height) || !double.IsFinite(region.Confidence) ||
            region.X < 0 || region.Y < 0 || region.Width <= 0 || region.Height <= 0 ||
            region.X + region.Width > 1.000001 || region.Y + region.Height > 1.000001 ||
            region.Confidence < 0 || region.Confidence > 1)
            throw new ArgumentException("Invalid sensitive-region coordinates.");
    }

    public static void Apply(SKBitmap bitmap, SKRectI bounds, PrivacyRedactionEffect effect, int strength)
    {
        if (!Enum.IsDefined(effect)) throw new ArgumentException("Unknown redaction effect.");
        bounds = new SKRectI(Math.Clamp(bounds.Left, 0, bitmap.Width), Math.Clamp(bounds.Top, 0, bitmap.Height),
            Math.Clamp(bounds.Right, 0, bitmap.Width), Math.Clamp(bounds.Bottom, 0, bitmap.Height));
        if (bounds.Width <= 0 || bounds.Height <= 0) throw new ArgumentException("Empty redaction region.");
        using var canvas = new SKCanvas(bitmap);
        using var replace = new SKPaint { BlendMode = SKBlendMode.Src, IsAntialias = false };
        if (effect == PrivacyRedactionEffect.Mask)
        {
            replace.Color = SKColors.Black;
            canvas.DrawRect(bounds, replace);
            return;
        }
        using var crop = new SKBitmap(bounds.Width, bounds.Height, bitmap.ColorType, bitmap.AlphaType);
        if (!bitmap.ExtractSubset(crop, bounds)) throw new InvalidOperationException("Could not extract region.");
        if (effect == PrivacyRedactionEffect.Blur)
        {
            // Existing editor effect extends edge pixels before filtering. Src replaces alpha too.
            using var blurred = new BlurImageEffect { Radius = Math.Clamp(strength, 1, 100) }.Apply(crop);
            canvas.DrawBitmap(blurred, bounds.Left, bounds.Top, SKSamplingOptions.Default, replace);
        }
        else
        {
            int block = Math.Clamp(strength, 2, 100);
            using var reduced = new SKBitmap(Math.Max(1, bounds.Width / block), Math.Max(1, bounds.Height / block));
            if (!crop.ScalePixels(reduced, new SKSamplingOptions(SKFilterMode.Linear)))
                throw new InvalidOperationException("Could not pixelate region.");
            canvas.DrawBitmap(reduced, bounds, new SKSamplingOptions(SKFilterMode.Nearest), replace);
        }
    }
}
