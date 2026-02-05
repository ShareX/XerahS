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
using System.Text;
using System.Text.RegularExpressions;
using XerahS.Common;
using XerahS.Uploaders;
using UploadersHttpMethod = XerahS.Uploaders.HttpMethod;

namespace ShareX.AmazonS3.Plugin;

internal sealed class S3ProvisionResult
{
    public bool IsSuccess { get; }
    public string Message { get; }

    public S3ProvisionResult(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }
}

internal sealed class S3Provisioner : AwsApiClientBase
{
    private const string DefaultRegion = "us-east-1";
    private const string DefaultEndpoint = "s3.amazonaws.com";
    private const string Scheme = "https://";

    private readonly string _accessKeyId;
    private readonly string _secretAccessKey;
    private readonly string? _sessionToken;

    public S3Provisioner(string accessKeyId, string secretAccessKey, string? sessionToken)
    {
        _accessKeyId = accessKeyId;
        _secretAccessKey = secretAccessKey;
        _sessionToken = sessionToken;
    }

    public S3ProvisionResult EnsureBucket(string bucketName)
    {
        if (!S3BucketNameValidator.IsValid(bucketName))
        {
            return new S3ProvisionResult(false, "Bucket name is invalid.");
        }

        bool usePathStyle = bucketName.Contains(".");
        AwsApiResponse head = SendBucketRequest(UploadersHttpMethod.HEAD, bucketName, usePathStyle, "", null, null, allowNon2xx: true);

        if (head.StatusCode == 200)
        {
            return ApplyPublicAccessBlock(bucketName, usePathStyle);
        }

        if (head.StatusCode == 404)
        {
            AwsApiResponse create = SendBucketRequest(UploadersHttpMethod.PUT, bucketName, usePathStyle, "", null, null, allowNon2xx: true);
            if (!create.IsSuccess && create.StatusCode != 200 && create.StatusCode != 204)
            {
                if (create.StatusCode == 409)
                {
                    AwsApiResponse confirm = SendBucketRequest(UploadersHttpMethod.HEAD, bucketName, usePathStyle, "", null, null, allowNon2xx: true);
                    if (confirm.StatusCode == 200)
                    {
                        return ApplyPublicAccessBlock(bucketName, usePathStyle);
                    }
                }

                return new S3ProvisionResult(false, $"Bucket creation failed ({create.StatusCode}). {create.Body}");
            }

            return ApplyPublicAccessBlock(bucketName, usePathStyle);
        }

        if (head.StatusCode == 403)
        {
            return new S3ProvisionResult(false, "Access denied to bucket. It may exist in another account.");
        }

        if (head.StatusCode == 301)
        {
            return new S3ProvisionResult(false, "Bucket exists in a different region.");
        }

        return new S3ProvisionResult(false, $"Bucket check failed ({head.StatusCode}). {head.Body}");
    }

    private S3ProvisionResult ApplyPublicAccessBlock(string bucketName, bool usePathStyle)
    {
        string xml = "<PublicAccessBlockConfiguration xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">" +
                     "<BlockPublicAcls>false</BlockPublicAcls>" +
                     "<IgnorePublicAcls>false</IgnorePublicAcls>" +
                     "<BlockPublicPolicy>false</BlockPublicPolicy>" +
                     "<RestrictPublicBuckets>false</RestrictPublicBuckets>" +
                     "</PublicAccessBlockConfiguration>";

        byte[] payload = Encoding.UTF8.GetBytes(xml);
        AwsApiResponse response = SendBucketRequest(UploadersHttpMethod.PUT, bucketName, usePathStyle, "publicAccessBlock=", payload, "application/xml", allowNon2xx: true);

        if (response.IsSuccess || response.StatusCode == 200 || response.StatusCode == 204)
        {
            return new S3ProvisionResult(true, "Bucket ready for uploads.");
        }

        return new S3ProvisionResult(false, $"Failed to update public access block ({response.StatusCode}). {response.Body}");
    }

    private AwsApiResponse SendBucketRequest(UploadersHttpMethod method, string bucketName, bool usePathStyle, string queryString, byte[]? payload,
        string? contentType, bool allowNon2xx)
    {
        string host = usePathStyle ? DefaultEndpoint : $"{bucketName}.{DefaultEndpoint}";
        string canonicalUri = usePathStyle ? $"/{bucketName}" : "/";
        canonicalUri = URLHelpers.URLEncode(canonicalUri, true);

        string canonicalQueryString = queryString ?? string.Empty;

        NameValueCollection headers = new NameValueCollection
        {
            ["Host"] = host
        };

        string hashedPayload = "UNSIGNED-PAYLOAD";
        AwsS3Signer.Sign(headers, method.ToString(), canonicalUri, canonicalQueryString, DefaultRegion, _accessKeyId, _secretAccessKey, _sessionToken, hashedPayload);

        string url = $"{Scheme}{host}{canonicalUri}";
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            url += "?" + queryString;
        }

        return SendRawRequest(method, url, payload, contentType, headers, allowNon2xx);
    }
}

internal static class S3BucketNameValidator
{
    private static readonly Regex ValidPattern = new Regex("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$", RegexOptions.Compiled);
    private static readonly Regex IpAddressPattern = new Regex("^\\d+\\.\\d+\\.\\d+\\.\\d+$", RegexOptions.Compiled);

    public static bool IsValid(string bucketName)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            return false;
        }

        if (bucketName.Length < 3 || bucketName.Length > 63)
        {
            return false;
        }

        if (!ValidPattern.IsMatch(bucketName))
        {
            return false;
        }

        if (bucketName.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        if (IpAddressPattern.IsMatch(bucketName))
        {
            return false;
        }

        return true;
    }
}
