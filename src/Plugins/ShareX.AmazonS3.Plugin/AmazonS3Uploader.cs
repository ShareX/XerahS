#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
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

using XerahS.Common;
using XerahS.Uploaders;
using System.Collections.Specialized;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ShareX.AmazonS3.Plugin;

/// <summary>
/// Amazon S3 uploader - supports basic S3 uploads with AWS V4 signing and advanced features
/// </summary>
public class AmazonS3Uploader : FileUploader
{
    private const string DefaultRegion = "us-east-1";
    private readonly S3ConfigModel _config;

    public static List<AmazonS3Endpoint> Endpoints { get; } = new List<AmazonS3Endpoint>
    {
        new AmazonS3Endpoint("Asia Pacific (Hong Kong)", "s3.ap-east-1.amazonaws.com", "ap-east-1"),
        new AmazonS3Endpoint("Asia Pacific (Mumbai)", "s3.ap-south-1.amazonaws.com", "ap-south-1"),
        new AmazonS3Endpoint("Asia Pacific (Seoul)", "s3.ap-northeast-2.amazonaws.com", "ap-northeast-2"),
        new AmazonS3Endpoint("Asia Pacific (Singapore)", "s3.ap-southeast-1.amazonaws.com", "ap-southeast-1"),
        new AmazonS3Endpoint("Asia Pacific (Sydney)", "s3.ap-southeast-2.amazonaws.com", "ap-southeast-2"),
        new AmazonS3Endpoint("Asia Pacific (Tokyo)", "s3.ap-northeast-1.amazonaws.com", "ap-northeast-1"),
        new AmazonS3Endpoint("Canada (Central)", "s3.ca-central-1.amazonaws.com", "ca-central-1"),
        new AmazonS3Endpoint("China (Beijing)", "s3.cn-north-1.amazonaws.com.cn", "cn-north-1"),
        new AmazonS3Endpoint("China (Ningxia)", "s3.cn-northwest-1.amazonaws.com.cn", "cn-northwest-1"),
        new AmazonS3Endpoint("EU (Frankfurt)", "s3.eu-central-1.amazonaws.com", "eu-central-1"),
        new AmazonS3Endpoint("EU (Ireland)", "s3.eu-west-1.amazonaws.com", "eu-west-1"),
        new AmazonS3Endpoint("EU (London)", "s3.eu-west-2.amazonaws.com", "eu-west-2"),
        new AmazonS3Endpoint("EU (Paris)", "s3.eu-west-3.amazonaws.com", "eu-west-3"),
        new AmazonS3Endpoint("EU (Stockholm)", "s3.eu-north-1.amazonaws.com", "eu-north-1"),
        new AmazonS3Endpoint("Middle East (Bahrain)", "s3.me-south-1.amazonaws.com", "me-south-1"),
        new AmazonS3Endpoint("South America (São Paulo)", "s3.sa-east-1.amazonaws.com", "sa-east-1"),
        new AmazonS3Endpoint("US East (N. Virginia)", "s3.amazonaws.com", "us-east-1"),
        new AmazonS3Endpoint("US East (Ohio)", "s3.us-east-2.amazonaws.com", "us-east-2"),
        new AmazonS3Endpoint("US West (N. California)", "s3.us-west-1.amazonaws.com", "us-west-1"),
        new AmazonS3Endpoint("US West (Oregon)", "s3.us-west-2.amazonaws.com", "us-west-2"),
        new AmazonS3Endpoint("DreamObjects", "objects-us-east-1.dream.io"),
        new AmazonS3Endpoint("DigitalOcean (Amsterdam)", "ams3.digitaloceanspaces.com", "ams3"),
        new AmazonS3Endpoint("DigitalOcean (New York)", "nyc3.digitaloceanspaces.com", "nyc3"),
        new AmazonS3Endpoint("DigitalOcean (San Francisco)", "sfo2.digitaloceanspaces.com", "sfo2"),
        new AmazonS3Endpoint("DigitalOcean (Singapore)", "sgp1.digitaloceanspaces.com", "sgp1"),
        new AmazonS3Endpoint("Wasabi", "s3.wasabisys.com")
    };

    public AmazonS3Uploader(S3ConfigModel config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public override UploadResult Upload(Stream stream, string fileName)
    {
        bool isPathStyleRequest = _config.UsePathStyleUrl || _config.BucketName.Contains(".");

        string scheme = _config.Endpoint.StartsWith("http") ? "" : "https://";
        string endpoint = _config.Endpoint;

        string host = isPathStyleRequest
            ? endpoint
            : $"{_config.BucketName}.{endpoint}";

        string algorithm = "AWS4-HMAC-SHA256";
        string credentialDate = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string region = GetRegion();
        string scope = string.Join("/", credentialDate, region, "s3", "aws4_request");
        string credential = string.Join("/", _config.AccessKeyId, scope);
        string timeStamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        string contentType = MimeTypes.GetMimeTypeFromFileName(fileName);

        string hashedPayload;
        if (_config.SignedPayload)
        {
            hashedPayload = ComputeSHA256Hash(stream);
        }
        else
        {
            hashedPayload = "UNSIGNED-PAYLOAD";
        }

        string uploadPath = GetUploadPath(fileName);
        string resultURL = GenerateURL(uploadPath);

        OnEarlyURLCopyRequested(resultURL);

        var headers = new NameValueCollection
        {
            ["Host"] = host,
            ["Content-Length"] = stream.Length.ToString(CultureInfo.InvariantCulture),
            ["Content-Type"] = contentType,
            ["x-amz-date"] = timeStamp,
            ["x-amz-content-sha256"] = hashedPayload,
            ["x-amz-storage-class"] = _config.StorageClass.ToString().ToUpperInvariant()
        };

        if (_config.SetPublicACL)
        {
            headers["x-amz-acl"] = "public-read";
        }

        string canonicalURI = uploadPath;
        if (isPathStyleRequest) canonicalURI = URLHelpers.CombineURL(_config.BucketName, canonicalURI);
        canonicalURI = URLHelpers.AddSlash(canonicalURI, SlashType.Prefix);
        canonicalURI = URLHelpers.URLEncode(canonicalURI, true);

        string canonicalQueryString = "";
        string canonicalHeaders = CreateCanonicalHeaders(headers);
        string signedHeaders = GetSignedHeaders(headers);

        string canonicalRequest = $"PUT\n{canonicalURI}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{hashedPayload}";

        string stringToSign = $"{algorithm}\n{timeStamp}\n{scope}\n{BytesToHex(ComputeSHA256(canonicalRequest))}";

        byte[] dateKey = ComputeHMACSHA256(credentialDate, "AWS4" + _config.SecretAccessKey);
        byte[] dateRegionKey = ComputeHMACSHA256(region, dateKey);
        byte[] dateRegionServiceKey = ComputeHMACSHA256("s3", dateRegionKey);
        byte[] signingKey = ComputeHMACSHA256("aws4_request", dateRegionServiceKey);

        string signature = BytesToHex(ComputeHMACSHA256(stringToSign, signingKey));

        headers["Authorization"] = $"{algorithm} Credential={credential},SignedHeaders={signedHeaders},Signature={signature}";

        headers.Remove("Host");
        headers.Remove("Content-Type");

        string url = URLHelpers.CombineURL(scheme + host, canonicalURI);
        url = URLHelpers.FixPrefix(url);

        SendRequest(XerahS.Uploaders.HttpMethod.PUT, url, stream, contentType, null, headers);

        if (LastResponseInfo?.IsSuccess == true)
        {
            return new UploadResult
            {
                IsSuccess = true,
                URL = resultURL
            };
        }

        Errors.Add("Upload to Amazon S3 failed.");
        return null;
    }

    private string GetRegion()
    {
        if (!string.IsNullOrEmpty(_config.Region))
        {
            return _config.Region;
        }

        string url = _config.Endpoint;

        if (url.Contains("//"))
        {
            url = url.Split(new[] { "//" }, StringSplitOptions.None)[1];
        }

        if (url.EndsWith("/"))
        {
            url = url.Substring(0, url.Length - 1);
        }

        if (!url.Contains(".amazonaws.com"))
        {
            return DefaultRegion;
        }

        string serviceAndRegion = url.Split(new[] { ".amazonaws.com" }, StringSplitOptions.None)[0];
        if (serviceAndRegion.StartsWith("s3-"))
        {
            serviceAndRegion = "s3." + serviceAndRegion.Substring(3);
        }

        int separatorIndex = serviceAndRegion.LastIndexOf('.');
        if (separatorIndex == -1)
        {
            return DefaultRegion;
        }

        return serviceAndRegion.Substring(separatorIndex + 1);
    }

    private string GetUploadPath(string fileName)
    {
        string path = NameParser.Parse(NameParserType.FilePath, _config.ObjectPrefix).Trim('/');

        // Remove extension based on settings
        bool removeExt = false;
        if (_config.RemoveExtensionImage && IsImageFile(fileName)) removeExt = true;
        else if (_config.RemoveExtensionVideo && IsVideoFile(fileName)) removeExt = true;
        else if (_config.RemoveExtensionText && IsTextFile(fileName)) removeExt = true;

        if (removeExt)
        {
            fileName = System.IO.Path.GetFileNameWithoutExtension(fileName);
        }

        return URLHelpers.CombineURL(path, fileName);
    }

    private string GenerateURL(string uploadPath)
    {
        if (!string.IsNullOrEmpty(_config.Endpoint) && !string.IsNullOrEmpty(_config.BucketName))
        {
            uploadPath = URLHelpers.URLEncode(uploadPath, true);

            string url;

            if (_config.UseCustomCNAME && !string.IsNullOrEmpty(_config.CustomDomain))
            {
                ShareXCustomUploaderSyntaxParser parser = new ShareXCustomUploaderSyntaxParser();
                string parsedDomain = parser.Parse(_config.CustomDomain);
                url = URLHelpers.CombineURL(parsedDomain, uploadPath);
            }
            else
            {
                url = URLHelpers.CombineURL(_config.Endpoint, _config.BucketName, uploadPath);
            }

            return URLHelpers.FixPrefix(url);
        }

        return string.Empty;
    }

    private bool IsImageFile(string fileName)
    {
        string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".webp" }.Contains(ext);
    }

    private bool IsVideoFile(string fileName)
    {
        string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return new[] { ".mp4", ".avi", ".mov", ".mkv", ".flv", ".wmv", ".webm" }.Contains(ext);
    }

    private bool IsTextFile(string fileName)
    {
        string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return new[] { ".txt", ".log", ".json", ".xml", ".md", ".html", ".css", ".js" }.Contains(ext);
    }

    private string CreateCanonicalHeaders(NameValueCollection headers)
    {
        var sorted = headers.AllKeys.OrderBy(k => k).Select(k => $"{k.ToLowerInvariant()}:{headers[k].Trim()}\n");
        return string.Join("", sorted);
    }

    private string GetSignedHeaders(NameValueCollection headers)
    {
        return string.Join(";", headers.AllKeys.OrderBy(k => k).Select(k => k.ToLowerInvariant()));
    }

    private string ComputeSHA256Hash(Stream stream)
    {
        long position = stream.Position;
        stream.Seek(0, SeekOrigin.Begin);
        byte[] hash = SHA256.HashData(stream);
        stream.Seek(position, SeekOrigin.Begin);
        return BytesToHex(hash);
    }

    // Local crypto helper methods
    private static byte[] ComputeSHA256(string text)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(text));
    }

    private static byte[] ComputeHMACSHA256(string text, string key)
    {
        return ComputeHMACSHA256(text, Encoding.UTF8.GetBytes(key));
    }

    private static byte[] ComputeHMACSHA256(string text, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(text));
    }

    private static string BytesToHex(byte[] bytes)
    {
        return Convert.ToHexStringLower(bytes);
    }
}

public class AmazonS3Endpoint
{
    public string Name { get; set; }
    public string Endpoint { get; set; }
    public string Region { get; set; }

    public AmazonS3Endpoint(string name, string endpoint, string region = "")
    {
        Name = name;
        Endpoint = endpoint;
        Region = region;
    }
}
