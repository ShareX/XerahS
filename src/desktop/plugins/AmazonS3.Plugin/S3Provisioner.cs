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
    private readonly string _region;
    private readonly string _endpoint;

    public S3Provisioner(string accessKeyId, string secretAccessKey, string? sessionToken, string region, string endpoint)
    {
        _accessKeyId = accessKeyId;
        _secretAccessKey = secretAccessKey;
        _sessionToken = sessionToken;
        _endpoint = NormalizeEndpoint(endpoint);
        _region = ResolveRegion(region, _endpoint);
    }

    public S3ProvisionResult EnsureBucket(string bucketName, bool applyPublicPolicy)
    {
        if (!S3BucketNameValidator.IsValid(bucketName))
        {
            return new S3ProvisionResult(false, "Bucket name is invalid.");
        }

        bool usePathStyle = bucketName.Contains(".");
        AwsApiResponse head = SendBucketRequest(UploadersHttpMethod.HEAD, bucketName, usePathStyle, "", null, null, allowNon2xx: true);

        if (head.StatusCode == 200)
        {
            return ApplyPublicSettings(bucketName, usePathStyle, applyPublicPolicy);
        }

        if (head.StatusCode == 404)
        {
            byte[]? payload = null;
            string? contentType = null;

            if (!string.Equals(_region, DefaultRegion, StringComparison.OrdinalIgnoreCase))
            {
                string xml = "<CreateBucketConfiguration xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">" +
                             "<LocationConstraint>" + _region + "</LocationConstraint>" +
                             "</CreateBucketConfiguration>";
                payload = Encoding.UTF8.GetBytes(xml);
                contentType = "application/xml";
            }

            AwsApiResponse create = SendBucketRequest(UploadersHttpMethod.PUT, bucketName, usePathStyle, "", payload, contentType, allowNon2xx: true);
            if (!create.IsSuccess && create.StatusCode != 200 && create.StatusCode != 204)
            {
                if (create.StatusCode == 409)
                {
                    AwsApiResponse confirm = SendBucketRequest(UploadersHttpMethod.HEAD, bucketName, usePathStyle, "", null, null, allowNon2xx: true);
                    if (confirm.StatusCode == 200)
                    {
                        return ApplyPublicSettings(bucketName, usePathStyle, applyPublicPolicy);
                    }
                }

                return new S3ProvisionResult(false, $"Bucket creation failed ({create.StatusCode}). {create.Body}");
            }

            return ApplyPublicSettings(bucketName, usePathStyle, applyPublicPolicy);
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

    private S3ProvisionResult ApplyPublicSettings(string bucketName, bool usePathStyle, bool applyPublicPolicy)
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
            if (!applyPublicPolicy)
            {
                return new S3ProvisionResult(true, "Bucket ready for uploads.");
            }

            return ApplyPublicReadPolicy(bucketName, usePathStyle);
        }

        return new S3ProvisionResult(false, $"Failed to update public access block ({response.StatusCode}). {response.Body}");
    }

    private S3ProvisionResult ApplyPublicReadPolicy(string bucketName, bool usePathStyle)
    {
        string policyJson = "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Sid\":\"AllowPublicRead\",\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"s3:GetObject\",\"Resource\":\"arn:aws:s3:::" +
                            bucketName + "/*\"}]}";

        byte[] payload = Encoding.UTF8.GetBytes(policyJson);
        AwsApiResponse response = SendBucketRequest(UploadersHttpMethod.PUT, bucketName, usePathStyle, "policy=", payload, "application/json", allowNon2xx: true);

        if (response.IsSuccess || response.StatusCode == 200 || response.StatusCode == 204)
        {
            return new S3ProvisionResult(true, "Bucket ready for uploads.");
        }

        return new S3ProvisionResult(false, $"Failed to set public bucket policy ({response.StatusCode}). {response.Body}");
    }

    private AwsApiResponse SendBucketRequest(UploadersHttpMethod method, string bucketName, bool usePathStyle, string queryString, byte[]? payload,
        string? contentType, bool allowNon2xx)
    {
        string host = usePathStyle ? _endpoint : $"{bucketName}.{_endpoint}";
        string canonicalUri = usePathStyle ? $"/{bucketName}" : "/";
        canonicalUri = URLHelpers.URLEncode(canonicalUri, true);

        string canonicalQueryString = queryString ?? string.Empty;

        NameValueCollection headers = new NameValueCollection
        {
            ["Host"] = host
        };

        string hashedPayload = "UNSIGNED-PAYLOAD";
        AwsS3Signer.Sign(headers, method.ToString(), canonicalUri, canonicalQueryString, _region, _accessKeyId, _secretAccessKey, _sessionToken, hashedPayload);

        string url = $"{Scheme}{host}{canonicalUri}";
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            url += "?" + queryString;
        }

        return SendRawRequest(method, url, payload, contentType, headers, allowNon2xx);
    }

    private static string ResolveRegion(string region, string endpoint)
    {
        if (!string.IsNullOrWhiteSpace(region))
        {
            return region;
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return DefaultRegion;
        }

        string host = endpoint;
        if (host.Contains(".amazonaws.com", StringComparison.OrdinalIgnoreCase))
        {
            string serviceAndRegion = host.Split(new[] { ".amazonaws.com" }, StringSplitOptions.None)[0];
            if (serviceAndRegion.StartsWith("s3-"))
            {
                serviceAndRegion = "s3." + serviceAndRegion.Substring(3);
            }

            int separatorIndex = serviceAndRegion.LastIndexOf('.');
            if (separatorIndex != -1)
            {
                return serviceAndRegion.Substring(separatorIndex + 1);
            }
        }

        return DefaultRegion;
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return DefaultEndpoint;
        }

        string value = endpoint.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                return uri.Host;
            }

            value = value.Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("http://", "", StringComparison.OrdinalIgnoreCase);
        }

        return value.TrimEnd('/');
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
