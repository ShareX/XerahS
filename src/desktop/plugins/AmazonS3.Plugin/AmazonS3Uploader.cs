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

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ShareX.AmazonS3.Plugin.Multipart;
using XerahS.Common;
using XerahS.Uploaders;
using XerahS.Uploaders.Multipart;

namespace ShareX.AmazonS3.Plugin;

/// <summary>
/// Amazon S3 uploader - supports basic S3 uploads with AWS V4 signing and multipart uploads for large files.
/// </summary>
public class AmazonS3Uploader : FileUploader
{
    private const string DefaultRegion = "us-east-1";
    private const long MaximumSinglePutSizeBytes = 5L * 1024 * 1024 * 1024;
    private readonly S3ConfigModel _config;
    private readonly string _accessKeyId;
    private readonly string _secretAccessKey;
    private readonly string? _sessionToken;
    private readonly Func<IAmazonS3> _s3ClientFactory;
    private CancellationTokenSource? _sdkCancellationTokenSource;

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
        new AmazonS3Endpoint("South America (Sao Paulo)", "s3.sa-east-1.amazonaws.com", "sa-east-1"),
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

    public AmazonS3Uploader(S3ConfigModel config, string accessKeyId, string secretAccessKey, string? sessionToken = null)
        : this(config, accessKeyId, secretAccessKey, sessionToken, null)
    {
    }

    internal AmazonS3Uploader(S3ConfigModel config, string accessKeyId, string secretAccessKey,
        string? sessionToken, Func<IAmazonS3>? s3ClientFactory)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _accessKeyId = accessKeyId ?? throw new ArgumentNullException(nameof(accessKeyId));
        _secretAccessKey = secretAccessKey ?? throw new ArgumentNullException(nameof(secretAccessKey));
        _sessionToken = sessionToken;
        _s3ClientFactory = s3ClientFactory ?? CreateConfiguredS3Client;
    }

    public override UploadResult? UploadFile(string filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            FileInfo fileInfo = new FileInfo(filePath);
            if (ShouldUseMultipart(fileInfo.Length))
            {
                return UploadMultipart(filePath, Path.GetFileName(filePath));
            }
        }

        return base.UploadFile(filePath);
    }

    public override void StopUpload()
    {
        base.StopUpload();

        try
        {
            _sdkCancellationTokenSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    internal const string MissingBucketMessage =
        "Amazon S3: no bucket is set for this destination. Set one in Destinations, or remove the destination.";

    public override UploadResult Upload(Stream stream, string fileName)
    {
        // Without a bucket the SDK throws ArgumentException before any request; say why instead.
        if (string.IsNullOrWhiteSpace(_config.BucketName))
        {
            DebugHelper.WriteLine(MissingBucketMessage);
            Errors.Add(MissingBucketMessage);
            return new UploadResult { Response = MissingBucketMessage };
        }

        if (stream is FileStream fileStream && File.Exists(fileStream.Name) && ShouldUseMultipart(stream.Length))
        {
            return UploadMultipart(fileStream.Name, fileName);
        }

        return UploadSinglePut(stream, fileName);
    }

    private UploadResult UploadSinglePut(Stream stream, string fileName)
    {
        (string uploadPath, string resultUrl, string contentType) = CreateUploadContext(fileName);
        OnEarlyURLCopyRequested(resultUrl);

        IsUploading = true;
        StopUploadRequested = false;

        using CancellationTokenSource sdkCancellationTokenSource = new();
        _sdkCancellationTokenSource = sdkCancellationTokenSource;
        ProgressManager progressManager = new(stream.Length);

        try
        {
            using IAmazonS3 client = CreateS3Client();
            PutObjectRequest request = new()
            {
                BucketName = _config.BucketName,
                Key = uploadPath,
                InputStream = stream,
                AutoCloseStream = false,
                AutoResetStreamPosition = false,
                ContentType = contentType,
                StorageClass = MapStorageClass(_config.StorageClass),
                DisablePayloadSigning = !_config.SignedPayload
            };

            if (_config.SetPublicACL)
            {
                request.CannedACL = S3CannedACL.PublicRead;
            }

            request.StreamTransferProgress += (_, args) =>
            {
                if (args.IncrementTransferred > 0 && AllowReportProgress && progressManager.UpdateProgress(args.IncrementTransferred))
                {
                    OnProgressChanged(progressManager);
                }
            };

            PutObjectResponse response = Task.Run(
                () => client.PutObjectAsync(request, sdkCancellationTokenSource.Token),
                sdkCancellationTokenSource.Token).GetAwaiter().GetResult();

            if ((int)response.HttpStatusCode is >= 200 and < 300)
            {
                var putResult = new UploadResult
                {
                    IsSuccess = true,
                    Response = response.ETag,
                    URL = resultUrl
                };
                AddObjectMetadata(putResult, uploadPath);
                return putResult;
            }

            string responseMessage = $"Upload to Amazon S3 failed ({(int)response.HttpStatusCode}).";
            Errors.Add(responseMessage);
            return new UploadResult { Response = responseMessage };
        }
        catch (OperationCanceledException)
        {
            const string cancellationMessage = "Amazon S3 upload was canceled.";
            DebugHelper.WriteLine(cancellationMessage);
            return new UploadResult { Response = cancellationMessage };
        }
        catch (AmazonS3Exception ex)
        {
            string failureMessage = string.IsNullOrWhiteSpace(ex.ErrorCode)
                ? $"Upload to Amazon S3 failed ({(int)ex.StatusCode})."
                : $"Upload to Amazon S3 failed ({(int)ex.StatusCode}, {ex.ErrorCode}).";
            DebugHelper.WriteLine(failureMessage);
            Errors.Add(failureMessage);
            return new UploadResult { Response = failureMessage };
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Upload to Amazon S3 failed ({ex.GetType().Name}).");
            const string failureMessage = "Upload to Amazon S3 failed.";
            Errors.Add(failureMessage);
            return new UploadResult { Response = failureMessage };
        }
        finally
        {
            _sdkCancellationTokenSource = null;
            IsUploading = false;
        }
    }

    private UploadResult UploadMultipart(string filePath, string fileName)
    {
        UploadResult result = new UploadResult();

        if (!File.Exists(filePath))
        {
            result.Response = $"Upload file not found: {filePath}";
            Errors.Add(result.Response);
            return result;
        }

        FileInfo fileInfo = new FileInfo(filePath);
        if (!ShouldUseMultipart(fileInfo.Length))
        {
            using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return UploadSinglePut(stream, fileName);
        }

        (string uploadPath, string resultUrl, string contentType) = CreateUploadContext(fileName);
        OnEarlyURLCopyRequested(resultUrl);

        IsUploading = true;
        StopUploadRequested = false;

        using CancellationTokenSource multipartCancellationTokenSource = new CancellationTokenSource();
        _sdkCancellationTokenSource = multipartCancellationTokenSource;

        ProgressManager progressManager = new ProgressManager(fileInfo.Length);
        long reportedBytes = 0;
        Lock progressSync = new();

        try
        {
            using IAmazonS3 client = CreateS3Client();

            S3MultipartUploadOptions options = new S3MultipartUploadOptions
            {
                BucketName = _config.BucketName,
                ObjectKey = uploadPath,
                URL = resultUrl,
                ContentType = contentType,
                PartSizeBytes = _config.MultipartPartSizeBytes,
                MaxConcurrency = _config.MultipartMaxConcurrency,
                RetryPolicy = new XerahS.Uploaders.Multipart.RetryPolicy(),
                StorageClass = _config.StorageClass,
                SetPublicAcl = _config.SetPublicACL
            };

            options.Validate();

            InlineProgress<MultipartUploadProgress> progressReporter = new(snapshot =>
            {
                long delta;

                lock (progressSync)
                {
                    delta = snapshot.BytesUploaded - reportedBytes;
                    reportedBytes = snapshot.BytesUploaded;

                    if (delta > 0 && AllowReportProgress && progressManager.UpdateProgress(delta))
                    {
                        OnProgressChanged(progressManager);
                    }
                }
            });

            MultipartUploadResult multipartResult = Task.Run(
                () => new S3MultipartUploader(client).UploadAsync(filePath, options, progressReporter, multipartCancellationTokenSource.Token),
                multipartCancellationTokenSource.Token).GetAwaiter().GetResult();

            var multipartUploadResult = new UploadResult
            {
                IsSuccess = multipartResult.IsSuccess,
                Response = multipartResult.ETag,
                URL = multipartResult.URL ?? resultUrl
            };
            if (multipartResult.IsSuccess)
            {
                AddObjectMetadata(multipartUploadResult, uploadPath);
            }

            return multipartUploadResult;
        }
        catch (OperationCanceledException)
        {
            result.Response = "Amazon S3 multipart upload was canceled.";
            DebugHelper.WriteLine(result.Response);
            return result;
        }
        catch (MultipartUploadException ex)
        {
            DebugHelper.WriteLine("Amazon S3 multipart upload failed after retries.");
            Errors.Add(ex.Message);
            result.Response = ex.Message;
            return result;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"Amazon S3 multipart upload failed ({ex.GetType().Name}).");
            Errors.Add(ex.Message);
            result.Response = ex.Message;
            return result;
        }
        finally
        {
            _sdkCancellationTokenSource = null;
            IsUploading = false;
        }
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

        bool removeExt = false;
        if (_config.RemoveExtensionImage && IsImageFile(fileName)) removeExt = true;
        else if (_config.RemoveExtensionVideo && IsVideoFile(fileName)) removeExt = true;
        else if (_config.RemoveExtensionText && IsTextFile(fileName)) removeExt = true;

        if (removeExt)
        {
            fileName = Path.GetFileNameWithoutExtension(fileName);
        }

        return URLHelpers.CombineURL(path, fileName);
    }

    private (string UploadPath, string ResultUrl, string ContentType) CreateUploadContext(string fileName)
    {
        string uploadPath = GetUploadPath(fileName);
        return (uploadPath, GenerateURL(uploadPath), MimeTypes.GetMimeTypeFromFileName(fileName));
    }

    /// <summary>Upload result metadata key holding the S3 object key (used by "Delete from host").</summary>
    internal const string ObjectKeyMetadata = "S3Key";
    internal const string BucketMetadata = "S3Bucket";

    private void AddObjectMetadata(UploadResult result, string objectKey)
    {
        result.Metadata[ObjectKeyMetadata] = objectKey;
        result.Metadata[BucketMetadata] = _config.BucketName;
    }

    /// <summary>
    /// Recovers the object key from a URL this uploader generated (endpoint/bucket or custom domain form).
    /// </summary>
    internal bool TryGetObjectKey(string url, out string objectKey)
    {
        objectKey = string.Empty;
        const string marker = "xerahs-object-key-marker";
        string template = GenerateURL(marker);
        int markerIndex = template.IndexOf(marker, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(url) || markerIndex < 0)
        {
            return false;
        }

        string prefix = template[..markerIndex];
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || url.Length == prefix.Length)
        {
            return false;
        }

        string encodedKey = url[prefix.Length..];
        int query = encodedKey.IndexOfAny(['?', '#']);
        if (query >= 0)
        {
            encodedKey = encodedKey[..query];
        }

        objectKey = Uri.UnescapeDataString(encodedKey);
        return objectKey.Length > 0;
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
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".webp" }.Contains(ext);
    }

    private bool IsVideoFile(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return new[] { ".mp4", ".avi", ".mov", ".mkv", ".flv", ".wmv", ".webm" }.Contains(ext);
    }

    private bool IsTextFile(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return new[] { ".txt", ".log", ".json", ".xml", ".md", ".html", ".css", ".js" }.Contains(ext);
    }

    internal bool ShouldUseMultipart(long streamLength)
    {
        long threshold = Math.Max(0, _config.MultipartThresholdBytes);
        return streamLength > 0 &&
            (streamLength > MaximumSinglePutSizeBytes || streamLength >= threshold);
    }

    private IAmazonS3 CreateS3Client()
    {
        return _s3ClientFactory();
    }

    /// <summary>A client configured like the uploader's, for Media Browser operations.</summary>
    internal IAmazonS3 CreateClient() => CreateS3Client();

    private IAmazonS3 CreateConfiguredS3Client()
    {
        AWSCredentials credentials = string.IsNullOrWhiteSpace(_sessionToken)
            ? new BasicAWSCredentials(_accessKeyId, _secretAccessKey)
            : new SessionAWSCredentials(_accessKeyId, _secretAccessKey, _sessionToken);

        AmazonS3Config config = new AmazonS3Config
        {
            ServiceURL = GetServiceUrl(),
            AuthenticationRegion = GetRegion(),
            ForcePathStyle = _config.UsePathStyleUrl || _config.BucketName.Contains("."),
            UseHttp = _config.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        };

        return new AmazonS3Client(credentials, config);
    }

    private string GetServiceUrl()
    {
        string endpoint = _config.Endpoint.Trim().TrimEnd('/');

        if (endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return endpoint;
        }

        return "https://" + endpoint;
    }

    internal static Amazon.S3.S3StorageClass MapStorageClass(S3StorageClass storageClass)
    {
        return storageClass switch
        {
            S3StorageClass.Standard => Amazon.S3.S3StorageClass.Standard,
            S3StorageClass.StandardInfrequentAccess => Amazon.S3.S3StorageClass.StandardInfrequentAccess,
            S3StorageClass.OneZoneInfrequentAccess => Amazon.S3.S3StorageClass.OneZoneInfrequentAccess,
            S3StorageClass.Glacier => Amazon.S3.S3StorageClass.Glacier,
            S3StorageClass.DeepArchive => Amazon.S3.S3StorageClass.DeepArchive,
            _ => Amazon.S3.S3StorageClass.Standard
        };
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public InlineProgress(Action<T> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Report(T value)
        {
            _handler(value);
        }
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
