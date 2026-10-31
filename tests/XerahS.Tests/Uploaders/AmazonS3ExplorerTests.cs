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

using NUnit.Framework;
using ShareX.AmazonS3.Plugin;

namespace XerahS.Tests.Uploaders;

[TestFixture]
public class AmazonS3ExplorerTests
{
    [Test]
    public void GetExplorerRootPrefix_WholeBucket_ListsFromBucketRoot()
    {
        var config = new S3ConfigModel { ObjectPrefix = "ShareX/%y/%mo", ExplorerBrowseWholeBucket = true };
        string? root = S3ExplorerListHelper.GetExplorerRootPrefix(config);

        Assert.That(S3ExplorerListHelper.ResolveListPrefix(root, ""), Is.EqualTo(""));
        Assert.That(S3ExplorerListHelper.ResolveListPrefix(root, "2014/05/"), Is.EqualTo("2014/05/"));
        Assert.That(S3ExplorerListHelper.GetExplorerPath("ShareX/2026/10/a.png", root), Is.EqualTo("ShareX/2026/10/a.png"));
    }

    [Test]
    public void GetExplorerRootPrefix_Default_KeepsUploadFolder()
    {
        var config = new S3ConfigModel { ObjectPrefix = "ShareX/%y/%mo" };

        Assert.That(S3ExplorerListHelper.ResolveListPrefix(S3ExplorerListHelper.GetExplorerRootPrefix(config), ""), Is.EqualTo("ShareX/"));
    }

    [Test]
    public void ResolveListPrefix_EmptyFolder_UsesStaticObjectPrefix()
    {
        string prefix = S3ExplorerListHelper.ResolveListPrefix("ShareX/%y/%mo", "");

        Assert.That(prefix, Is.EqualTo("ShareX/"));
    }

    [Test]
    public void ResolveListPrefix_FolderPath_AppendsToLogicalRoot()
    {
        string prefix = S3ExplorerListHelper.ResolveListPrefix("ShareX/%y/%mo", "2026/");

        Assert.That(prefix, Is.EqualTo("ShareX/2026/"));
    }

    [Test]
    public void ResolveListPrefix_TokenEmbeddedInSegment_ListsNearestFolder()
    {
        string prefix = S3ExplorerListHelper.ResolveListPrefix("customers/tenant-%y/%mo", null);

        Assert.That(prefix, Is.EqualTo("customers/"));
    }

    [Test]
    public void ResolveListPrefix_TokenAtStart_ListsBucketRoot()
    {
        string prefix = S3ExplorerListHelper.ResolveListPrefix("%y/%mo", null);

        Assert.That(prefix, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ResolveListPrefix_StaticFolder_NavigatesBelowLogicalRoot()
    {
        string prefix = S3ExplorerListHelper.ResolveListPrefix("uploads", "images/");

        Assert.That(prefix, Is.EqualTo("uploads/images/"));
    }

    [Test]
    public void ResolveListPrefix_NoObjectPrefix_ListsBucketRoot()
    {
        string prefix = S3ExplorerListHelper.ResolveListPrefix(null, null);

        Assert.That(prefix, Is.EqualTo(string.Empty));
    }

    [Test]
    public void GetExplorerPath_ConfiguredPrefix_IsLogicalRoot()
    {
        string path = S3ExplorerListHelper.GetExplorerPath("ShareX/2026/", "ShareX/%y/%mo");

        Assert.That(path, Is.EqualTo("2026/"));
    }

    [Test]
    public void IsListBucketDenied_DetectsAwsAccessDeniedMessage()
    {
        const string serviceMessage =
            "S3 request failed: AccessDenied - User is not authorized to perform: s3:ListBucket";

        Assert.That(S3ExplorerListHelper.IsListBucketDenied(serviceMessage), Is.True);
    }

    [Test]
    public void IsListBucketDenied_DetectsStandardAccessDeniedMessage()
    {
        const string serviceMessage = "S3 request failed: AccessDenied - Access Denied";

        Assert.That(S3ExplorerListHelper.IsListBucketDenied(serviceMessage), Is.True);
    }

    [Test]
    public void IsListBucketDenied_DetectsEmptyBodyForbiddenFallback()
    {
        const string serviceMessage = "S3 request failed: 403 Forbidden";

        Assert.That(S3ExplorerListHelper.IsListBucketDenied(serviceMessage), Is.True);
    }

    [Test]
    public void IsListBucketDenied_DoesNotRewriteUnrelatedS3Failure()
    {
        const string serviceMessage = "S3 request failed: SignatureDoesNotMatch - The request signature does not match";

        Assert.That(S3ExplorerListHelper.IsListBucketDenied(serviceMessage), Is.False);
    }

    [Test]
    public void BuildListBucketDeniedMessage_IncludesBucketArnAndRequiredAction()
    {
        string message = S3ExplorerListHelper.BuildListBucketDeniedMessage(
            "example-bucket",
            "AccessDenied - s3:ListBucket");

        Assert.That(message, Does.Contain("s3:ListBucket"));
        Assert.That(message, Does.Contain("arn:aws:s3:::example-bucket"));
        Assert.That(message, Does.Contain("bucket-level list permission"));
        Assert.That(message, Does.Contain("s3:prefix"));
    }
}
