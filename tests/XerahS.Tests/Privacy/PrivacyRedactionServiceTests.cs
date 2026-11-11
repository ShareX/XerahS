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
using XerahS.Assistant.Ai;
using XerahS.Assistant.Services;
using XerahS.Core;
using XerahS.Core.Services.Privacy;
using XerahS.Platform.Abstractions;

namespace XerahS.Tests.Privacy;

[TestFixture, NonParallelizable]
public sealed class PrivacyRedactionServiceTests
{
    private const int Width = 100, Height = 50;
    private string _folder = null!;
    private string _original = null!;
    private byte[] _originalBytes = null!;
    private IAiClient _originalClient = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "xerahs-redact-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _original = Path.Combine(_folder, "synthetic.png");
        using (var image = new SKBitmap(Width, Height))
        {
            image.Erase(SKColors.White);
            File.WriteAllBytes(_original, RegionRedactionRenderer.EncodePng(image));
        }
        _originalBytes = File.ReadAllBytes(_original);
        _originalClient = AiServices.Client;
        SettingsManager.Settings.AssistantDisabledFeatures.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        AiServices.Client = _originalClient;
        SettingsManager.Settings.AssistantDisabledFeatures.Clear();
        Directory.Delete(_folder, true);
    }

    private static readonly string Regions =
        $"{{\"complete\":true,\"width\":{Width},\"height\":{Height},\"regions\":[" +
        "{\"category\":\"Email\",\"x\":0.25,\"y\":0.25,\"width\":0.5,\"height\":0.5,\"confidence\":0.9}]}";

    private static PrivacyRedactionService Create(Reviewer reviewer, IOcrService? ocr = null, TaskSettingsPrivacy? policy = null) =>
        new(reviewer, AssistantPrivacyAnalysisProvider.CreateForRedaction, ocr,
            policy ?? new TaskSettingsPrivacy { Effect = PrivacyRedactionEffect.Mask, MarginPixels = 0 });

    private FakeAiClient UseAi(bool configured = true, bool supportsImages = true)
    {
        var client = new FakeAiClient(supportsImages) { Configured = configured, Reply = Regions };
        AiServices.Client = client;
        return client;
    }

    [Test]
    public async Task UsesAiWhenUsableAndAllowedAndSavesAFlattenedCopyBesideTheOriginal()
    {
        var ai = UseAi();
        var reviewer = new Reviewer();

        PrivacyRedactionResult result = await Create(reviewer).RedactFileAsync(_original, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrivacyRedactionOutcome.Saved));
        Assert.That(result.OutputPath, Is.EqualTo(Path.Combine(_folder, "synthetic-redacted.png")));
        Assert.That(reviewer.ConsentCalls, Is.EqualTo(1));
        Assert.That(ai.Prompts, Has.Count.EqualTo(1));
        Assert.That(reviewer.LastScan!.Status, Is.EqualTo(PrivacyScanStatus.Complete));
        Assert.That(reviewer.LastScan.Findings.Single().Category, Is.EqualTo(PrivacyCategory.Email));
        using SKBitmap saved = SKBitmap.Decode(result.OutputPath)!;
        Assert.That(saved.GetPixel(Width / 2, Height / 2), Is.EqualTo(SKColors.Black), "The AI region is masked.");
        Assert.That(saved.GetPixel(1, 1), Is.EqualTo(SKColors.White));
        Assert.That(File.ReadAllBytes(_original), Is.EqualTo(_originalBytes), "The original is never changed.");
    }

    [Test]
    public async Task SkipsAiWhenTheFeatureIsSwitchedOff()
    {
        var ai = UseAi();
        AiFeatures.SetEnabled(AiFeatures.RedactImage, false);
        var reviewer = new Reviewer();

        PrivacyRedactionResult result = await Create(reviewer).RedactFileAsync(_original, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrivacyRedactionOutcome.Saved));
        Assert.That(reviewer.ConsentCalls, Is.Zero);
        Assert.That(ai.Prompts, Is.Empty);
        Assert.That(reviewer.Reviews, Is.EqualTo(1));
        Assert.That(reviewer.LastScan!.Status, Is.EqualTo(PrivacyScanStatus.Unsupported));
    }

    [Test]
    public async Task ItsSwitchIsIndependentOfTheUploadPrivacyScan()
    {
        var ai = UseAi();
        AiFeatures.SetEnabled(AiFeatures.PrivacyScan, false);
        var reviewer = new Reviewer();

        await Create(reviewer).RedactFileAsync(_original, CancellationToken.None);

        Assert.That(ai.Prompts, Has.Count.EqualTo(1));
        Assert.That(AssistantPrivacyAnalysisProvider.CreateActive(), Is.Null, "The upload path keeps its own switch.");
    }

    [Test]
    public async Task SkipsAiWhenConsentIsDeclined()
    {
        var ai = UseAi();
        var reviewer = new Reviewer { Consent = false };

        PrivacyRedactionResult result = await Create(reviewer).RedactFileAsync(_original, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrivacyRedactionOutcome.Saved));
        Assert.That(reviewer.ConsentCalls, Is.EqualTo(1));
        Assert.That(ai.Prompts, Is.Empty);
        Assert.That(reviewer.LastScan!.Status, Is.EqualTo(PrivacyScanStatus.Unsupported));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task SkipsAiWithoutAProviderThatReadsImages(bool configured, bool supportsImages)
    {
        var ai = UseAi(configured, supportsImages);
        var reviewer = new Reviewer();

        PrivacyRedactionResult result = await Create(reviewer).RedactFileAsync(_original, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrivacyRedactionOutcome.Saved));
        Assert.That(reviewer.ConsentCalls, Is.Zero);
        Assert.That(ai.Prompts, Is.Empty);
    }

    [Test]
    public async Task FailedAiScanIsNotReportedCompleteAndStillReachesReview()
    {
        var ai = UseAi();
        ai.Status = AiReplyStatus.Failed;
        var reviewer = new Reviewer();

        PrivacyRedactionResult result = await Create(reviewer).RedactFileAsync(_original, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrivacyRedactionOutcome.Saved));
        Assert.That(reviewer.LastScan!.Status, Is.EqualTo(PrivacyScanStatus.Error));
    }

    [Test]
    public async Task LocalEvidenceIsAddedWithAndWithoutAi()
    {
        var policy = new TaskSettingsPrivacy { Effect = PrivacyRedactionEffect.Mask, MarginPixels = 0, CustomPatterns = ["PROJECT-ALPHA"] };

        UseAi(configured: false);
        var localOnly = new Reviewer();
        await Create(localOnly, new FakeOcr(), policy).RedactFileAsync(_original, CancellationToken.None);
        Assert.That(localOnly.LastScan!.Findings.Single().Category, Is.EqualTo(PrivacyCategory.Custom));

        UseAi();
        var withAi = new Reviewer();
        PrivacyRedactionResult result = await Create(withAi, new FakeOcr(), policy).RedactFileAsync(_original, CancellationToken.None);
        Assert.That(withAi.LastScan!.Findings.Select(f => f.Category),
            Is.EquivalentTo(new[] { PrivacyCategory.Email, PrivacyCategory.Custom }));
        Assert.That(result.OutputPath, Is.EqualTo(Path.Combine(_folder, "synthetic-redacted-2.png")));
    }

    [Test]
    public async Task ChoosesAFreeNameAndLeavesExistingFilesAlone()
    {
        UseAi(configured: false);
        string first = Path.Combine(_folder, "synthetic-redacted.png");
        string second = Path.Combine(_folder, "synthetic-redacted-2.png");
        await File.WriteAllTextAsync(first, "existing");
        await File.WriteAllTextAsync(second, "existing");

        PrivacyRedactionResult result = await Create(new Reviewer()).RedactFileAsync(_original, CancellationToken.None);

        Assert.That(result.OutputPath, Is.EqualTo(Path.Combine(_folder, "synthetic-redacted-3.png")));
        Assert.That(await File.ReadAllTextAsync(first), Is.EqualTo("existing"));
        Assert.That(await File.ReadAllTextAsync(second), Is.EqualTo("existing"));
        Assert.That(File.ReadAllBytes(_original), Is.EqualTo(_originalBytes));
    }

    [Test]
    public async Task CancelWritesNothing()
    {
        var ai = UseAi();
        var reviewer = new Reviewer { Decision = PrivacyReviewDecision.Cancel };

        PrivacyRedactionResult result = await Create(reviewer).RedactFileAsync(_original, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrivacyRedactionOutcome.Cancelled));
        Assert.That(result.OutputPath, Is.Null);
        Assert.That(Directory.GetFiles(_folder), Is.EqualTo(new[] { _original }));
        Assert.That(File.ReadAllBytes(_original), Is.EqualTo(_originalBytes));
        Assert.That(ai.Prompts, Has.Count.EqualTo(1));
    }

    [TestCase(PrivacyReviewDecision.Upload)]
    [TestCase(PrivacyReviewDecision.KeepLocal)]
    public async Task OnlyASaveDecisionWritesACopy(PrivacyReviewDecision decision)
    {
        UseAi(configured: false);

        PrivacyRedactionResult result = await Create(new Reviewer { Decision = decision }).RedactFileAsync(_original, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrivacyRedactionOutcome.Cancelled));
        Assert.That(Directory.GetFiles(_folder), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task ReviewServiceWithoutASaveReviewSavesNothing()
    {
        UseAi(configured: false);

        PrivacyRedactionResult result = await new PrivacyRedactionService(new UploadOnlyReviewer(), null, null)
            .RedactFileAsync(_original, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrivacyRedactionOutcome.Cancelled));
        Assert.That(Directory.GetFiles(_folder), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task BitmapIsSavedInTheGivenFolderAndLeftUnchanged()
    {
        UseAi(configured: false);
        using var image = new SKBitmap(Width, Height);
        image.Erase(SKColors.White);
        var reviewer = new Reviewer { Mark = true };

        PrivacyRedactionResult result = await Create(reviewer).RedactAsync(image, _folder, "capture", CancellationToken.None);

        Assert.That(result.OutputPath, Is.EqualTo(Path.Combine(_folder, "capture-redacted.png")));
        Assert.That(image.GetPixel(0, 0), Is.EqualTo(SKColors.White));
        using SKBitmap saved = SKBitmap.Decode(result.OutputPath)!;
        Assert.That(saved.GetPixel(0, 0), Is.EqualTo(SKColors.Black), "The approved (manually edited) image is what is saved.");
    }

    [Test]
    public async Task UnreadableFileIsRefusedWithoutAskingAnything()
    {
        string text = Path.Combine(_folder, "notes.png");
        await File.WriteAllTextAsync(text, "not an image");
        var reviewer = new Reviewer();
        var ai = UseAi();

        PrivacyRedactionResult result = await Create(reviewer).RedactFileAsync(text, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrivacyRedactionOutcome.UnsupportedImage));
        Assert.That(reviewer.ConsentCalls + reviewer.Reviews + ai.Prompts.Count, Is.Zero);
        Assert.That(Directory.GetFiles(_folder), Has.Length.EqualTo(2));
    }

    private sealed class Reviewer : IPrivacyReviewService
    {
        public bool Consent = true;
        public PrivacyReviewDecision Decision = PrivacyReviewDecision.Save;
        public bool Mark;
        public int ConsentCalls, Reviews;
        public PrivacyScanResult? LastScan;

        public Task<bool> ConfirmCloudAnalysisAsync(string provider, CancellationToken token)
        { ConsentCalls++; return Task.FromResult(Consent); }

        public Task<PrivacyReviewResult> ReviewAsync(SKBitmap original, SKBitmap candidate, PrivacyScanResult scan,
            string destination, TaskSettingsPrivacy policy, CancellationToken token) =>
            throw new AssertionException("The redaction tool must use the save review, not the upload review.");

        public Task<PrivacyReviewResult> ReviewSaveAsync(SKBitmap original, SKBitmap candidate, PrivacyScanResult scan,
            TaskSettingsPrivacy policy, CancellationToken token)
        {
            Reviews++;
            LastScan = scan;
            if (Decision == PrivacyReviewDecision.Cancel) return Task.FromResult(new PrivacyReviewResult(Decision, null));
            SKBitmap approved = candidate.Copy();
            if (Mark) approved.SetPixel(0, 0, SKColors.Black);
            return Task.FromResult(new PrivacyReviewResult(Decision, approved));
        }
    }

    private sealed class UploadOnlyReviewer : IPrivacyReviewService
    {
        public Task<bool> ConfirmCloudAnalysisAsync(string provider, CancellationToken token) => Task.FromResult(true);
        public Task<PrivacyReviewResult> ReviewAsync(SKBitmap original, SKBitmap candidate, PrivacyScanResult scan,
            string destination, TaskSettingsPrivacy policy, CancellationToken token) =>
            Task.FromResult(new PrivacyReviewResult(PrivacyReviewDecision.Upload, candidate.Copy()));
    }

    private sealed class FakeOcr : IOcrService
    {
        public bool IsSupported => true;
        public OcrLanguage[] GetAvailableLanguages() => [];
        public Task<OcrResult> RecognizeAsync(SKBitmap image, OcrOptions options) => Task.FromResult(new OcrResult
        {
            Success = true, GeometryComplete = true, SourceWidth = image.Width, SourceHeight = image.Height,
            Words = [new("PROJECT-ALPHA", "1", 10, 10, 50, 10)]
        });
    }

    private sealed class FakeAiClient(bool supportsImages) : IAiClient
    {
        public bool Configured { get; set; } = true;
        public string Reply { get; set; } = "";
        public AiReplyStatus Status { get; set; } = AiReplyStatus.Ok;
        public List<AiPrompt> Prompts { get; } = [];

        public AiConnection? Connection => Configured ? new AiConnection("Fake AI", "fake-1", "http://localhost", supportsImages, "identity") : null;

        public Task<AiReply> AskAsync(AiPrompt prompt, CancellationToken cancellationToken = default)
        {
            Prompts.Add(prompt);
            return Task.FromResult(Status == AiReplyStatus.Ok ? new AiReply(AiReplyStatus.Ok, Reply) : new AiReply(Status));
        }
    }
}
