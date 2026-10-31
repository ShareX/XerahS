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

using System.Xml.Linq;

namespace ShareX.AmazonS3.Plugin;

/// <summary>Region/endpoint hint parsed from an S3 PermanentRedirect or wrong-region error.</summary>
internal sealed record S3RegionRedirect(string? Region, string? EndpointHost);

/// <summary>
/// Builds ListObjectsV2 prefixes and IAM-aware Media Explorer errors.
/// </summary>
internal static class S3ExplorerListHelper
{
    /// <summary>The prefix Media Explorer treats as its root: none for the whole bucket, otherwise the upload prefix.</summary>
    public static string? GetExplorerRootPrefix(S3ConfigModel config) =>
        config.ExplorerBrowseWholeBucket ? null : config.ObjectPrefix;

    public static string ResolveListPrefix(string? objectPrefix, string? folderPath)
    {
        string root = EnsureTrailingSlash(ExtractStaticPrefix(objectPrefix));
        string folder = NormalizePrefix(folderPath);
        if (string.IsNullOrEmpty(folder))
        {
            return root;
        }

        return root + EnsureTrailingSlash(folder);
    }

    public static string ExtractStaticPrefix(string? objectPrefix)
    {
        if (string.IsNullOrWhiteSpace(objectPrefix))
        {
            return string.Empty;
        }

        string value = objectPrefix.Replace('\\', '/').Trim().Trim('/');
        int tokenIndex = value.IndexOf('%');
        if (tokenIndex < 0)
        {
            return value;
        }

        string staticPart = value[..tokenIndex];
        int lastSeparator = staticPart.LastIndexOf('/');
        return lastSeparator < 0 ? string.Empty : staticPart[..lastSeparator].Trim('/');
    }

    public static string GetExplorerPath(string objectKey, string? objectPrefix)
    {
        string normalizedKey = objectKey.Replace('\\', '/').TrimStart('/');
        string root = EnsureTrailingSlash(ExtractStaticPrefix(objectPrefix));
        return !string.IsNullOrEmpty(root) && normalizedKey.StartsWith(root, StringComparison.Ordinal)
            ? normalizedKey[root.Length..]
            : normalizedKey;
    }

    public static bool IsListBucketDenied(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("s3:ListBucket", StringComparison.OrdinalIgnoreCase)
            || message.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Access Denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("S3 request failed: 403", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildListBucketDeniedMessage(string? bucketName, string serviceMessage)
    {
        string bucket = string.IsNullOrWhiteSpace(bucketName) ? "YOUR_BUCKET" : bucketName.Trim();
        return
            "Amazon S3 Media Explorer cannot list this bucket because the service denied the ListObjectsV2 request. " +
            "Existing object upload, download, or delete permissions may still work while browsing requires a bucket-level list permission. " +
            $"For AWS S3, allow s3:ListBucket on arn:aws:s3:::{bucket}; for compatible services, grant the equivalent bucket-list permission. " +
            "If access is prefix-scoped, allow the configured static prefix with the service's prefix condition (s3:prefix on AWS). " +
            "The service said: " + serviceMessage;
    }

    /// <summary>
    /// Reads the bucket's real region/endpoint from an S3 error: the <c>x-amz-bucket-region</c>
    /// header, and the <c>Endpoint</c>/<c>Region</c> elements of PermanentRedirect or
    /// AuthorizationHeaderMalformed bodies. Returns null for any other error.
    /// </summary>
    public static S3RegionRedirect? TryParseRegionRedirect(string? body, string? bucketRegionHeader)
    {
        string? code = null;
        string? endpoint = null;
        string? region = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                XElement? root = XDocument.Parse(body).Root;
                if (root != null && root.Name.LocalName.Equals("Error", StringComparison.OrdinalIgnoreCase))
                {
                    code = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Code")?.Value;
                    endpoint = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Endpoint")?.Value;
                    region = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Region")?.Value;
                }
            }
            catch (System.Xml.XmlException)
            {
                // Not XML; rely on the header.
            }
        }

        bool isRedirect = code is "PermanentRedirect" or "AuthorizationHeaderMalformed" or "IllegalLocationConstraintException" or "TemporaryRedirect";
        if (!isRedirect && string.IsNullOrWhiteSpace(bucketRegionHeader))
        {
            return null;
        }

        if (!isRedirect && code != null)
        {
            // Other errors (AccessDenied, NoSuchBucket) also carry the header; they are not redirects.
            return null;
        }

        region = FirstNonEmpty(bucketRegionHeader, region, RegionFromEndpoint(endpoint));
        if (string.IsNullOrWhiteSpace(region) && string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        return new S3RegionRedirect(region?.Trim(), string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim());
    }

    /// <summary>Region embedded in an AWS host such as bucket.s3.ap-southeast-2.amazonaws.com.</summary>
    public static string? RegionFromEndpoint(string? endpointHost)
    {
        if (string.IsNullOrWhiteSpace(endpointHost) ||
            !endpointHost.Contains(".amazonaws.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string prefix = endpointHost[..endpointHost.IndexOf(".amazonaws.com", StringComparison.OrdinalIgnoreCase)];
        string[] labels = prefix.Split('.');
        for (int i = 0; i < labels.Length; i++)
        {
            string label = labels[i];
            if (label.StartsWith("s3-", StringComparison.OrdinalIgnoreCase) && label.Length > 3)
            {
                return label[3..];
            }

            if (label.Equals("s3", StringComparison.OrdinalIgnoreCase) && i + 1 < labels.Length)
            {
                return labels[i + 1];
            }
        }

        return null;
    }

    public static string BuildWrongRegionMessage(string? bucketName, S3RegionRedirect redirect, string serviceMessage)
    {
        string bucket = string.IsNullOrWhiteSpace(bucketName) ? "this bucket" : $"bucket '{bucketName.Trim()}'";
        string endpoint = !string.IsNullOrWhiteSpace(redirect.Region)
            ? $"s3.{redirect.Region}.amazonaws.com"
            : redirect.EndpointHost ?? "the endpoint S3 reported";
        string region = string.IsNullOrWhiteSpace(redirect.Region) ? string.Empty : $" region '{redirect.Region}' and";
        return $"Amazon S3 says {bucket} lives in another region. Set the destination's{region} endpoint to '{endpoint}'. " +
            "The service said: " + serviceMessage;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string NormalizePrefix(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Replace('\\', '/').Trim().Trim('/');
    }

    private static string EnsureTrailingSlash(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return string.Empty;
        }

        return prefix.EndsWith('/') ? prefix : prefix + "/";
    }
}
