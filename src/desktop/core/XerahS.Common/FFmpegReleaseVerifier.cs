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

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XerahS.Common
{
    /// <summary>Verifies archives against BtbN's HTTPS-hosted checksums.sha256 manifest.</summary>
    public static class FFmpegReleaseVerifier
    {
        public const string ChecksumsFileName = "checksums.sha256";

        private static readonly Regex ChecksumLine = new(
            @"^(?<hash>[0-9a-fA-F]{64}) [ *](?<name>\S+)$",
            RegexOptions.CultureInvariant);

        /// <summary>Hash listed for <paramref name="fileName"/> in sha256sum output, lower case, or null.</summary>
        public static string? FindHash(byte[] checksums, string fileName)
        {
            string? hash = null;
            foreach (string line in Encoding.UTF8.GetString(checksums).Split('\n'))
            {
                Match match = ChecksumLine.Match(line.TrimEnd('\r'));
                if (match.Success && match.Groups["name"].Value == fileName)
                {
                    if (hash != null) return null;
                    hash = match.Groups["hash"].Value.ToLowerInvariant();
                }
            }

            return hash;
        }

        public static async Task<bool> IsFileHashValidAsync(string path, string expectedHash, CancellationToken cancellationToken = default)
        {
            await using FileStream stream = File.OpenRead(path);
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            return string.Equals(Convert.ToHexStringLower(hash), expectedHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
