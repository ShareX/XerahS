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
using XerahS.Assistant.Configuration;
using XerahS.Assistant.Models;
using XerahS.Assistant.Providers;
using XerahS.Assistant.Services;
using XerahS.Core;
using XerahS.OCR;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.Assistant;

[TestFixture]
[NonParallelizable]
public sealed class AiFrameworkTests
{
    private IAiClient _originalClient = null!;

    [SetUp]
    public void SetUp()
    {
        _originalClient = AiServices.Client;
        PlatformServices.Reset();
        SettingsManager.Settings.AssistantDisabledFeatures.Clear();
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions = new OCROptions();
    }

    [TearDown]
    public void TearDown()
    {
        AiServices.Client = _originalClient;
        PlatformServices.Reset();
        SettingsManager.Settings.AssistantDisabledFeatures.Clear();
        SettingsManager.DefaultTaskSettings.CaptureSettings.OCROptions = new OCROptions();
    }

    [Test]
    public async Task ClientSaysWhenNoProviderIsSetUp()
    {
        var client = new AiClient(() => null, _ => throw new InvalidOperationException("must not be called"));

        Assert.That(client.Connection, Is.Null);
        Assert.That((await client.AskAsync(new AiPrompt("do", "this"))).Status, Is.EqualTo(AiReplyStatus.NotConfigured));
    }

    [Test]
    public async Task ClientRefusesImagesForAModelThatCannotReadThem()
    {
        var provider = new RecordingProvider("unused");
        var client = new AiClient(() => Settings("ollama"), _ => provider);
        using var image = new SKBitmap(4, 4);

        AiReply reply = await client.AskAsync(new AiPrompt("do", "this") { Images = [image] });

        Assert.That(reply.Status, Is.EqualTo(AiReplyStatus.ImagesNotSupported));
        Assert.That(provider.Requests, Is.Empty);
    }

    [Test]
    public async Task ClientSendsInstructionsInputAndImagesAndReturnsTheText()
    {
        var provider = new RecordingProvider("answer");
        var client = new AiClient(() => Settings("openai"), _ => provider);
        using var image = new SKBitmap(4, 4);

        AiReply reply = await client.AskAsync(new AiPrompt("instructions", "input") { Images = [image], MaxOutputTokens = 500 });

        Assert.That(reply.Success, Is.True);
        Assert.That(reply.Text, Is.EqualTo("answer"));
        AssistantModelRequest request = provider.Requests.Single();
        Assert.That(request.AllowImageContent, Is.True);
        Assert.That(request.PrivacyScope, Is.EqualTo(AssistantPrivacyScope.CloudImage));
        Assert.That(request.MaxOutputTokens, Is.EqualTo(500));
        Assert.That(request.Messages[0].Text, Is.EqualTo("instructions"));
        Assert.That(request.Messages[1].Text, Is.EqualTo("input"));
        Assert.That(request.Messages[1].Images!.Single().MediaType, Is.EqualTo("image/png"));
        Assert.That(client.Connection!.SupportsImages, Is.True);
    }

    [Test]
    public async Task ClientTurnsProviderErrorsAndExceptionsIntoFailedReplies()
    {
        var failing = new AiClient(() => Settings("openai"), _ => new RecordingProvider("Provider returned 401", AssistantModelResultKind.Error));
        var throwing = new AiClient(() => Settings("openai"), _ => throw new InvalidOperationException("boom"));

        AiReply failed = await failing.AskAsync(new AiPrompt("a", "b"));
        AiReply thrown = await throwing.AskAsync(new AiPrompt("a", "b"));

        Assert.That(failed.Status, Is.EqualTo(AiReplyStatus.Failed));
        Assert.That(failed.ErrorMessage, Is.EqualTo("Provider returned 401"));
        Assert.That(thrown.Status, Is.EqualTo(AiReplyStatus.Failed));
        Assert.That(thrown.ErrorMessage, Is.EqualTo("The AI request failed."));
    }

    [TestCase("{\"a\":1}", "{\"a\":1}")]
    [TestCase("```json\n{\"a\":1}\n```", "{\"a\":1}")]
    [TestCase("Here you go: [1,2] thanks", "[1,2]")]
    [TestCase("no json here", null)]
    public void JsonIsFoundInsideAModelReply(string text, string? expected)
    {
        Assert.That(AiJson.Extract(text), Is.EqualTo(expected));
        Assert.That(new AiReply(AiReplyStatus.Ok, text).TryReadJson(out var document), Is.EqualTo(expected != null));
        document?.Dispose();
    }

    [Test]
    public void FeaturesAreOnUntilSwitchedOffAndNeedAFittingProvider()
    {
        var textOnly = new FakeAiClient(supportsImages: false);

        Assert.That(AiFeatures.All.Select(feature => feature.Id), Is.Unique);
        Assert.That(AiFeatures.IsUsable(AiFeatures.TextTools, textOnly), Is.True);
        Assert.That(AiFeatures.IsUsable(AiFeatures.Ocr, textOnly), Is.False);
        Assert.That(AiFeatures.IsUsable(AiFeatures.Ocr, new FakeAiClient()), Is.True);
        Assert.That(AiFeatures.IsUsable(AiFeatures.Ocr, new FakeAiClient { Configured = false }), Is.False);

        AiFeatures.SetEnabled(AiFeatures.Ocr, false);
        AiFeatures.SetEnabled(AiFeatures.Ocr, false);

        Assert.That(AiFeatures.IsEnabled(AiFeatures.Ocr), Is.False);
        Assert.That(AiFeatures.IsUsable(AiFeatures.Ocr, new FakeAiClient()), Is.False);
        Assert.That(SettingsManager.Settings.AssistantDisabledFeatures, Is.EqualTo(new[] { AiFeatures.Ocr }));

        AiFeatures.SetEnabled(AiFeatures.Ocr, true);
        Assert.That(SettingsManager.Settings.AssistantDisabledFeatures, Is.Empty);
    }

    [TestCase("line one\r\nline two  ", false, "line one|line two")]
    [TestCase("line one\n\nline two", true, "line one line two")]
    [TestCase("NO_TEXT", false, "")]
    [TestCase("```\nfenced\n```", false, "fenced")]
    public void AiOcrTidiesTheModelReply(string reply, bool singleLine, string expected)
    {
        Assert.That(AiOcrEngine.FormatText(reply, singleLine), Is.EqualTo(expected.Replace("|", Environment.NewLine)));
    }

    [Test]
    public async Task AiOcrIsOfferedAsAnEngineAndUsedOnlyWhenChosen()
    {
        var ai = new FakeAiClient { Reply = "read by ai" };
        int paddleCalls = 0;
        var service = new OcrEngineService(null, () => true, _ =>
        {
            paddleCalls++;
            return new PaddleOcrResult([]);
        }, _ => true);
        service.AddEngine(new AiOcrEngine(ai));
        using var image = new SKBitmap(8, 8);

        Assert.That(service.Engines.Select(engine => engine.Id), Is.EqualTo(new[] { OcrEngineIds.PaddleOcr, OcrEngineIds.AiAssistant }));
        Assert.That(service.Engines[1].DisplayName, Is.EqualTo("AI Assistant (Fake AI)"));
        Assert.That(service.Engines[1].IsRemote, Is.True);
        Assert.That(service.GetAvailableLanguages(OcrEngineIds.AiAssistant).Select(l => l.LanguageTag), Is.EqualTo(new[] { "auto" }));

        OcrResult chosen = await service.RecognizeAsync(image, new OcrOptions { Engine = OcrEngineIds.AiAssistant });
        Assert.That(chosen.Text, Is.EqualTo("read by ai"));
        Assert.That(ai.Prompts.Single().Images, Has.Count.EqualTo(1));

        // Automatic, and background work that must stay on this computer, never reach the AI provider.
        await service.RecognizeAsync(image, new OcrOptions());
        await service.RecognizeAsync(image, new OcrOptions { Engine = OcrEngineIds.AiAssistant, LocalOnly = true });
        Assert.That(ai.Prompts, Has.Count.EqualTo(1));
        Assert.That(paddleCalls, Is.EqualTo(2));
    }

    [Test]
    public void AiOcrLeavesTheEngineListWhenSwitchedOffOrWithoutAProvider()
    {
        var ai = new FakeAiClient();
        var service = new OcrEngineService(null, () => true, _ => new PaddleOcrResult([]), _ => true);
        service.AddEngine(new AiOcrEngine(ai));

        AiFeatures.SetEnabled(AiFeatures.Ocr, false);
        Assert.That(service.Engines.Select(engine => engine.Id), Is.EqualTo(new[] { OcrEngineIds.PaddleOcr }));

        AiFeatures.SetEnabled(AiFeatures.Ocr, true);
        ai.Configured = false;
        Assert.That(service.Engines.Select(engine => engine.Id), Is.EqualTo(new[] { OcrEngineIds.PaddleOcr }));
    }

    [Test]
    public async Task AiOcrReportsAProviderFailure()
    {
        var engine = new AiOcrEngine(new FakeAiClient { Status = AiReplyStatus.Failed, Error = "Provider returned 429" });
        using var image = new SKBitmap(8, 8);

        OcrResult result = await engine.RecognizeAsync(image, new OcrOptions());

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("Provider returned 429"));
    }

    [Test]
    public void ThePrivacyScanIsOfferedOnlyWhenSwitchedOnWithAModelThatReadsImages()
    {
        AiServices.Client = new FakeAiClient();
        Assert.That(AssistantPrivacyAnalysisProvider.CreateActive(), Is.Not.Null);

        AiFeatures.SetEnabled(AiFeatures.PrivacyScan, false);
        Assert.That(AssistantPrivacyAnalysisProvider.CreateActive(), Is.Null);

        AiFeatures.SetEnabled(AiFeatures.PrivacyScan, true);
        AiServices.Client = new FakeAiClient(supportsImages: false);
        Assert.That(AssistantPrivacyAnalysisProvider.CreateActive(), Is.Null);
    }

    [Test]
    public async Task TheOcrWindowAiMenuWorksOnTheResultText()
    {
        var ai = new FakeAiClient { Reply = "translated" };
        AiServices.Client = ai;
        PlatformServices.Ocr = new OcrEngineService(null, () => true,
            _ => new PaddleOcrResult([new PaddleOcrTextRegion([new(0, 0), new(4, 0), new(4, 4), new(0, 4)], "hola", 0.9f)]), _ => true);
        using var image = new SKBitmap(8, 8);
        var viewModel = new OcrViewModel(image);
        await viewModel.RunOcrAsync();

        Assert.That(viewModel.AiMenuAvailable, Is.True);
        Assert.That(viewModel.ResultText, Is.EqualTo("hola"));

        await viewModel.AiTranslateCommand.ExecuteAsync(null);

        Assert.That(viewModel.ResultText, Is.EqualTo("translated"));
        Assert.That(ai.Prompts.Single().Input, Does.Contain("hola"));
        Assert.That(ai.Prompts.Single().Images, Is.Empty);

        ai.Status = AiReplyStatus.Failed;
        await viewModel.AiSummariseCommand.ExecuteAsync(null);

        Assert.That(viewModel.ResultText, Is.EqualTo("translated"), "A failed action keeps the text.");
        Assert.That(viewModel.StatusText, Is.EqualTo("The AI request failed."));
    }

    [Test]
    public void TheOcrWindowHidesTheAiMenuWhenItsFeaturesAreOff()
    {
        AiServices.Client = new FakeAiClient();
        AiFeatures.SetEnabled(AiFeatures.TextTools, false);
        AiFeatures.SetEnabled(AiFeatures.DescribeImage, false);
        using var image = new SKBitmap(8, 8);

        Assert.That(new OcrViewModel(image).AiMenuAvailable, Is.False);
    }

    private static AssistantProviderRuntimeSettings Settings(string providerId)
    {
        AssistantProviderMetadata metadata = AssistantProviderCatalog.Find(providerId)!;
        return new AssistantProviderRuntimeSettings(metadata, metadata.DefaultModelId, metadata.DefaultBaseUrl, "test-key");
    }

    private sealed class RecordingProvider(string text, AssistantModelResultKind kind = AssistantModelResultKind.Text) : IAssistantModelProvider
    {
        public List<AssistantModelRequest> Requests { get; } = [];
        public AssistantProviderMetadata Metadata => AssistantProviderCatalog.Find("openai")!;

        public Task<AssistantModelResult> CompleteAsync(AssistantModelRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new AssistantModelResult(kind, text, [], null, null));
        }

        public Task<AssistantModelResult> ValidateAsync(string modelId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeAiClient(bool supportsImages = true) : IAiClient
    {
        public bool Configured { get; set; } = true;
        public string Reply { get; set; } = "ok";
        public AiReplyStatus Status { get; set; } = AiReplyStatus.Ok;
        public string? Error { get; set; }
        public List<AiPrompt> Prompts { get; } = [];

        public AiConnection? Connection => Configured ? new AiConnection("Fake AI", "fake-1", "http://localhost", supportsImages, "identity") : null;

        public Task<AiReply> AskAsync(AiPrompt prompt, CancellationToken cancellationToken = default)
        {
            Prompts.Add(prompt);
            return Task.FromResult(Status == AiReplyStatus.Ok ? new AiReply(AiReplyStatus.Ok, Reply) : new AiReply(Status, Error: Error));
        }
    }
}
