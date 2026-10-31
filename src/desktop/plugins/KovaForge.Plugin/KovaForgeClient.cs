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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http.Headers;
using XerahS.Common;

namespace ShareX.KovaForge.Plugin;

/// <summary>Talks to KovaForge Docs: POST {server}/docs/api/upload?filename=... with a bearer token and the raw file as the body.</summary>
public sealed class KovaForgeClient
{
    private const int MaximumResponseLength = 8192;
    private const int MaximumServerMessageLength = 512;
    private readonly string _serverUrl;
    private readonly string _apiToken;
    private readonly HttpClient? _httpClient;

    public KovaForgeClient(string serverUrl, string apiToken)
        : this(serverUrl, apiToken, httpClient: null)
    {
    }

    internal KovaForgeClient(string serverUrl, string apiToken, HttpMessageHandler handler)
        : this(serverUrl, apiToken, new HttpClient(handler, disposeHandler: true))
    {
    }

    private KovaForgeClient(string serverUrl, string apiToken, HttpClient? httpClient)
    {
        _serverUrl = NormalizeServerUrl(serverUrl);
        _apiToken = apiToken ?? string.Empty;
        _httpClient = httpClient;
    }

    private HttpClient Http => _httpClient ?? HttpClientFactory.Create(allowAutoRedirect: false, infiniteTimeout: true);

    public static string NormalizeServerUrl(string? serverUrl)
    {
        string value = serverUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return value.TrimEnd('/');
        }

        string path = uri.AbsolutePath.TrimEnd('/');
        UriBuilder builder = new(uri)
        {
            Path = string.IsNullOrEmpty(path) ? "/" : path,
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    public async Task<KovaForgeUploadResponse> UploadAsync(
        Stream stream,
        string fileName,
        string? contentType = null,
        Action<int>? reportProgress = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        EnsureConfigured();

        string safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "upload";
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using HttpRequestMessage request = new(HttpMethod.Post, BuildUploadUrl(safeFileName));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        ProgressStreamContent content = new(stream, reportProgress);
        content.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? MimeTypes.GetMimeTypeFromFileName(safeFileName) : contentType);
        request.Content = content;

        using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        string body = await ReadBodyBoundedAsync(response.Content, cancellation);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildHttpErrorMessage(response, body));
        }

        JObject payload;
        try
        {
            payload = JObject.Parse(body);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("KovaForge returned malformed JSON after the upload.");
        }

        string? url = payload.Value<string>("url")?.Trim();
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || !IsHttpScheme(uri))
        {
            throw new InvalidOperationException("KovaForge upload succeeded but the response did not contain a valid link.");
        }

        return new KovaForgeUploadResponse(url, payload.Value<string>("id") ?? string.Empty);
    }

    internal string BuildUploadUrl(string fileName)
    {
        return _serverUrl + "/docs/api/upload?filename=" + Uri.EscapeDataString(fileName);
    }

    private void EnsureConfigured()
    {
        if (!Uri.TryCreate(_serverUrl, UriKind.Absolute, out Uri? uri) || !IsHttpScheme(uri))
        {
            throw new InvalidOperationException("KovaForge server URL must be a valid http:// or https:// URL.");
        }

        if (string.IsNullOrWhiteSpace(_apiToken))
        {
            throw new InvalidOperationException("KovaForge upload token is required.");
        }
    }

    private string BuildHttpErrorMessage(HttpResponseMessage response, string body)
    {
        string baseMessage = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                $"KovaForge rejected the upload token (HTTP {(int)response.StatusCode}). Create a new token on kovaforge.org/docs.",
            HttpStatusCode.RequestEntityTooLarge =>
                "KovaForge rejected the upload because it is larger than 25 MB (HTTP 413).",
            HttpStatusCode.TooManyRequests =>
                "KovaForge limits uploads per hour. Try again later (HTTP 429).",
            _ => $"KovaForge upload failed (HTTP {(int)response.StatusCode})."
        };

        string? serverMessage = TryReadServerMessage(body);
        return string.IsNullOrWhiteSpace(serverMessage)
            ? baseMessage
            : baseMessage + " " + RedactToken(serverMessage);
    }

    private static string? TryReadServerMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            string? message = JObject.Parse(body).Value<string>("error")?.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            string singleLine = string.Join(" ", message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return singleLine.Length <= MaximumServerMessageLength
                ? singleLine
                : singleLine[..MaximumServerMessageLength] + "...";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string RedactToken(string message)
    {
        return string.IsNullOrEmpty(_apiToken)
            ? message
            : message.Replace(_apiToken, "[redacted]", StringComparison.Ordinal);
    }

    private static async Task<string> ReadBodyBoundedAsync(HttpContent content, CancellationToken cancellation)
    {
        await using Stream stream = await content.ReadAsStreamAsync(cancellation);
        using StreamReader reader = new(stream);
        char[] buffer = new char[MaximumResponseLength + 1];
        int totalRead = 0;

        while (totalRead < buffer.Length)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellation);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return new string(buffer, 0, Math.Min(totalRead, MaximumResponseLength));
    }

    private static bool IsHttpScheme(Uri uri)
    {
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ProgressStreamContent : HttpContent
    {
        private readonly Stream _source;
        private readonly Action<int>? _reportProgress;

        public ProgressStreamContent(Stream source, Action<int>? reportProgress)
        {
            _source = source;
            _reportProgress = reportProgress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            byte[] buffer = new byte[81920];

            if (_source.CanSeek)
            {
                _source.Position = 0;
            }

            while (true)
            {
                int read = await _source.ReadAsync(buffer.AsMemory(0, buffer.Length));
                if (read <= 0)
                {
                    break;
                }

                await stream.WriteAsync(buffer.AsMemory(0, read));
                _reportProgress?.Invoke(read);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            if (_source.CanSeek)
            {
                length = _source.Length;
                return true;
            }

            length = 0;
            return false;
        }
    }
}

public sealed class KovaForgeUploadResponse
{
    public KovaForgeUploadResponse(string url, string id)
    {
        Url = url;
        Id = id;
    }

    public string Url { get; }
    public string Id { get; }
}
