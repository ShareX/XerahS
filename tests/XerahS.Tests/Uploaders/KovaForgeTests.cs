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

using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ShareX.KovaForge.Plugin;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public sealed class KovaForgeTests
{
    private const string ApiToken = "kfd_fake-token";

    [TestCase("https://kovaforge.org/", "https://kovaforge.org")]
    [TestCase("https://kovaforge.org/sub/?x=1#y", "https://kovaforge.org/sub")]
    [TestCase("  https://kovaforge.org  ", "https://kovaforge.org")]
    public void NormalizeServerUrl_RemovesQueryFragmentAndTrailingSlash(string input, string expected)
    {
        Assert.That(KovaForgeClient.NormalizeServerUrl(input), Is.EqualTo(expected));
    }

    [Test]
    public async Task UploadAsync_PostsRawBodyWithBearerTokenAndReturnsLink()
    {
        RecordingHandler handler = new(HttpStatusCode.Created, """{ "id": "abc", "url": "https://kovaforge.org/docs/abc/my%20notes.md" }""");
        KovaForgeClient client = new("https://kovaforge.org/", ApiToken, handler);
        using MemoryStream stream = new("# Hello"u8.ToArray());

        KovaForgeUploadResponse response = await client.UploadAsync(stream, "my notes.md", "text/markdown");

        Assert.Multiple(() =>
        {
            Assert.That(handler.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.RequestUri, Is.EqualTo(new Uri("https://kovaforge.org/docs/api/upload?filename=my%20notes.md")));
            Assert.That(handler.Authorization?.Scheme, Is.EqualTo("Bearer"));
            Assert.That(handler.Authorization?.Parameter, Is.EqualTo(ApiToken));
            Assert.That(handler.ContentType, Is.EqualTo("text/markdown"));
            Assert.That(handler.Body, Is.EqualTo("# Hello"u8.ToArray()));
            Assert.That(response.Url, Is.EqualTo("https://kovaforge.org/docs/abc/my%20notes.md"));
            Assert.That(response.Id, Is.EqualTo("abc"));
        });
    }

    [Test]
    public void UploadAsync_StripsDirectoriesFromFileName()
    {
        KovaForgeClient client = new("https://kovaforge.org", ApiToken, new RecordingHandler(HttpStatusCode.Created, "{}"));

        Assert.That(client.BuildUploadUrl(Path.GetFileName("/tmp/secret dir/report.pdf")), Is.EqualTo("https://kovaforge.org/docs/api/upload?filename=report.pdf"));
    }

    [TestCase(HttpStatusCode.Unauthorized, "token", "401")]
    [TestCase(HttpStatusCode.RequestEntityTooLarge, "25 MB", "413")]
    [TestCase(HttpStatusCode.TooManyRequests, "per hour", "429")]
    public void UploadAsync_NonSuccess_ReportsReasonWithoutLeakingToken(HttpStatusCode status, string expectedText, string expectedStatus)
    {
        RecordingHandler handler = new(status, $$"""{ "error": "bad {{ApiToken}}" }""");
        KovaForgeClient client = new("https://kovaforge.org", ApiToken, handler);
        using MemoryStream stream = new("x"u8.ToArray());

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await client.UploadAsync(stream, "a.txt"));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain(expectedText));
            Assert.That(exception.Message, Does.Contain(expectedStatus));
            Assert.That(exception.Message, Does.Not.Contain(ApiToken));
        });
    }

    [Test]
    public void UploadAsync_ResponseWithoutLink_Fails()
    {
        KovaForgeClient client = new("https://kovaforge.org", ApiToken, new RecordingHandler(HttpStatusCode.Created, """{ "id": "abc" }"""));
        using MemoryStream stream = new("x"u8.ToArray());

        Assert.ThrowsAsync<InvalidOperationException>(async () => await client.UploadAsync(stream, "a.txt"));
    }

    [Test]
    public void Provider_ExposesMetadataAndDefaultsWithoutToken()
    {
        KovaForgeProvider provider = new();
        string settingsJson = provider.GetDefaultSettings(UploaderCategory.Text);
        KovaForgeConfigModel? config = JsonConvert.DeserializeObject<KovaForgeConfigModel>(settingsJson);

        Assert.Multiple(() =>
        {
            Assert.That(provider.ProviderId, Is.EqualTo("kovaforge"));
            Assert.That(provider.SupportedCategories, Is.EqualTo(new[] { UploaderCategory.Image, UploaderCategory.Text, UploaderCategory.File }));
            Assert.That(config!.ServerUrl, Is.EqualTo("https://kovaforge.org"));
            Assert.That(config.TextAsMarkdown, Is.True);
            Assert.That(JObject.Parse(settingsJson).Properties().Select(p => p.Name), Is.EquivalentTo(new[] { "SecretKey", "ServerUrl", "TextAsMarkdown" }));
            Assert.That(settingsJson, Does.Not.Contain("Token"));
        });
    }

    [Test]
    public void ValidateSettings_RequiresValidUrlAndStoredToken()
    {
        KovaForgeProvider provider = new();

        Assert.That(provider.ValidateSettings("""{ "SecretKey": "k", "ServerUrl": "https://kovaforge.org" }"""), Is.False, "no token stored");
        Assert.That(provider.ValidateSettings("not json"), Is.False);
    }

    [Test]
    public void SecretReferences_PointAtTheStoredToken()
    {
        KovaForgeProvider provider = new();

        IReadOnlyList<InstanceSecretReference> references = provider.GetSecretReferences("""{ "SecretKey": "abc" }""");

        Assert.That(references, Has.Count.EqualTo(1));
        Assert.That(references[0].SecretKey, Is.EqualTo("abc"));
        Assert.That(provider.GetSecretReferences(""), Is.Empty);
    }

    [TestCase(UploaderCategory.Text, "note.txt", true, "note.md")]
    [TestCase(UploaderCategory.Text, "note", true, "note.md")]
    [TestCase(UploaderCategory.Text, "note.log", true, "note.log")]
    [TestCase(UploaderCategory.Text, "note.txt", false, "note.txt")]
    [TestCase(UploaderCategory.Image, "a.txt", true, "a.txt")]
    [TestCase(UploaderCategory.File, "a.txt", true, "a.txt")]
    public void ResolveFileName_RenamesPlainTextToMarkdownOnlyWhenAsked(UploaderCategory category, string name, bool asMarkdown, string expected)
    {
        KovaForgeUploader uploader = new(new KovaForgeConfigModel { TextAsMarkdown = asMarkdown }, ApiToken);
        using MemoryStream stream = new();

        string actual = uploader.ResolveFileName(new UploadRequest { Content = stream, FileName = name, Category = category });

        Assert.That(actual, Is.EqualTo(expected));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public RecordingHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public string? ContentType { get; private set; }
        public byte[] Body { get; private set; } = Array.Empty<byte>();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization;
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Body = request.Content == null ? Array.Empty<byte>() : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
        }
    }
}
