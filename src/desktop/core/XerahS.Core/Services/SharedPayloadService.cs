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

namespace XerahS.Core.Services;

public static class SharedPayloadService
{
    public const string PayloadPrefix = "xerahs_shared_payload:";

    public static IReadOnlyList<SharedPayloadFile> Parse(string? payloadText)
    {
        if (string.IsNullOrWhiteSpace(payloadText) ||
            !payloadText.StartsWith(PayloadPrefix, StringComparison.Ordinal))
        {
            return Array.Empty<SharedPayloadFile>();
        }

        var encodedPayload = payloadText[PayloadPrefix.Length..];
        if (string.IsNullOrWhiteSpace(encodedPayload))
        {
            return Array.Empty<SharedPayloadFile>();
        }

        var items = new List<SharedPayloadFile>();
        var lines = encodedPayload.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var separator = line.IndexOf('|');
            if (separator <= 0 || separator >= line.Length - 1)
            {
                continue;
            }

            var encodedFileName = line[..separator];
            var base64Content = line[(separator + 1)..];

            try
            {
                var decodedName = Uri.UnescapeDataString(encodedFileName);
                var fileName = string.IsNullOrWhiteSpace(decodedName)
                    ? $"share_{Guid.NewGuid():N}.bin"
                    : Path.GetFileName(decodedName);
                var content = Convert.FromBase64String(base64Content);

                if (content.Length > 0)
                {
                    items.Add(new SharedPayloadFile(fileName, content));
                }
            }
            catch (UriFormatException)
            {
                // Ignore malformed file names and continue parsing the rest.
            }
            catch (FormatException)
            {
                // Ignore malformed payload rows and continue parsing the rest.
            }
        }

        return items;
    }
}

public sealed record SharedPayloadFile(string FileName, byte[] Content);
