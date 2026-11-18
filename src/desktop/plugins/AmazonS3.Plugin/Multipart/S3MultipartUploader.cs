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

using Amazon.S3;
using Amazon.S3.Model;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Runtime.ExceptionServices;
using XerahS.Common;
using XerahS.Uploaders.Multipart;

namespace ShareX.AmazonS3.Plugin.Multipart;

public sealed class S3MultipartUploader : IMultipartUploader
{
    public const long MinimumPartSizeBytes = 5L * 1024 * 1024;
    public const long MaximumPartSizeBytes = 5L * 1024 * 1024 * 1024;
    public const int MaximumPartCount = 10_000;

    private readonly IAmazonS3 _s3Client;

    public S3MultipartUploader(IAmazonS3 s3Client)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
    }

    public async Task<MultipartUploadResult> UploadAsync(
        string filePath,
        MultipartUploadOptions options,
        IProgress<MultipartUploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(options);

        if (options is not S3MultipartUploadOptions s3Options)
        {
            throw new ArgumentException("S3 multipart uploads require S3MultipartUploadOptions.", nameof(options));
        }

        ValidateInputs(filePath, s3Options);

        FileInfo fileInfo = new(filePath);
        IReadOnlyList<PartRange> partRanges = CreatePartRanges(fileInfo.Length, s3Options.PartSizeBytes, out long effectivePartSizeBytes);
        Stopwatch stopwatch = Stopwatch.StartNew();
        ConcurrentDictionary<int, CompletedPart> completedParts = new();
        Dictionary<int, long> activePartBytes = new();
        Lock progressSync = new();
        long committedBytes = 0;
        long reportedBytes = 0;
        int committedPartCount = 0;
        string? uploadId = null;
        bool uploadCompleted = false;
        Exception? firstFailure = null;

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        void WriteLog(string message)
        {
            DebugHelper.WriteLine($"[S3Multipart] {message}");
        }

        void ReportProgressSnapshot()
        {
            if (progress == null)
            {
                return;
            }

            long currentBytes;
            int currentCompleted;

            lock (progressSync)
            {
                long activeBytes = activePartBytes.Values.Sum();
                long candidate = committedBytes + activeBytes;
                if (candidate < reportedBytes)
                {
                    candidate = reportedBytes;
                }
                else
                {
                    reportedBytes = candidate;
                }

                currentBytes = Math.Min(candidate, fileInfo.Length);
                currentCompleted = committedPartCount;
            }

            progress.Report(new MultipartUploadProgress(
                currentBytes,
                fileInfo.Length,
                currentCompleted,
                partRanges.Count,
                stopwatch.Elapsed));
        }

        void ResetPartProgress(int partNumber)
        {
            if (progress == null)
            {
                return;
            }

            lock (progressSync)
            {
                activePartBytes[partNumber] = 0;
            }

            ReportProgressSnapshot();
        }

        void UpdatePartProgress(int partNumber, long transferredBytes)
        {
            if (progress == null)
            {
                return;
            }

            lock (progressSync)
            {
                activePartBytes[partNumber] = Math.Max(0, transferredBytes);
            }

            ReportProgressSnapshot();
        }

        void MarkPartCompleted(PartRange range)
        {
            lock (progressSync)
            {
                activePartBytes.Remove(range.PartNumber);
                committedBytes += range.Length;
                committedPartCount++;
                reportedBytes = Math.Max(reportedBytes, committedBytes);
            }

            ReportProgressSnapshot();
        }

        async Task<UploadPartResponse> UploadPartOnceAsync(PartRange range, CancellationToken token)
        {
            UploadPartRequest request = new()
            {
                BucketName = s3Options.BucketName,
                Key = s3Options.ObjectKey,
                UploadId = uploadId,
                PartNumber = range.PartNumber,
                FilePath = filePath,
                FilePosition = range.Offset,
                PartSize = range.Length
            };

            request.StreamTransferProgress += (_, args) =>
            {
                UpdatePartProgress(range.PartNumber, Math.Min(range.Length, args.TransferredBytes));
            };

            return await _s3Client.UploadPartAsync(request, token);
        }

        async Task UploadRangeAsync(PartRange range, CancellationToken token)
        {
            ResetPartProgress(range.PartNumber);

            for (int attempt = 0; ; attempt++)
            {
                token.ThrowIfCancellationRequested();
                WriteLog($"Uploading part {range.PartNumber}/{partRanges.Count} (length={range.Length}, attempt={attempt + 1}).");

                try
                {
                    UploadPartResponse response = await UploadPartOnceAsync(range, token);
                    completedParts[range.PartNumber] = new CompletedPart(range.PartNumber, response.ETag ?? string.Empty);
                    MarkPartCompleted(range);
                    WriteLog($"Part {range.PartNumber}/{partRanges.Count} succeeded.");
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < s3Options.RetryPolicy.MaxRetries && IsRetryable(ex, token))
                {
                    ResetPartProgress(range.PartNumber);
                    TimeSpan delay = s3Options.RetryPolicy.GetDelay(attempt + 1);
                    WriteLog($"Part {range.PartNumber}/{partRanges.Count} failed ({ex.GetType().Name}). Retrying in {delay.TotalSeconds:F1}s.");
                    await Task.Delay(delay, token);
                }
                catch
                {
                    ResetPartProgress(range.PartNumber);
                    throw;
                }
            }
        }

        try
        {
            if (effectivePartSizeBytes != s3Options.PartSizeBytes)
            {
                WriteLog($"Adjusted part size from {s3Options.PartSizeBytes} bytes to {effectivePartSizeBytes} bytes to stay within the {MaximumPartCount:N0}-part S3 limit.");
            }

            WriteLog($"Starting multipart upload ({fileInfo.Length} bytes, {partRanges.Count} parts, concurrency={s3Options.MaxConcurrency}).");

            InitiateMultipartUploadRequest initiateRequest = BuildInitiateRequest(s3Options);
            InitiateMultipartUploadResponse initiateResponse = await _s3Client.InitiateMultipartUploadAsync(initiateRequest, cancellationToken);
            uploadId = initiateResponse.UploadId;

            if (string.IsNullOrWhiteSpace(uploadId))
            {
                throw new InvalidOperationException("S3 did not return an upload ID for the multipart upload.");
            }

            ReportProgressSnapshot();

            using SemaphoreSlim semaphore = new(s3Options.MaxConcurrency);
            Task[] uploadTasks = partRanges.Select(async range =>
            {
                await semaphore.WaitAsync(linkedCts.Token);

                try
                {
                    await UploadRangeAsync(range, linkedCts.Token);
                }
                catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    lock (progressSync)
                    {
                        firstFailure ??= ex;
                    }

                    linkedCts.Cancel();
                    throw;
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            try
            {
                await Task.WhenAll(uploadTasks);
            }
            catch when (firstFailure != null)
            {
                ExceptionDispatchInfo.Capture(firstFailure).Throw();
            }

            cancellationToken.ThrowIfCancellationRequested();

            List<CompletedPart> orderedParts = completedParts.Values
                .OrderBy(part => part.PartNumber)
                .ToList();

            if (orderedParts.Count != partRanges.Count)
            {
                throw new MultipartUploadException(
                    "Multipart upload finished without completing every part.",
                    uploadId,
                    orderedParts);
            }

            CompleteMultipartUploadRequest completeRequest = new()
            {
                BucketName = s3Options.BucketName,
                Key = s3Options.ObjectKey,
                UploadId = uploadId,
                PartETags = orderedParts
                    .Select(part => new PartETag(part.PartNumber, part.ETag))
                    .ToList()
            };

            CompleteMultipartUploadResponse completeResponse = await CompleteWithRetryAsync(
                completeRequest,
                s3Options.RetryPolicy,
                cancellationToken);

            uploadCompleted = true;
            ReportProgressSnapshot();
            WriteLog("Multipart upload completed.");

            return new MultipartUploadResult
            {
                IsSuccess = true,
                ETag = completeResponse.ETag,
                VersionId = completeResponse.VersionId,
                URL = s3Options.URL,
                Elapsed = stopwatch.Elapsed,
                CompletedParts = orderedParts
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            WriteLog("Multipart upload cancelled.");
            throw;
        }
        catch (MultipartUploadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            List<CompletedPart> completedSnapshot = completedParts.Values
                .OrderBy(part => part.PartNumber)
                .ToList();

            throw new MultipartUploadException(
                $"Multipart upload failed for '{s3Options.ObjectKey}': {ex.Message}",
                uploadId,
                completedSnapshot,
                ex);
        }
        finally
        {
            if (!uploadCompleted && !string.IsNullOrWhiteSpace(uploadId))
            {
                try
                {
                    WriteLog("Aborting incomplete multipart upload.");
                    await _s3Client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
                    {
                        BucketName = s3Options.BucketName,
                        Key = s3Options.ObjectKey,
                        UploadId = uploadId
                    }, CancellationToken.None);
                }
                catch (Exception abortEx)
                {
                    WriteLog($"Abort failed ({abortEx.GetType().Name}).");
                }
            }
        }
    }

    public static IReadOnlyList<PartRange> CreatePartRanges(
        long fileSize,
        long requestedPartSizeBytes,
        out long effectivePartSizeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(fileSize, 0);

        if (requestedPartSizeBytes < MinimumPartSizeBytes || requestedPartSizeBytes > MaximumPartSizeBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedPartSizeBytes),
                $"S3 multipart part size must be between {MinimumPartSizeBytes} and {MaximumPartSizeBytes} bytes.");
        }

        effectivePartSizeBytes = requestedPartSizeBytes;
        long partCount = DivideRoundUp(fileSize, effectivePartSizeBytes);

        if (partCount > MaximumPartCount)
        {
            effectivePartSizeBytes = DivideRoundUp(fileSize, MaximumPartCount);
            effectivePartSizeBytes = Math.Max(effectivePartSizeBytes, MinimumPartSizeBytes);

            if (effectivePartSizeBytes > MaximumPartSizeBytes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestedPartSizeBytes),
                    "The requested file size cannot be represented within S3 multipart upload limits.");
            }

            partCount = DivideRoundUp(fileSize, effectivePartSizeBytes);
        }

        if (partCount > MaximumPartCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileSize),
                "The multipart upload would exceed the 10,000 part S3 limit.");
        }

        List<PartRange> ranges = new((int)partCount);
        long offset = 0;

        for (int partNumber = 1; partNumber <= partCount; partNumber++)
        {
            long length = Math.Min(effectivePartSizeBytes, fileSize - offset);
            ranges.Add(new PartRange(partNumber, offset, length));
            offset += length;
        }

        return ranges;
    }

    private static void ValidateInputs(string filePath, S3MultipartUploadOptions options)
    {
        options.Validate();

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Multipart upload file does not exist.", filePath);
        }

        if (string.IsNullOrWhiteSpace(options.BucketName))
        {
            throw new ArgumentException("S3 bucket name is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.ObjectKey))
        {
            throw new ArgumentException("S3 object key is required.", nameof(options));
        }

        FileInfo fileInfo = new(filePath);
        if (fileInfo.Length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(filePath), "Multipart upload requires a non-empty file.");
        }
    }

    private static InitiateMultipartUploadRequest BuildInitiateRequest(S3MultipartUploadOptions options)
    {
        InitiateMultipartUploadRequest request = new()
        {
            BucketName = options.BucketName,
            Key = options.ObjectKey,
            ContentType = options.ContentType,
            StorageClass = MapStorageClass(options.StorageClass)
        };

        if (options.SetPublicAcl)
        {
            request.CannedACL = S3CannedACL.PublicRead;
        }

        foreach ((string key, string value) in options.Metadata)
        {
            request.Metadata[key] = value;
        }

        if (options.Tags.Count > 0)
        {
            request.TagSet = options.Tags
                .Select(pair => new Tag { Key = pair.Key, Value = pair.Value })
                .ToList();
        }

        return request;
    }

    private async Task<CompleteMultipartUploadResponse> CompleteWithRetryAsync(
        CompleteMultipartUploadRequest request,
        RetryPolicy retryPolicy,
        CancellationToken cancellationToken)
    {
        const int completionMaxAttempts = 2;

        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await _s3Client.CompleteMultipartUploadAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt + 1 < completionMaxAttempts && IsRetryable(ex, cancellationToken))
            {
                TimeSpan delay = retryPolicy.GetDelay(attempt + 1);
                DebugHelper.WriteLine($"[S3Multipart] CompleteMultipartUpload failed ({ex.GetType().Name}). Retrying in {delay.TotalSeconds:F1}s.");
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static bool IsRetryable(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            return !cancellationToken.IsCancellationRequested;
        }

        if (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            return true;
        }

        if (exception is AmazonS3Exception s3Exception)
        {
            return s3Exception.StatusCode is HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                or HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;
        }

        return false;
    }

    private static long DivideRoundUp(long dividend, long divisor)
    {
        return (dividend + divisor - 1) / divisor;
    }

    private static Amazon.S3.S3StorageClass MapStorageClass(S3StorageClass storageClass)
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
}
