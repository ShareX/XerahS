// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
using NUnit.Framework;
using SkiaSharp;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux;
using XerahS.Platform.Linux.Services.QuickSetup;

namespace XerahS.Tests.Platform.Linux;

public class LinuxOcrServiceTests
{
    private static readonly OcrLanguage[] Installed =
    [
        new("English", "eng"),
        new("Japanese", "jpn"),
        new("Chinese (chi_sim)", "chi_sim"),
    ];

    [Test]
    public void ParseLanguages_SkipsHeaderAndOrientationData()
    {
        const string output = "List of available languages in \"/usr/share/tessdata/\" (3):\neng\nosd\nchi_sim\n";

        OcrLanguage[] languages = LinuxOcrService.ParseLanguages(output);

        Assert.That(languages.Select(l => l.LanguageTag), Is.EquivalentTo(new[] { "eng", "chi_sim" }));
    }

    [TestCase("eng", "eng")]
    [TestCase("en", "eng")]
    [TestCase("en-US", "eng")]
    [TestCase("ja", "jpn")]
    [TestCase("zh-Hans", "chi_sim")]
    [TestCase("de", null)]
    [TestCase("", null)]
    public void MatchLanguage_MapsBcp47TagsToInstalledTesseractCodes(string tag, string? expected)
    {
        Assert.That(LinuxOcrService.MatchLanguage(tag, Installed), Is.EqualTo(expected));
    }

    [TestCase(false, "Hello\nWorld")]
    [TestCase(true, "Hello World")]
    public void FormatText_DropsBlankLinesAndFormFeed(bool singleLine, string expected)
    {
        string text = LinuxOcrService.FormatText("Hello  \n\nWorld\n\f", singleLine);

        Assert.That(text, Is.EqualTo(expected.Replace("\n", Environment.NewLine)));
    }

    [TestCase("Arch", "sudo pacman -S tesseract tesseract-data-eng")]
    [TestCase("Debian", "sudo apt install tesseract-ocr")]
    [TestCase("Fedora", "sudo dnf install tesseract tesseract-langpack-eng")]
    public void InstallHint_NamesTheTesseractPackages(string family, string expected)
    {
        string hint = LinuxDistroGuidance.InstallHint(Enum.Parse<LinuxDistroFamily>(family), QuickSetupPackage.Tesseract);

        Assert.That(hint, Is.EqualTo(expected));
    }

    [Test]
    public void ParseGeometryMapsScaledWordsToSourcePixels()
    {
        const string tsv = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n" +
            "5\t1\t1\t1\t1\t1\t20\t40\t100\t30\t95.5\tsynthetic@example.test\n";
        var result = LinuxOcrService.ParseGeometry(tsv, 200, 100, 400, 200, false);
        Assert.That(result.GeometryComplete, Is.True);
        Assert.That(result.Words[0].X, Is.EqualTo(10));
        Assert.That(result.Words[0].Y, Is.EqualTo(20));
        Assert.That(result.Words[0].Width, Is.EqualTo(50));
        Assert.That(result.Words[0].Confidence, Is.EqualTo(.955).Within(.0001));
        var findings = XerahS.Core.Services.Privacy.PrivacyScanService.DetectText(result, new());
        Assert.That(findings.Single().X, Is.EqualTo(.05));
        Assert.That(findings.Single().Category, Is.EqualTo(XerahS.Core.Services.Privacy.PrivacyCategory.Email));
    }

    [Test]
    public async Task RecognizeAsync_ReadsRenderedTextWithSystemTesseract()
    {
        var service = new LinuxOcrService();
        Assume.That(OperatingSystem.IsLinux() && service.IsSupported, "tesseract is not installed");
        Assume.That(service.GetAvailableLanguages().Any(l => l.LanguageTag == "eng"), "English Tesseract data is not installed");

        using var bitmap = new SKBitmap(640, 120);
        using (var canvas = new SKCanvas(bitmap))
        using (var font = new SKFont(SKTypeface.Default, 48))
        using (var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true })
        {
            canvas.Clear(SKColors.White);
            canvas.DrawText("Hello XerahS 2026", 20, 80, SKTextAlign.Left, font, paint);
        }

        OcrResult result = await service.RecognizeAsync(bitmap, new OcrOptions { Language = "en", IncludeGeometry = true });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.Text, Does.Contain("Hello XerahS 2026"));
            Assert.That(result.GeometryComplete, Is.True);
            Assert.That(result.Words, Is.Not.Empty);
            Assert.That(result.Words.All(word => word.X >= 0 && word.X + word.Width <= bitmap.Width), Is.True);
        });
    }
}
