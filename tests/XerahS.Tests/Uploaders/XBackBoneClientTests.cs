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
using System.Text;
using NUnit.Framework;
using ShareX.XBackBone.Plugin;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public sealed class XBackBoneClientTests
{
    private const string ApiToken = "fake-token|with-pipe";

    [TestCase("https://xbackbone.example.invalid/", "https://xbackbone.example.invalid")]
    [TestCase("https://xbackbone.example.invalid/apps/files/", "https://xbackbone.example.invalid/apps/files")]
    [TestCase("https://xbackbone.example.invalid/apps/files/?page=1#uploads", "https://xbackbone.example.invalid/apps/files")]
    public void NormalizeServerUrl_RemovesQueryFragmentAndTrailingSlashWhilePreservingSubpath(string input, string expected)
    {
        string actual = XBackBoneClient.NormalizeServerUrl(input);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public async Task UploadAsync_Stable3_UsesProvenWireContractAndMapsUrls()
    {
        RecordingHttpMessageHandler handler = new(HttpStatusCode.Created, /*lang=json*/ """
            {
              "url": "https://xbackbone.example.invalid/s/abc123",
              "raw_url": "https://xbackbone.example.invalid/raw/abc123"
            }
            """);
        XBackBoneClient client = new("https://xbackbone.example.invalid/subpath/", ApiToken, handler);
        using MemoryStream stream = new("stable payload"u8.ToArray());

        XBackBoneUploadResponse response = await client.UploadAsync(
            stream,
            "capture.png",
            XBackBoneApiGeneration.Stable3);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.RequestUri, Is.EqualTo(new Uri("https://xbackbone.example.invalid/subpath/upload")));
            Assert.That(handler.Authorization, Is.Null);
            Assert.That(handler.AcceptMediaTypes, Is.Empty);
            Assert.That(handler.Parts.Keys, Is.EquivalentTo(new[] { "upload", "token" }));
            Assert.That(handler.Parts["upload"].FileName, Is.EqualTo("capture.png"));
            Assert.That(handler.Parts["upload"].Bytes, Is.EqualTo("stable payload"u8.ToArray()));
            Assert.That(handler.Parts["token"].Text, Is.EqualTo(ApiToken));
            Assert.That(response.CanonicalUrl, Is.EqualTo("https://xbackbone.example.invalid/s/abc123"));
            Assert.That(response.RawUrl, Is.EqualTo("https://xbackbone.example.invalid/raw/abc123"));
            Assert.That(response.DeletionUrl, Is.Null);
        });
    }

    [Test]
    public async Task UploadAsync_ApiV1_UsesProvenWireContractAndMapsUrls()
    {
        RecordingHttpMessageHandler handler = new(HttpStatusCode.Created, /*lang=json*/ """
            {
              "data": {
                "preview_ext_url": "https://xbackbone.example.invalid/p/abc123",
                "raw_url": "https://xbackbone.example.invalid/r/abc123",
                "deletion_url": "https://xbackbone.example.invalid/delete/abc123"
              }
            }
            """);
        XBackBoneClient client = new("https://xbackbone.example.invalid/subpath", ApiToken, handler);
        using MemoryStream stream = new("v1 payload"u8.ToArray());

        XBackBoneUploadResponse response = await client.UploadAsync(
            stream,
            "notes.txt",
            XBackBoneApiGeneration.ApiV1);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.RequestUri, Is.EqualTo(new Uri("https://xbackbone.example.invalid/subpath/api/v1/upload")));
            Assert.That(handler.Authorization?.Scheme, Is.EqualTo("Bearer"));
            Assert.That(handler.Authorization?.Parameter, Is.EqualTo(ApiToken));
            Assert.That(handler.AcceptMediaTypes, Does.Contain("application/json"));
            Assert.That(handler.Parts.Keys, Is.EquivalentTo(new[] { "file", "name" }));
            Assert.That(handler.Parts.ContainsKey("token"), Is.False);
            Assert.That(handler.Parts["file"].FileName, Is.EqualTo("notes.txt"));
            Assert.That(handler.Parts["file"].Bytes, Is.EqualTo("v1 payload"u8.ToArray()));
            Assert.That(handler.Parts["name"].Text, Is.EqualTo("notes.txt"));
            Assert.That(response.CanonicalUrl, Is.EqualTo("https://xbackbone.example.invalid/p/abc123"));
            Assert.That(response.RawUrl, Is.EqualTo("https://xbackbone.example.invalid/r/abc123"));
            Assert.That(response.DeletionUrl, Is.EqualTo("https://xbackbone.example.invalid/delete/abc123"));
        });
    }

    [TestCase(HttpStatusCode.Unauthorized, "authentication or permission", "401")]
    [TestCase(HttpStatusCode.Forbidden, "authentication or permission", "403")]
    [TestCase(HttpStatusCode.RequestEntityTooLarge, "too large", "413")]
    [TestCase(HttpStatusCode.UnprocessableEntity, "upload data", "422")]
    public async Task UploadAsync_NonSuccess_UsesServerMessageWithoutLeakingToken(
        HttpStatusCode statusCode,
        string expectedDescription,
        string expectedStatus)
    {
        string body = $$"""{ "message": "Rejected credential {{ApiToken}} for this request" }""";
        RecordingHttpMessageHandler handler = new(statusCode, body);
        XBackBoneClient client = new("https://xbackbone.example.invalid", ApiToken, handler);
        using MemoryStream stream = new("payload"u8.ToArray());

        InvalidOperationException? exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.UploadAsync(stream, "capture.png", XBackBoneApiGeneration.ApiV1));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain(expectedDescription).IgnoreCase);
            Assert.That(exception.Message, Does.Contain(expectedStatus));
            Assert.That(exception.Message, Does.Contain("Rejected credential"));
            Assert.That(exception.Message, Does.Contain("[redacted]"));
            Assert.That(exception.Message, Does.Not.Contain(ApiToken));
        });
    }

    [Test]
    public async Task UploadAsync_OversizedServerMessage_IsBounded()
    {
        string oversizedMessage = new('x', 20_000);
        RecordingHttpMessageHandler handler = new(
            HttpStatusCode.BadGateway,
            $$"""{ "message": "{{oversizedMessage}}" }""");
        XBackBoneClient client = new("https://xbackbone.example.invalid", ApiToken, handler);
        using MemoryStream stream = new("payload"u8.ToArray());

        InvalidOperationException? exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.UploadAsync(stream, "capture.png", XBackBoneApiGeneration.Stable3));

        Assert.That(exception!.Message.Length, Is.LessThan(1_000));
    }

    [Test]
    public async Task UploadAsync_MalformedJson_ThrowsActionableError()
    {
        RecordingHttpMessageHandler handler = new(HttpStatusCode.Created, "<html>not json</html>", "text/html");
        XBackBoneClient client = new("https://xbackbone.example.invalid", ApiToken, handler);
        using MemoryStream stream = new("payload"u8.ToArray());

        InvalidOperationException? exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.UploadAsync(stream, "capture.png", XBackBoneApiGeneration.Stable3));

        Assert.That(exception!.Message, Does.Contain("malformed JSON"));
    }

    [TestCase(XBackBoneApiGeneration.Stable3, /*lang=json*/ "{ \"url\": \"\" }")]
    [TestCase(XBackBoneApiGeneration.Stable3, /*lang=json*/ "{ \"url\": \"/relative/share\" }")]
    [TestCase(XBackBoneApiGeneration.ApiV1, /*lang=json*/ "{ \"data\": {} }")]
    [TestCase(XBackBoneApiGeneration.ApiV1, /*lang=json*/ "{ \"data\": { \"preview_ext_url\": \"file:///tmp/upload\" } }")]
    public async Task UploadAsync_MissingOrInvalidCanonicalUrl_ThrowsActionableError(
        XBackBoneApiGeneration apiGeneration,
        string responseBody)
    {
        RecordingHttpMessageHandler handler = new(HttpStatusCode.Created, responseBody);
        XBackBoneClient client = new("https://xbackbone.example.invalid", ApiToken, handler);
        using MemoryStream stream = new("payload"u8.ToArray());

        InvalidOperationException? exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.UploadAsync(stream, "capture.png", apiGeneration));

        Assert.That(exception!.Message, Does.Contain("valid canonical URL"));
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;
        private readonly string _responseContentType;

        public RecordingHttpMessageHandler(
            HttpStatusCode statusCode,
            string responseBody,
            string responseContentType = "application/json")
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
            _responseContentType = responseContentType;
        }

        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public List<string> AcceptMediaTypes { get; } = new();
        public Dictionary<string, RecordedPart> Parts { get; } = new(StringComparer.Ordinal);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization;
            AcceptMediaTypes.AddRange(request.Headers.Accept.Select(value => value.MediaType ?? string.Empty));

            Assert.That(request.Content, Is.InstanceOf<MultipartFormDataContent>());
            MultipartFormDataContent form = (MultipartFormDataContent)request.Content!;
            foreach (HttpContent part in form)
            {
                string name = part.Headers.ContentDisposition?.Name?.Trim('"') ?? string.Empty;
                string? fileName = part.Headers.ContentDisposition?.FileName?.Trim('"');
                byte[] bytes = await part.ReadAsByteArrayAsync(cancellationToken);
                Parts[name] = new RecordedPart(fileName, bytes);
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, _responseContentType)
            };
        }
    }

    private sealed class RecordedPart
    {
        public RecordedPart(string? fileName, byte[] bytes)
        {
            FileName = fileName;
            Bytes = bytes;
        }

        public string? FileName { get; }
        public byte[] Bytes { get; }
        public string Text => Encoding.UTF8.GetString(Bytes);
    }
}
