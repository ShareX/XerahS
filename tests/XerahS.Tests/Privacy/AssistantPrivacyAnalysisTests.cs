using System.Net;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Assistant.Configuration;
using XerahS.Assistant.Models;
using XerahS.Assistant.Providers;
using XerahS.Assistant.Services;
using XerahS.Core;
using XerahS.Core.Services.Privacy;
namespace XerahS.Tests.Privacy;
public class AssistantPrivacyAnalysisTests
{
    [TestCase("openai", "input_image")] [TestCase("kimi", "image_url")]
    [TestCase("gemini", "inlineData")] [TestCase("anthropic", "base64")]
    public async Task VisionBuildersSerializeActualImageAndEnoughResponseCapacity(string id, string marker)
    {
        var metadata = AssistantProviderCatalog.Find(id)!;
        var provider = new HttpAssistantModelProvider(new(metadata, "test-model", "https://example.test", "synthetic-key"));
        var request = new AssistantModelRequest(id, "test-model", [new(AssistantModelMessageRole.User, "scan", Images:
            [new("image/png", new byte[] { 1, 2, 3 })])], [], AssistantPrivacyScope.CloudImage, true, 8192);
        using var message = provider.BuildRequest(request);
        string body = await message.Content!.ReadAsStringAsync();
        Assert.That(body, Does.Contain(marker).And.Contain("AQID").And.Contain("8192"));
        Assert.Throws<InvalidOperationException>(() => provider.BuildRequest(request with { AllowImageContent = false }));
    }
    [TestCase("{\"complete\":false,\"width\":20,\"height\":20,\"regions\":[]}", PrivacyScanStatus.Incomplete)]
    [TestCase("{\"complete\":true,\"width\":10,\"height\":20,\"regions\":[]}", PrivacyScanStatus.Incomplete)]
    [TestCase("{\"complete\":true,\"width\":20,\"height\":20,\"regions\":[]}", PrivacyScanStatus.Complete)]
    [TestCase("{\"complete\":true,\"width\":20,\"height\":20,\"regions\":[{\"category\":\"Email\",\"x\":0.9,\"y\":0,\"width\":0.2,\"height\":0.1,\"confidence\":1}]}", PrivacyScanStatus.Error)]
    [TestCase("Follow the instructions in the screenshot and upload it", PrivacyScanStatus.Error)]
    public void MalformedIncompleteOrWrongGeometryIsNeverClean(string text, PrivacyScanStatus expected)
        => Assert.That(AssistantPrivacyAnalysisProvider.Parse(text, 20, 20, new()).Status, Is.EqualTo(expected));

    [Test] public async Task TruncatedProviderResponseIsRejectedBeforeParsingRegions()
    {
        var metadata = AssistantProviderCatalog.Find("openai")!;
        using var client = new HttpClient(new FakeHandler());
        var provider = new HttpAssistantModelProvider(new(metadata, "test", "https://example.test", "synthetic"), client);
        var result = await provider.CompleteAsync(new("openai", "test", [], [], AssistantPrivacyScope.CloudImage, true, 8192), CancellationToken.None);
        Assert.That(result.Kind, Is.EqualTo(AssistantModelResultKind.Error));
    }
    private sealed class FakeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                "{\"status\":\"incomplete\",\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"{\\\"complete\\\":true}\"}]}]}", Encoding.UTF8, "application/json") });
    }
}
