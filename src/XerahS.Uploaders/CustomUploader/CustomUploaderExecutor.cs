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

using XerahS.Common;

namespace XerahS.Uploaders.CustomUploader;

/// <summary>
/// Unified execution engine for custom uploaders.
/// Handles all upload types: Image, Text, File, URL Shortening, URL Sharing.
/// </summary>
public sealed class CustomUploaderExecutor : GenericUploader
{
    private readonly CustomUploaderItem _uploader;
    private readonly CustomUploaderExecutionMode _mode;

    /// <summary>
    /// Execution mode for the custom uploader.
    /// </summary>
    public enum CustomUploaderExecutionMode
    {
        /// <summary>
        /// Upload binary file data (Image/File).
        /// </summary>
        FileUpload,

        /// <summary>
        /// Upload text content.
        /// </summary>
        TextUpload,

        /// <summary>
        /// Shorten a URL.
        /// </summary>
        UrlShortener,

        /// <summary>
        /// Share a URL.
        /// </summary>
        UrlSharing
    }

    public CustomUploaderExecutor(CustomUploaderItem uploaderItem, CustomUploaderExecutionMode mode = CustomUploaderExecutionMode.FileUpload)
    {
        _uploader = uploaderItem ?? throw new ArgumentNullException(nameof(uploaderItem));
        _mode = mode;
    }

    /// <summary>
    /// Uploads a file stream. Used for Image and File uploads.
    /// </summary>
    public override UploadResult Upload(Stream stream, string fileName)
    {
        UploadResult result = new UploadResult();
        CustomUploaderInput input = new CustomUploaderInput(fileName, "");

        try
        {
            switch (_uploader.Body)
            {
                case CustomUploaderBody.MultipartFormData:
                    result = SendRequestFile(
                        _uploader.GetRequestURL(input),
                        stream,
                        fileName,
                        _uploader.GetFileFormName(),
                        _uploader.GetArguments(input),
                        _uploader.GetHeaders(input),
                        null,
                        _uploader.RequestMethod
                    );
                    break;

                case CustomUploaderBody.Binary:
                    result.Response = SendRequest(
                        _uploader.RequestMethod,
                        _uploader.GetRequestURL(input),
                        stream,
                        MimeTypes.GetMimeTypeFromFileName(fileName),
                        null,
                        _uploader.GetHeaders(input)
                    );
                    break;

                case CustomUploaderBody.FormURLEncoded:
                    // Read stream into base64 and pass as argument
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        byte[] data = ms.ToArray();
                        string base64 = Convert.ToBase64String(data);
                        input = new CustomUploaderInput(fileName, base64);
                    }

                    result.Response = SendRequestURLEncoded(
                        _uploader.RequestMethod,
                        _uploader.GetRequestURL(input),
                        _uploader.GetArguments(input),
                        _uploader.GetHeaders(input)
                    );
                    break;

                case CustomUploaderBody.JSON:
                case CustomUploaderBody.XML:
                    // Read stream for body data replacement
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        byte[] data = ms.ToArray();
                        string base64 = Convert.ToBase64String(data);
                        input = new CustomUploaderInput(fileName, base64);
                    }

                    string requestBody = _uploader.GetData(input);
                    result.Response = SendRequest(
                        _uploader.RequestMethod,
                        _uploader.GetRequestURL(input),
                        requestBody,
                        _uploader.GetContentType(),
                        null,
                        _uploader.GetHeaders(input)
                    );
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported body type for file upload: {_uploader.Body}");
            }

            _uploader.TryParseResponse(result, LastResponseInfo, Errors, input);
        }
        catch (Exception ex)
        {
            Errors.Add($"Upload failed: {ex.Message}");
            result.Response = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Uploads text content.
    /// </summary>
    /// <param name="text">Text content to upload.</param>
    /// <param name="fileName">Optional file name for the text.</param>
    /// <returns>Upload result with the URL.</returns>
    public UploadResult UploadText(string text, string fileName = "")
    {
        UploadResult result = new UploadResult();

        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "text.txt";
        }

        CustomUploaderInput input = new CustomUploaderInput(fileName, text);

        try
        {
            switch (_uploader.Body)
            {
                case CustomUploaderBody.None:
                    result.Response = SendRequest(
                        _uploader.RequestMethod,
                        _uploader.GetRequestURL(input),
                        headers: _uploader.GetHeaders(input)
                    );
                    break;

                case CustomUploaderBody.MultipartFormData:
                    result = SendRequestFile(
                        _uploader.GetRequestURL(input),
                        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)),
                        fileName,
                        _uploader.GetFileFormName(),
                        _uploader.GetArguments(input),
                        _uploader.GetHeaders(input),
                        null,
                        _uploader.RequestMethod
                    );
                    break;

                case CustomUploaderBody.FormURLEncoded:
                    result.Response = SendRequestURLEncoded(
                        _uploader.RequestMethod,
                        _uploader.GetRequestURL(input),
                        _uploader.GetArguments(input),
                        _uploader.GetHeaders(input)
                    );
                    break;

                case CustomUploaderBody.JSON:
                case CustomUploaderBody.XML:
                    string requestBody = _uploader.GetData(input);
                    result.Response = SendRequest(
                        _uploader.RequestMethod,
                        _uploader.GetRequestURL(input),
                        requestBody,
                        _uploader.GetContentType(),
                        null,
                        _uploader.GetHeaders(input)
                    );
                    break;

                case CustomUploaderBody.Binary:
                    result.Response = SendRequest(
                        _uploader.RequestMethod,
                        _uploader.GetRequestURL(input),
                        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)),
                        RequestHelpers.ContentTypeOctetStream,
                        null,
                        _uploader.GetHeaders(input)
                    );
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported body type for text upload: {_uploader.Body}");
            }

            _uploader.TryParseResponse(result, LastResponseInfo, Errors, input);
        }
        catch (Exception ex)
        {
            Errors.Add($"Text upload failed: {ex.Message}");
            result.Response = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Shortens a URL using the custom uploader.
    /// </summary>
    /// <param name="url">URL to shorten.</param>
    /// <returns>Upload result with the shortened URL.</returns>
    public UploadResult ShortenUrl(string url)
    {
        UploadResult result = new UploadResult { URL = url };
        CustomUploaderInput input = new CustomUploaderInput("", url);

        try
        {
            switch (_uploader.Body)
            {
                case CustomUploaderBody.None:
                    result.Response = SendRequest(
                        _uploader.RequestMethod,
                        _uploader.GetRequestURL(input),
                        headers: _uploader.GetHeaders(input)
                    );
                    break;

                case CustomUploaderBody.FormURLEncoded:
                    result.Response = SendRequestURLEncoded(
                        _uploader.RequestMethod,
                        _uploader.GetRequestURL(input),
                        _uploader.GetArguments(input),
                        _uploader.GetHeaders(input)
                    );
                    break;

                case CustomUploaderBody.JSON:
                case CustomUploaderBody.XML:
                    string requestBody = _uploader.GetData(input);
                    result.Response = SendRequest(
                        _uploader.RequestMethod,
                        _uploader.GetRequestURL(input),
                        requestBody,
                        _uploader.GetContentType(),
                        null,
                        _uploader.GetHeaders(input)
                    );
                    break;

                case CustomUploaderBody.MultipartFormData:
                    result.Response = SendRequestMultiPart(
                        _uploader.GetRequestURL(input),
                        _uploader.GetArguments(input),
                        _uploader.GetHeaders(input),
                        null,
                        _uploader.RequestMethod
                    );
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported body type for URL shortening: {_uploader.Body}");
            }

            _uploader.TryParseResponse(result, LastResponseInfo, Errors, input, isShortenedURL: true);
        }
        catch (Exception ex)
        {
            Errors.Add($"URL shortening failed: {ex.Message}");
            result.Response = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Shares a URL using the custom uploader.
    /// </summary>
    /// <param name="url">URL to share.</param>
    /// <returns>Upload result.</returns>
    public UploadResult ShareUrl(string url)
    {
        UploadResult result = new UploadResult { URL = url };
        CustomUploaderInput input = new CustomUploaderInput("", url);

        try
        {
            switch (_uploader.Body)
            {
                case CustomUploaderBody.None:
                    result.Response = SendRequest(
                        _uploader.RequestMethod,
                        _uploader.GetRequestURL(input),
                        headers: _uploader.GetHeaders(input)
                    );
                    break;

                case CustomUploaderBody.FormURLEncoded:
                    result.Response = SendRequestURLEncoded(
                        _uploader.RequestMethod,
                        _uploader.GetRequestURL(input),
                        _uploader.GetArguments(input),
                        _uploader.GetHeaders(input)
                    );
                    break;

                case CustomUploaderBody.JSON:
                case CustomUploaderBody.XML:
                    string requestBody = _uploader.GetData(input);
                    result.Response = SendRequest(
                        _uploader.RequestMethod,
                        _uploader.GetRequestURL(input),
                        requestBody,
                        _uploader.GetContentType(),
                        null,
                        _uploader.GetHeaders(input)
                    );
                    break;

                case CustomUploaderBody.MultipartFormData:
                    result.Response = SendRequestMultiPart(
                        _uploader.GetRequestURL(input),
                        _uploader.GetArguments(input),
                        _uploader.GetHeaders(input),
                        null,
                        _uploader.RequestMethod
                    );
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported body type for URL sharing: {_uploader.Body}");
            }

            _uploader.TryParseResponse(result, LastResponseInfo, Errors, input);
        }
        catch (Exception ex)
        {
            Errors.Add($"URL sharing failed: {ex.Message}");
            result.Response = ex.Message;
        }

        return result;
    }
}
