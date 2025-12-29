#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;

namespace ShareX.Avalonia.Common
{
    public static class URLHelpers
    {
        private static readonly string[] URLPrefixes = new[] { "http://", "https://", "ftp://", "ftps://", "file://", "//" };

        public static string ForcePrefix(string? url, string prefix = "https://")
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            return prefix + RemovePrefixes(url);
        }

        public static string FixPrefix(string? url)
        {
            return FixPrefix(url, "https://");
        }

        public static string FixPrefix(string? url, string prefix = "https://")
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            return HasPrefix(url) ? url : prefix + url;
        }

        public static string URLEncode(string? value, bool replaceSpace = false)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string encoded = WebUtility.UrlEncode(value);
            return replaceSpace ? encoded.Replace("+", "%20", StringComparison.Ordinal) : encoded;
        }

        public static string JSONEncode(string? value)
        {
            return value?.Replace("\"", "\\\"", StringComparison.Ordinal) ?? string.Empty;
        }

        public static string XMLEncode(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("'", "&apos;", StringComparison.Ordinal);
        }

        public static string GetHostName(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                return uri.Host;
            }

            return url;
        }

        public static string CombineURL(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            string combined = parts[0]?.TrimEnd('/') ?? string.Empty;
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i]?.Trim('/') ?? string.Empty;
                if (part.Length > 0)
                {
                    combined = combined + "/" + part;
                }
            }

            return combined;
        }

        public static bool HasPrefix(string? url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            foreach (string prefix in URLPrefixes)
            {
                if (url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static string RemovePrefixes(string? url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            foreach (string prefix in URLPrefixes)
            {
                if (url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return url.Substring(prefix.Length);
                }
            }

            return url;
        }

        public static string CreateQueryString(string url, NameValueCollection args)
        {
            if (args == null || args.Count == 0)
            {
                return url;
            }

            string query = string.Join("&", args.AllKeys.Select(key => $"{URLEncode(key)}={URLEncode(args[key])}"));
            return url.Contains("?", StringComparison.Ordinal) ? $"{url}&{query}" : $"{url}?{query}";
        }

        public static string CreateQueryString(NameValueCollection args)
        {
            if (args == null || args.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("&", args.AllKeys.Select(key => $"{URLEncode(key)}={URLEncode(args[key])}"));
        }

        public static string CreateQueryString(string url, Dictionary<string, string> args)
        {
            if (args == null || args.Count == 0)
            {
                return url;
            }

            string query = string.Join("&", args.Select(pair => $"{URLEncode(pair.Key)}={URLEncode(pair.Value)}"));
            return url.Contains("?", StringComparison.Ordinal) ? $"{url}&{query}" : $"{url}?{query}";
        }

        public static string CreateQueryString(Dictionary<string, string> args)
        {
            if (args == null || args.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("&", args.Select(pair => $"{URLEncode(pair.Key)}={URLEncode(pair.Value)}"));
        }

        public static NameValueCollection ParseQueryString(string url)
        {
            NameValueCollection nvc = new NameValueCollection();

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                return nvc;
            }

            string query = uri.Query.TrimStart('?');
            foreach (string part in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kvp = part.Split(new[] { '=' }, 2);
                string key = WebUtility.UrlDecode(kvp[0]);
                string value = kvp.Length > 1 ? WebUtility.UrlDecode(kvp[1]) : string.Empty;
                nvc.Add(key, value);
            }

            return nvc;
        }

        public static string RemoveQueryString(string url)
        {
            int index = url.IndexOf("?", StringComparison.Ordinal);
            return index > 0 ? url.Substring(0, index) : url;
        }

        public static List<string> GetPaths(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                return new List<string>();
            }

            return uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public static string GetDirectoryPath(string url)
        {
            List<string> paths = GetPaths(url);
            if (paths.Count <= 1)
            {
                return "/";
            }

            return "/" + string.Join("/", paths.Take(paths.Count - 1));
        }

        public static string GetFileName(string url)
        {
            List<string> paths = GetPaths(url);
            return paths.Count == 0 ? string.Empty : paths[^1];
        }

        public static void OpenURL(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
            }
        }
    }
}
