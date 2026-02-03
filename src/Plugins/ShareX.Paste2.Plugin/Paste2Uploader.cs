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

using XerahS.Uploaders;

namespace ShareX.Paste2.Plugin;

/// <summary>
/// Paste2 uploader - supports basic text uploads
/// </summary>
public sealed class Paste2Uploader : TextUploader
{
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
        }

        return result;
    }
}
