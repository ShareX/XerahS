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

using NUnit.Framework;
using SkiaSharp;
using XerahS.Core;
using XerahS.OCR;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.OCR;

[TestFixture]
[NonParallelizable]
public sealed class PaddleOcrServiceTests
{
    [SetUp]
    public void SetUp()
    {
        PlatformServices.Reset();
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions = new OCROptions();
    }

    [TearDown]
    public void TearDown()
    {
        PlatformServices.Reset();
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions = new OCROptions();
    }

    [Test]
    public async Task ReadsRenderedTextAndReportsWhereItIs()
    {
        // ONNX Runtime has no Intel Mac build; OCR stays with the platform's service there.
        if (!PaddleOcrEngine.IsAvailable) Assert.Ignore("PaddleOCR is not available on this platform.");

        using SKBitmap image = RenderText("XERAHS TEST", "12345");
        var service = new PaddleOcrService(null);

        OcrResult result = await service.RecognizeAsync(image, new OcrOptions { IncludeGeometry = true });

        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(result.Text.Split(Environment.NewLine), Is.EqualTo(new[] { "XERAHS TEST", "12345" }));
        Assert.That(result.GeometryComplete, Is.True);
        Assert.That((result.SourceWidth, result.SourceHeight), Is.EqualTo((image.Width, image.Height)));
        Assert.That(result.Words.Select(word => word.LineId).Distinct().Count(), Is.EqualTo(2));
        Assert.That(result.Words, Has.All.Matches<OcrWordRegion>(word =>
            word is not null && word.X >= 0 && word.Y >= 0 && word.X + word.Width <= image.Width && word.Y + word.Height <= image.Height));

        OcrResult singleLine = await service.RecognizeAsync(image, new OcrOptions { SingleLine = true });
        Assert.That(singleLine.Text, Is.EqualTo("XERAHS TEST 12345"));
    }

    [Test]
    public void DictionaryCoversLatinAndCjkButNotCyrillicOrKorean()
    {
        if (!PaddleOcrEngine.IsAvailable) Assert.Ignore("PaddleOCR is not available on this platform.");

        Assert.That(PaddleOcrEngine.CanRead("Deutsch français"), Is.True);
        Assert.That(PaddleOcrEngine.CanRead("中文 日本語"), Is.True);
        Assert.That(PaddleOcrEngine.CanRead("русский"), Is.False);
        Assert.That(PaddleOcrEngine.CanRead("한국어"), Is.False);
    }

    [Test]
    public void OffersAutomaticPaddleAndTheSystemEngineWhenBothWork()
    {
        var service = Create(new FakeSystemOcr("eng", "rus"), out _);

        Assert.That(service.Engines.Select(engine => engine.Id),
            Is.EqualTo(new[] { OcrEngineIds.Automatic, OcrEngineIds.PaddleOcr, OcrEngineIds.System }));
        Assert.That(service.Engines[2].DisplayName, Is.EqualTo("Fake OCR"));
        Assert.That(service.GetAvailableLanguages(OcrEngineIds.PaddleOcr).Select(l => l.LanguageTag), Is.EqualTo(new[] { "auto" }));
        Assert.That(service.GetAvailableLanguages(OcrEngineIds.System).Select(l => l.LanguageTag), Is.EqualTo(new[] { "eng", "rus" }));
        Assert.That(service.GetAvailableLanguages().Select(l => l.LanguageTag), Is.EqualTo(new[] { "auto", "eng", "rus" }));
    }

    [Test]
    public void OffersOnlyWhatWorks()
    {
        Assert.That(Create(new FakeSystemOcr { Supported = false }, out _).Engines.Select(e => e.Id), Is.EqualTo(new[] { OcrEngineIds.PaddleOcr }));
        Assert.That(Create(null, out _).IsSupported, Is.True);

        var systemOnly = Create(new FakeSystemOcr("eng"), out _, paddleAvailable: false);
        Assert.That(systemOnly.Engines.Select(e => e.Id), Is.EqualTo(new[] { OcrEngineIds.System }));
        Assert.That(systemOnly.GetAvailableLanguages().Select(l => l.LanguageTag), Is.EqualTo(new[] { "eng" }));

        var none = Create(new FakeSystemOcr { Supported = false, Reason = "Install Fake OCR." }, out _, paddleAvailable: false);
        Assert.That(none.IsSupported, Is.False);
        Assert.That(none.UnavailableReason, Is.EqualTo("Install Fake OCR."));
        Assert.That(none.Engines, Is.Empty);
    }

    [TestCase("en", null, false)]
    [TestCase("eng", "auto", false)]
    [TestCase("auto", "auto", false)]
    [TestCase("ru", "auto", true)]
    [TestCase("rus", null, true)]
    [TestCase("ko", "auto", false, Description = "The system engine has no Korean, so PaddleOCR still tries.")]
    [TestCase("ru", "paddleocr", false)]
    [TestCase("en", "system", true)]
    [TestCase("en", "SYSTEM", true)]
    public async Task RoutesByEngineAndLanguage(string language, string? engine, bool expectSystem)
    {
        var system = new FakeSystemOcr("eng", "rus");
        var service = Create(system, out Func<int> paddleCalls);
        using var image = new SKBitmap(8, 8);

        OcrResult result = await service.RecognizeAsync(image, new OcrOptions { Language = language, Engine = engine });

        Assert.That(result.Success, Is.True);
        Assert.That(system.Calls, Is.EqualTo(expectSystem ? 1 : 0));
        Assert.That(paddleCalls(), Is.EqualTo(expectSystem ? 0 : 1));
    }

    [Test]
    public async Task APaddleFailureIsReportedNotThrown()
    {
        var service = new PaddleOcrService(null, () => true, _ => throw new InvalidOperationException("model broke"), _ => true);
        using var image = new SKBitmap(8, 8);

        OcrResult result = await service.RecognizeAsync(image, new OcrOptions());

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("model broke"));
    }

    [Test]
    public void RegisterWrapsThePlatformServiceOnce()
    {
        var system = new FakeSystemOcr("eng");
        PlatformServices.Ocr = system;

        PaddleOcrService.Register();
        IOcrService? first = PlatformServices.Ocr;
        PaddleOcrService.Register();

        Assert.That(first, Is.InstanceOf<PaddleOcrService>());
        Assert.That(PlatformServices.Ocr, Is.SameAs(first));
    }

    [Test]
    public void ChoosingAnEngineInTheOcrWindowSwapsTheLanguagesAndIsRemembered()
    {
        PlatformServices.Ocr = Create(new FakeSystemOcr("eng", "rus"), out _);
        using var image = new SKBitmap(8, 8);

        var viewModel = new OcrViewModel(image);

        Assert.That(viewModel.HasEngineChoice, Is.True);
        Assert.That(viewModel.SelectedEngine?.Id, Is.EqualTo(OcrEngineIds.Automatic));
        Assert.That(viewModel.AvailableLanguages.Select(l => l.LanguageTag), Is.EqualTo(new[] { "auto", "eng", "rus" }));

        viewModel.SelectedEngine = viewModel.AvailableEngines.Single(engine => engine.Id == OcrEngineIds.System);

        Assert.That(viewModel.AvailableLanguages.Select(l => l.LanguageTag), Is.EqualTo(new[] { "eng", "rus" }));
        Assert.That(viewModel.SelectedLanguage?.LanguageTag, Is.EqualTo("eng"));
        Assert.That(SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.Engine, Is.EqualTo(OcrEngineIds.System));

        viewModel.SelectedEngine = viewModel.AvailableEngines.Single(engine => engine.Id == OcrEngineIds.PaddleOcr);

        Assert.That(viewModel.AvailableLanguages.Select(l => l.LanguageTag), Is.EqualTo(new[] { "auto" }));
        Assert.That(viewModel.SelectedLanguage?.LanguageTag, Is.EqualTo("auto"));
    }

    [Test]
    public void TheOcrWindowOpensOnTheRememberedEngine()
    {
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions.Engine = OcrEngineIds.System;
        PlatformServices.Ocr = Create(new FakeSystemOcr("eng", "rus"), out _);
        using var image = new SKBitmap(8, 8);

        var viewModel = new OcrViewModel(image);

        Assert.That(viewModel.SelectedEngine?.Id, Is.EqualTo(OcrEngineIds.System));
        Assert.That(viewModel.AvailableLanguages.Select(l => l.LanguageTag), Is.EqualTo(new[] { "eng", "rus" }));
    }

    private static PaddleOcrService Create(IOcrService? system, out Func<int> paddleCalls, bool paddleAvailable = true)
    {
        int calls = 0;
        paddleCalls = () => calls;
        return new PaddleOcrService(system, () => paddleAvailable, _ =>
        {
            calls++;
            return new PaddleOcrResult([new PaddleOcrTextRegion([new(0, 0), new(4, 0), new(4, 4), new(0, 4)], "text", 0.9f)]);
        }, text => text.All(c => c < 0x250));
    }

    private static SKBitmap RenderText(params string[] lines)
    {
        var image = new SKBitmap(640, 40 + (90 * lines.Length));
        using var canvas = new SKCanvas(image);
        using var font = new SKFont(SKTypeface.Default, 48);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.Clear(SKColors.White);

        for (int i = 0; i < lines.Length; i++)
        {
            canvas.DrawText(lines[i], 30, 80 + (90 * i), SKTextAlign.Left, font, paint);
        }

        return image;
    }

    private sealed class FakeSystemOcr(params string[] languages) : IOcrService
    {
        public bool Supported { get; init; } = true;
        public string? Reason { get; init; }
        public int Calls { get; private set; }

        public bool IsSupported => Supported;
        public string? UnavailableReason => Supported ? null : Reason;
        public string EngineName => "Fake OCR";

        public OcrLanguage[] GetAvailableLanguages() => [.. languages.Select(tag => new OcrLanguage(tag, tag))];

        public Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options)
        {
            Calls++;
            return Task.FromResult(new OcrResult { Success = true, Text = "system" });
        }
    }
}
