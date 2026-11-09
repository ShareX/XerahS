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

using System.Reflection;
using Amazon.S3;
using Amazon.S3.Model;
using NUnit.Framework;
using ShareX.AmazonS3.Plugin;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public class S3ExplorerOperationsTests
{
    /// <summary>An in-memory bucket behind <see cref="IAmazonS3"/>, implementing the calls the browser makes.</summary>
    public class FakeS3 : DispatchProxy
    {
        public SortedDictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);
        public List<string> Calls { get; } = [];
        public Func<string, bool>? FailCopyTo { get; set; }
        public Func<string, bool>? FailDeleteOf { get; set; }
        public int MaxDeleteBatch { get; private set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case nameof(IAmazonS3.ListObjectsV2Async):
                {
                    var request = (ListObjectsV2Request)args![0]!;
                    int start = int.TryParse(request.ContinuationToken, out int s) ? s : 0;
                    var matching = Objects.Keys.Where(k => k.StartsWith(request.Prefix ?? "", StringComparison.Ordinal)).ToList();
                    var page = matching.Skip(start).Take(request.MaxKeys ?? 1000).ToList();
                    bool truncated = start + page.Count < matching.Count;
                    return Task.FromResult(new ListObjectsV2Response
                    {
                        S3Objects = page.Select(k => new S3Object { Key = k }).ToList(),
                        IsTruncated = truncated,
                        NextContinuationToken = truncated ? (start + page.Count).ToString() : null,
                    });
                }
                case nameof(IAmazonS3.CopyObjectAsync):
                {
                    var request = (CopyObjectRequest)args![0]!;
                    Calls.Add($"copy {request.SourceKey} -> {request.DestinationKey}");
                    if (FailCopyTo?.Invoke(request.DestinationKey) == true)
                        return Task.FromException<CopyObjectResponse>(new AmazonS3Exception("copy denied"));
                    Objects[request.DestinationKey] = Objects[request.SourceKey];
                    return Task.FromResult(new CopyObjectResponse());
                }
                case nameof(IAmazonS3.DeleteObjectsAsync):
                {
                    var request = (DeleteObjectsRequest)args![0]!;
                    MaxDeleteBatch = Math.Max(MaxDeleteBatch, request.Objects.Count);
                    foreach (KeyVersion key in request.Objects) Objects.Remove(key.Key);
                    Calls.Add($"deleteMany {request.Objects.Count}");
                    return Task.FromResult(new DeleteObjectsResponse { DeleteErrors = [] });
                }
                case nameof(IAmazonS3.DeleteObjectAsync) when args!.Length == 3:
                {
                    string key = (string)args[1]!;
                    Calls.Add($"delete {key}");
                    if (FailDeleteOf?.Invoke(key) == true)
                        return Task.FromException<DeleteObjectResponse>(new AmazonS3Exception("delete denied"));
                    Objects.Remove(key);
                    return Task.FromResult(new DeleteObjectResponse());
                }
                case nameof(IAmazonS3.PutObjectAsync):
                {
                    var request = (PutObjectRequest)args![0]!;
                    Objects[request.Key] = [];
                    Calls.Add($"put {request.Key}");
                    return Task.FromResult(new PutObjectResponse());
                }
                case "Dispose":
                    return null;
                default:
                    throw new NotSupportedException(method.Name);
            }
        }
    }

    private static (S3ExplorerOperations Ops, FakeS3 Bucket) Create(params string[] keys)
    {
        IAmazonS3 client = DispatchProxy.Create<IAmazonS3, FakeS3>();
        var bucket = (FakeS3)(object)client;
        foreach (string key in keys) bucket.Objects[key] = [1];
        return (new S3ExplorerOperations(client, new S3ConfigModel { BucketName = "b" }), bucket);
    }

    [Test]
    public async Task CreateFolderWritesAMarkerObject()
    {
        (S3ExplorerOperations ops, FakeS3 bucket) = Create();
        await ops.CreateFolderAsync("root/new", CancellationToken.None);
        Assert.That(bucket.Objects.Keys, Is.EqualTo(new[] { "root/new/" }));
    }

    [Test]
    public async Task RenamesAFolderWithAllItsObjects()
    {
        (S3ExplorerOperations ops, FakeS3 bucket) = Create("a/", "a/x.png", "a/sub/y.png", "ab/keep.png");
        await ops.RenameFolderAsync("a/", "b/", CancellationToken.None);
        Assert.That(bucket.Objects.Keys, Is.EquivalentTo(new[] { "ab/keep.png", "b/", "b/sub/y.png", "b/x.png" }));
    }

    [Test]
    public async Task ImplicitFolderKeepsAMarkerAfterRename()
    {
        (S3ExplorerOperations ops, FakeS3 bucket) = Create("a/x.png");
        await ops.RenameFolderAsync("a", "b", CancellationToken.None);
        Assert.That(bucket.Objects.Keys, Is.EquivalentTo(new[] { "b/", "b/x.png" }));
    }

    [Test]
    public async Task FailedFolderRenameRollsBackCopies()
    {
        (S3ExplorerOperations ops, FakeS3 bucket) = Create("a/1.png", "a/2.png", "a/3.png");
        bucket.FailCopyTo = key => key == "b/3.png";
        await Assert.ThrowsAsync<AmazonS3Exception>(() => ops.RenameFolderAsync("a/", "b/", CancellationToken.None));
        Assert.That(bucket.Objects.Keys, Is.EquivalentTo(new[] { "a/1.png", "a/2.png", "a/3.png" }), "originals intact, partial copies removed");
    }

    [Test]
    public async Task FailedFileRenameDoesNotLeaveADuplicate()
    {
        (S3ExplorerOperations ops, FakeS3 bucket) = Create("a.png");
        bucket.FailDeleteOf = key => key == "a.png";
        await Assert.ThrowsAsync<AmazonS3Exception>(() => ops.RenameFileAsync("a.png", "b.png", CancellationToken.None));
        Assert.That(bucket.Objects.Keys, Is.EqualTo(new[] { "a.png" }));
    }

    [Test]
    public async Task DeletesLargeFoldersInBatches()
    {
        string[] keys = Enumerable.Range(0, 2500).Select(i => $"big/{i:D4}.png").Append("other.png").ToArray();
        (S3ExplorerOperations ops, FakeS3 bucket) = Create(keys);
        await ops.DeleteFolderAsync("big", CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(bucket.Objects.Keys, Is.EqualTo(new[] { "other.png" }));
            Assert.That(bucket.MaxDeleteBatch, Is.LessThanOrEqualTo(1000));
        });
    }

    [Test]
    public async Task ReportsWhetherAFolderHasChildren()
    {
        (S3ExplorerOperations ops, _) = Create("empty/", "full/", "full/x.png");
        Assert.That(await ops.HasChildrenAsync("empty", CancellationToken.None), Is.False);
        Assert.That(await ops.HasChildrenAsync("full/", CancellationToken.None), Is.True);
    }

    [TestCase("a/b/c.png", "a/b/")]
    [TestCase("a/b/", "a/")]
    [TestCase("c.png", "")]
    public void ParentKeys(string key, string parent) => Assert.That(AmazonS3Provider.GetParentKey(key), Is.EqualTo(parent));
}
