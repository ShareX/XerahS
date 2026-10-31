using NUnit.Framework;
using SkiaSharp;
using XerahS.Core;
using XerahS.Core.Services.Privacy;
using XerahS.Platform.Abstractions;
namespace XerahS.Tests.Privacy;
public class LocalPrivacyEvidenceTests
{
    [Test]
    public async Task CustomRuleAddsLocalRegionWithoutChangingFailedAiStatus()
    {
        using var image = new SKBitmap(100, 50);
        var result = await PrivacyScanService.AddLocalEvidenceAsync(image,
            new TaskSettingsPrivacy { CustomPatterns = ["PROJECT-ALPHA"] }, new(PrivacyScanStatus.Unsupported, []), new FakeOcr(), CancellationToken.None);
        Assert.That(result.Status, Is.EqualTo(PrivacyScanStatus.Unsupported));
        Assert.That(result.Findings.Single().Category, Is.EqualTo(PrivacyCategory.Custom));
        Assert.That(result.Findings.Single().X, Is.EqualTo(.1));
    }
    [Test]
    public async Task InvalidCustomRuleRequiresManualReviewEvenWithoutOcr()
    {
        using var image = new SKBitmap(20, 20);
        var result = await PrivacyScanService.AddLocalEvidenceAsync(image,
            new TaskSettingsPrivacy { CustomPatterns = ["("] }, new(PrivacyScanStatus.Complete, []), null, CancellationToken.None);
        Assert.That(result.Status, Is.EqualTo(PrivacyScanStatus.Incomplete));
    }
    private sealed class FakeOcr : IOcrService
    {
        public bool IsSupported => true;
        public OcrLanguage[] GetAvailableLanguages() => [];
        public Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options) => Task.FromResult(new OcrResult
        { Success = true, GeometryComplete = true, SourceWidth = image.Width, SourceHeight = image.Height,
            Words = [new("PROJECT-ALPHA", "1", 10, 10, 50, 10)] });
    }
}
