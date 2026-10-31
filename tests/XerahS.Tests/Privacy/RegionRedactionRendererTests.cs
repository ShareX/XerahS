using NUnit.Framework;
using SkiaSharp;
using XerahS.Core;
using XerahS.Core.Services.Privacy;
namespace XerahS.Tests.Privacy;
public class RegionRedactionRendererTests
{
    [TestCase(PrivacyRedactionEffect.Blur)] [TestCase(PrivacyRedactionEffect.Pixelate)] [TestCase(PrivacyRedactionEffect.Mask)]
    public void EffectsReplaceTargetPixelsAndPreserveOutside(PrivacyRedactionEffect effect)
    {
        using var source = new SKBitmap(100, 100);
        source.Erase(SKColors.White);
        for (int y = 20; y < 40; y++) for (int x = 20; x < 40; x++) source.SetPixel(x, y, ((x+y)%2 == 0 ? SKColors.Black : SKColors.Red).WithAlpha(100));
        using var output = RegionRedactionRenderer.Render(source, [new(PrivacyCategory.Email, .2, .2, .2, .2, 1)],
            new TaskSettingsPrivacy { Effect = effect, MarginPixels = 0, BlurRadius = 12 });
        Assert.That(output.GetPixel(0,0), Is.EqualTo(source.GetPixel(0,0)));
        Assert.That(output.GetPixel(30,30), Is.Not.EqualTo(source.GetPixel(30,30)));
        if (effect == PrivacyRedactionEffect.Mask) Assert.That(output.GetPixel(20,20), Is.EqualTo(SKColors.Black));
    }
    [TestCase(-.1, .1)] [TestCase(.9, .2)] [TestCase(.1, 0)] [TestCase(double.NaN, .1)]
    public void InvalidBoundsFailInsteadOfReportingApplied(double x, double width)
    {
        using var source = new SKBitmap(10, 10);
        Assert.Throws<ArgumentException>(() => RegionRedactionRenderer.Render(source,
            [new(PrivacyCategory.Email, x, .1, width, .1, 1)], new()));
    }
}
