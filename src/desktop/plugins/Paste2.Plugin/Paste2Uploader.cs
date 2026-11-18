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

using System.Text.RegularExpressions;
using XerahS.Uploaders;

namespace ShareX.Paste2.Plugin;

/// <summary>
/// Paste2 uploader - supports basic text uploads
/// </summary>
public sealed partial class Paste2Uploader : TextUploader
{
    [GeneratedRegex("href\\s*=\\s*[\\\"'](?<url>[^\\\"']+)[\\\"']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TryExtractDeletionUrlRegex();

    private readonly Paste2ConfigModel _config;

    public Paste2Uploader(Paste2ConfigModel config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public override UploadResult UploadText(string text, string fileName)
    {
        UploadResult result = new UploadResult();

        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        Dictionary<string, string> arguments = new Dictionary<string, string>
        {
            ["code"] = text,
            ["lang"] = string.IsNullOrWhiteSpace(_config.TextFormat) ? "text" : _config.TextFormat,
            ["description"] = _config.Description ?? string.Empty,
            ["parent"] = ""
        };

        SendRequestMultiPart("https://paste2.org/", arguments);

        if (LastResponseInfo != null)
        {
            result.URL = LastResponseInfo.ResponseURL;

            string? deletionUrl = TryExtractDeletionUrl(LastResponseInfo);
            if (!string.IsNullOrWhiteSpace(deletionUrl))
            {
                result.DeletionURL = deletionUrl;
                result.Metadata["Deletion.Provider"] = "Paste2";
                result.Metadata["Deletion.Method"] = "URL";
            }
            else
            {
                result.Metadata["Deletion.Provider"] = "Paste2";
                result.Metadata["Deletion.Available"] = "false";
                result.Metadata["Deletion.Reason"] = "Paste2 does not document a public delete API or return a detectable deletion URL.";
            }
        }

        return result;
    }

    private static string? TryExtractDeletionUrl(ResponseInfo responseInfo)
    {
        if (string.IsNullOrWhiteSpace(responseInfo.ResponseText))
        {
            return null;
        }

        foreach (Match match in TryExtractDeletionUrlRegex().Matches(responseInfo.ResponseText))
        {
            string candidate = match.Groups["url"].Value;
            if (!candidate.Contains("delete", StringComparison.OrdinalIgnoreCase) &&
                !candidate.Contains("remove", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Uri.TryCreate(candidate, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            if (Uri.TryCreate("https://paste2.org/", UriKind.Absolute, out var baseUri) &&
                Uri.TryCreate(baseUri, candidate, out var relative))
            {
                return relative.ToString();
            }
        }

        return null;
    }
}
