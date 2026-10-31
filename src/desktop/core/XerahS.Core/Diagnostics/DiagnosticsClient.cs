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

using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace XerahS.Core.Diagnostics;

public sealed class DiagnosticsUploadException : Exception
{
    public DiagnosticsUploadException(string message, HttpStatusCode? status = null, Exception? inner = null)
        : base(message, inner)
    {
        Status = status;
    }

    public HttpStatusCode? Status { get; }
}

/// <summary>Talks to the diagnostics ingest Function (web/functions/diagnostics).</summary>
public sealed class DiagnosticsClient
{
    /// <summary>Neon Function `diagnostics` on the production branch.</summary>
    public static readonly Uri DefaultEndpoint = new("https://br-floral-resonance-b4f3jotb-diagnostics.compute.c-6.us-east-2.aws.neon.tech/");

    public const string EndpointOverrideVariable = "XERAHS_DIAGNOSTICS_URL";

    private readonly HttpClient _http;
    private readonly Uri _endpoint;

    public DiagnosticsClient(HttpClient? httpClient = null, Uri? endpoint = null)
    {
        _http = httpClient ?? XerahS.Common.HttpClientFactory.Create();
        _endpoint = endpoint ?? ResolveEndpoint();
    }

    public Uri Endpoint => _endpoint;

    public static Uri ResolveEndpoint()
    {
        string? value = Environment.GetEnvironmentVariable(EndpointOverrideVariable);
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps
            ? new Uri(uri.AbsoluteUri.TrimEnd('/') + "/")
            : DefaultEndpoint;
    }

    public async Task<DiagnosticsInstallStatus> GetStatusAsync(Guid installId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_endpoint, $"v1/installs/{installId}/status"));
        using HttpResponseMessage response = await SendAsync(request, TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<DiagnosticsInstallStatus>(DiagnosticsJson.Options, cancellationToken).ConfigureAwait(false)
               ?? new DiagnosticsInstallStatus();
    }

    public async Task<DiagnosticsSubmitResult> SubmitAsync(byte[] gzipJson, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_endpoint, "v1/reports"))
        {
            Content = new ByteArrayContent(gzipJson),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Content.Headers.ContentEncoding.Add("gzip");
        using HttpResponseMessage response = await SendAsync(request, TimeSpan.FromMinutes(2), cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<DiagnosticsSubmitResult>(DiagnosticsJson.Options, cancellationToken).ConfigureAwait(false)
               ?? throw new DiagnosticsUploadException("The server returned an empty response.");
    }

    public async Task<bool> DeleteAsync(Guid reportId, string deleteToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(_endpoint, $"v1/reports/{reportId}"));
        request.Headers.Add("X-Delete-Token", deleteToken);
        using HttpResponseMessage response = await SendAsync(request, TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public static byte[] Compress(DiagnosticsReportPayload payload)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            JsonSerializer.Serialize(gzip, payload, DiagnosticsJson.Options);
        }
        return buffer.ToArray();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            return await _http.SendAsync(request, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DiagnosticsUploadException("The server did not respond in time. Check your connection and try again.");
        }
        catch (HttpRequestException ex)
        {
            throw new DiagnosticsUploadException("Could not reach the XerahS diagnostics server. Check your connection and try again.", inner: ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        string? message = null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            message = doc.RootElement.GetProperty("error").GetProperty("message").GetString();
        }
        catch
        {
            // Not our JSON error shape.
        }

        throw new DiagnosticsUploadException(
            message ?? $"The diagnostics server returned {(int)response.StatusCode} {response.ReasonPhrase}.",
            response.StatusCode);
    }
}
