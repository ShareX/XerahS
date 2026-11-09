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
using NUnit.Framework;
using ShareX.AmazonS3.Plugin;
using ShareX.AmazonS3.Plugin.Multipart;
using XerahS.Uploaders.Multipart;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public class S3MultipartUploaderTests
{
    [Test]
    public void CreatePartRanges_SplitsFileIntoExpectedRanges()
    {
        long requestedPartSize = S3MultipartUploader.MinimumPartSizeBytes;
        long fileSize = requestedPartSize + 123;

        IReadOnlyList<PartRange> ranges = S3MultipartUploader.CreatePartRanges(fileSize, requestedPartSize, out long effectivePartSize);

        Assert.That(effectivePartSize, Is.EqualTo(requestedPartSize));
        Assert.That(ranges.Count, Is.EqualTo(2));
        Assert.That(ranges[0], Is.EqualTo(new PartRange(1, 0, requestedPartSize)));
        Assert.That(ranges[1], Is.EqualTo(new PartRange(2, requestedPartSize, 123)));
    }

    [Test]
    public void CreatePartRanges_AutoAdjustsPartSizeWhenLimitWouldBeExceeded()
    {
        long fileSize = (long)S3MultipartUploader.MaximumPartCount * S3MultipartUploader.MinimumPartSizeBytes + 1;

        IReadOnlyList<PartRange> ranges = S3MultipartUploader.CreatePartRanges(
            fileSize,
            S3MultipartUploader.MinimumPartSizeBytes,
            out long effectivePartSize);

        Assert.That(effectivePartSize, Is.GreaterThan(S3MultipartUploader.MinimumPartSizeBytes));
        Assert.That(ranges.Count, Is.LessThanOrEqualTo(S3MultipartUploader.MaximumPartCount));
        Assert.That(ranges[^1].Offset + ranges[^1].Length, Is.EqualTo(fileSize));
    }

    [Test]
    public async Task UploadAsync_UploadsPartsCompletesAndReportsProgress()
    {
        string filePath = CreateTempFile(S3MultipartUploader.MinimumPartSizeBytes + 1024);

        try
        {
            FakeAmazonS3Client client = new();
            S3MultipartUploader uploader = new(client);
            RecordingProgress progress = new();
            S3MultipartUploadOptions options = CreateOptions();

            MultipartUploadResult result = await uploader.UploadAsync(
                filePath,
                options,
                progress,
                CancellationToken.None);

            Assert.That(client.InitiateCalls, Is.EqualTo(1));
            Assert.That(client.UploadRequests.Count, Is.EqualTo(2));
            Assert.That(client.CompleteCalls, Is.EqualTo(1));
            Assert.That(client.AbortCalls, Is.EqualTo(0));
            Assert.That(client.CompletedPartNumbers, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.PartsUploaded, Is.EqualTo(2));
            Assert.That(result.Url, Is.EqualTo(options.URL));
            Assert.That(progress.Snapshots, Is.Not.Empty);
            Assert.That(progress.Snapshots[^1].BytesUploaded, Is.EqualTo(new FileInfo(filePath).Length));
            Assert.That(progress.Snapshots[^1].CompletedParts, Is.EqualTo(2));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public async Task UploadAsync_AbortsMultipartUploadWhenRetriesAreExhausted()
    {
        string filePath = CreateTempFile(S3MultipartUploader.MinimumPartSizeBytes);

        try
        {
            FakeAmazonS3Client client = new((_, _) => throw new HttpRequestException("simulated transient failure"));
            S3MultipartUploader uploader = new(client);
            S3MultipartUploadOptions options = CreateOptions();
            options.RetryPolicy = new XerahS.Uploaders.Multipart.RetryPolicy
            {
                MaxRetries = 1,
                BaseDelay = TimeSpan.FromMilliseconds(1),
                MaxDelay = TimeSpan.FromMilliseconds(1),
                JitterEnabled = false
            };

            MultipartUploadException? exception = await Assert.ThrowsAsync<MultipartUploadException>(async () =>
                await uploader.UploadAsync(filePath, options, cancellationToken: CancellationToken.None));

            Assert.That(exception!.UploadId, Is.EqualTo("upload-123"));
            Assert.That(client.UploadAttempts, Is.EqualTo(2));
            Assert.That(client.AbortCalls, Is.EqualTo(1));
            Assert.That(client.CompleteCalls, Is.EqualTo(0));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public void UploadAsync_AbortsMultipartUploadWhenCanceled()
    {
        string filePath = CreateTempFile(S3MultipartUploader.MinimumPartSizeBytes);

        try
        {
            FakeAmazonS3Client client = new(async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new UploadPartResponse();
            });
            S3MultipartUploader uploader = new(client);
            S3MultipartUploadOptions options = CreateOptions();
            using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromMilliseconds(50));

            Assert.That(async () =>
                await uploader.UploadAsync(filePath, options, cancellationToken: cancellationTokenSource.Token),
                Throws.InstanceOf<OperationCanceledException>());

            Assert.That(client.InitiateCalls, Is.EqualTo(1));
            Assert.That(client.CompleteCalls, Is.EqualTo(0));
            Assert.That(client.AbortCalls, Is.EqualTo(1));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static S3MultipartUploadOptions CreateOptions()
    {
        return new S3MultipartUploadOptions
        {
            BucketName = "xerahs-tests",
            ObjectKey = "uploads/test.bin",
            URL = "https://example.invalid/uploads/test.bin",
            PartSizeBytes = S3MultipartUploader.MinimumPartSizeBytes,
            MaxConcurrency = 2,
            StorageClass = ShareX.AmazonS3.Plugin.S3StorageClass.Standard
        };
    }

    private static string CreateTempFile(long length)
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");

        using FileStream stream = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.SetLength(length);

        return filePath;
    }

    private sealed class FakeAmazonS3Client : AmazonS3Client
    {
        private readonly Func<UploadPartRequest, CancellationToken, Task<UploadPartResponse>> _uploadPartHandler;

        public FakeAmazonS3Client(Func<UploadPartRequest, CancellationToken, Task<UploadPartResponse>>? uploadPartHandler = null)
            : base(
                new Amazon.Runtime.AnonymousAWSCredentials(),
                new AmazonS3Config
                {
                    ServiceURL = "https://example.invalid",
                    AuthenticationRegion = "us-east-1",
                    ForcePathStyle = true
                })
        {
            _uploadPartHandler = uploadPartHandler ?? DefaultUploadPartAsync;
        }

        public int InitiateCalls { get; private set; }

        public int UploadAttempts { get; private set; }

        public int CompleteCalls { get; private set; }

        public int AbortCalls { get; private set; }

        public List<(int PartNumber, long PartSize, string FilePath, long FilePosition)> UploadRequests { get; } = new();

        public List<int> CompletedPartNumbers { get; } = new();

        public override Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(
            InitiateMultipartUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            InitiateCalls++;

            return Task.FromResult(new InitiateMultipartUploadResponse
            {
                UploadId = "upload-123"
            });
        }

        public override async Task<UploadPartResponse> UploadPartAsync(
            UploadPartRequest request,
            CancellationToken cancellationToken = default)
        {
            UploadAttempts++;
            UploadRequests.Add((
                request.PartNumber.GetValueOrDefault(),
                request.PartSize.GetValueOrDefault(),
                request.FilePath ?? string.Empty,
                request.FilePosition.GetValueOrDefault()));

            return await _uploadPartHandler(request, cancellationToken);
        }

        public override Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(
            CompleteMultipartUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            CompleteCalls++;
            CompletedPartNumbers.AddRange(request.PartETags.Select(part => part.PartNumber.GetValueOrDefault()));

            return Task.FromResult(new CompleteMultipartUploadResponse
            {
                ETag = "complete-etag",
                VersionId = "version-1"
            });
        }

        public override Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(
            AbortMultipartUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            AbortCalls++;
            return Task.FromResult(new AbortMultipartUploadResponse());
        }

        private static Task<UploadPartResponse> DefaultUploadPartAsync(
            UploadPartRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new UploadPartResponse
            {
                ETag = $"etag-{request.PartNumber}"
            });
        }
    }

    private sealed class RecordingProgress : IProgress<MultipartUploadProgress>
    {
        public List<MultipartUploadProgress> Snapshots { get; } = new();

        public void Report(MultipartUploadProgress value)
        {
            Snapshots.Add(value);
        }
    }
}
