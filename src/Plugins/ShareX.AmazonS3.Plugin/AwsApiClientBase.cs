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

using System.Collections.Specialized;
using System.Net;
using System.Text;
using XerahS.Uploaders;
using UploadersHttpMethod = XerahS.Uploaders.HttpMethod;

namespace ShareX.AmazonS3.Plugin;

internal sealed class AwsApiResponse
{
    public int StatusCode { get; }
    public string? Body { get; }
    public WebHeaderCollection? Headers { get; }

    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;

    public AwsApiResponse(int statusCode, string? body, WebHeaderCollection? headers)
    {
        StatusCode = statusCode;
        Body = body;
        Headers = headers;
    }
}

internal abstract class AwsApiClientBase : Uploader
{
    protected AwsApiResponse SendJsonRequest(UploadersHttpMethod method, string url, string json, NameValueCollection? headers,
        string contentType, bool allowNon2xx = false)
    {
        byte[] payload = Encoding.UTF8.GetBytes(json);
        return SendRawRequest(method, url, payload, contentType, headers, allowNon2xx);
    }

    protected AwsApiResponse SendRawRequest(UploadersHttpMethod method, string url, byte[]? payload, string? contentType,
        NameValueCollection? headers, bool allowNon2xx = false)
    {
        using MemoryStream? dataStream = payload != null && payload.Length > 0 ? new MemoryStream(payload) : null;
        using HttpWebResponse? response = GetResponse(method, url, dataStream, contentType, null, headers, null, allowNon2xx);
        string? body = ReadResponse(response);
        int statusCode = response != null ? (int)response.StatusCode : 0;
        return new AwsApiResponse(statusCode, body, response?.Headers);
    }

    private static string? ReadResponse(HttpWebResponse? response)
    {
        if (response == null)
        {
            return null;
        }

        Stream? responseStream = response.GetResponseStream();
        if (responseStream == null)
        {
            return null;
        }

        using Stream stream = responseStream;
        using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
