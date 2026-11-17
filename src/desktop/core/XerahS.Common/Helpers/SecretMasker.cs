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

namespace XerahS.Common.Helpers
{
    /// <summary>
    /// Masks known secret values (access tokens, passwords, API keys) in text that
    /// may contain server response bodies before it is written to debug logs or
    /// error history.
    /// </summary>
    public static class SecretMasker
    {
        private const string MaskedValue = "***";

        // JSON-style "key": "value" pairs for known secret field names.
        // Group 1 captures the key name (without quotes) so the replacement can
        // rebuild the pair as "key": "***". Case-insensitive on purpose.
        private static readonly Regex JsonSecretValueRegex = new Regex(
            "\"(access_token|refresh_token|api[_-]?key|password|secret|token|authorization|client_secret)\"\\s*:\\s*\"[^\"]*\"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // JWT tokens: three dot-separated base64url segments, header always starts with "eyJ".
        private static readonly Regex JwtTokenRegex = new Regex(
            @"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b",
            RegexOptions.Compiled);

        // Long hex sequences (>= 32 chars, i.e. at least 128 bits) are commonly used
        // as API keys/secrets and are unlikely to appear in ordinary text.
        private static readonly Regex LongHexSequenceRegex = new Regex(
            @"\b[0-9a-fA-F]{32,}\b",
            RegexOptions.Compiled);

        // Long base64 sequences with padding (>= 40 chars) are commonly used as tokens.
        // Unpadded base64url tokens are covered by the JWT pattern.
        private static readonly Regex LongBase64SequenceRegex = new Regex(
            @"\b[A-Za-z0-9+/]{40,}={1,2}(?!\w)",
            RegexOptions.Compiled);

        /// <summary>
        /// Replaces values of known secret keys and long token-like sequences with "***".
        /// Returns an empty string for null, empty or whitespace-only input.
        /// </summary>
        public static string MaskSecrets(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string masked = JsonSecretValueRegex.Replace(text, "\"$1\": \"" + MaskedValue + "\"");
            masked = JwtTokenRegex.Replace(masked, MaskedValue);
            masked = LongHexSequenceRegex.Replace(masked, MaskedValue);
            masked = LongBase64SequenceRegex.Replace(masked, MaskedValue);

            return masked;
        }
    }
}