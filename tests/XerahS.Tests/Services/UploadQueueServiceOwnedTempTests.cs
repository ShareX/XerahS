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

#nullable enable

using System.IO;
using NUnit.Framework;
using XerahS.Core.Services;

namespace XerahS.Tests.Services;

[TestFixture]
public sealed class UploadQueueServiceOwnedTempTests
{
    [Test]
    public void PendingPrivacyReviewPreservesOwnedSourceFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"privacy-pending-{Guid.NewGuid():N}.png");
        File.WriteAllText(path, "synthetic source");
        try
        {
            UploadQueueService.TryDeleteOwnedTempFile(new UploadQueueItem { FilePath = path, IsOwnedTempFile = true, ReviewRequired = true });
            Assert.That(File.Exists(path), Is.True);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(new UploadQueueItem
                { FilePath = path, IsOwnedTempFile = true, ReviewRequired = true, PrivacyRequired = true });
            var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<UploadQueueItem>(json)!;
            Assert.That(restored.ReviewRequired, Is.True);
            Assert.That(restored.PrivacyRequired, Is.True);
            Assert.That(restored.IsOwnedTempFile, Is.False);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void TryDeleteOwnedTempFile_DeletesExistingOwnedFile()
    {
        string path = Path.Combine(Path.GetTempPath(), "xerahs_mobile_" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllText(path, "owned-temp");
        Assert.That(File.Exists(path), Is.True);

        try
        {
            var item = new UploadQueueItem
            {
                FilePath = path,
                IsOwnedTempFile = true
            };

            UploadQueueService.TryDeleteOwnedTempFile(item);

            Assert.That(File.Exists(path), Is.False, "Owned temp file must be deleted after processing.");
        }
        finally
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { /* best-effort cleanup */ }
            }
        }
    }

    [Test]
    public void TryDeleteOwnedTempFile_NoOpWhenNotOwned()
    {
        string path = Path.Combine(Path.GetTempPath(), "xerahs_mobile_" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllText(path, "user-file");
        Assert.That(File.Exists(path), Is.True);

        try
        {
            var item = new UploadQueueItem
            {
                FilePath = path,
                IsOwnedTempFile = false
            };

            UploadQueueService.TryDeleteOwnedTempFile(item);

            Assert.That(File.Exists(path), Is.True, "Non-owned paths must never be deleted by the queue.");
        }
        finally
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { /* best-effort cleanup */ }
            }
        }
    }

    [Test]
    public void TryDeleteOwnedTempFile_MissingFile_DoesNotThrow()
    {
        var item = new UploadQueueItem
        {
            FilePath = Path.Combine(Path.GetTempPath(), "xerahs_mobile_missing_" + Guid.NewGuid().ToString("N") + ".bin"),
            IsOwnedTempFile = true
        };

        Assert.DoesNotThrow(() => UploadQueueService.TryDeleteOwnedTempFile(item));
    }

    [Test]
    public void TryDeleteOwnedTempFile_NullOrEmptyPath_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => UploadQueueService.TryDeleteOwnedTempFile(null!));
        Assert.DoesNotThrow(() => UploadQueueService.TryDeleteOwnedTempFile(new UploadQueueItem
        {
            FilePath = "",
            IsOwnedTempFile = true
        }));
        Assert.DoesNotThrow(() => UploadQueueService.TryDeleteOwnedTempFile(new UploadQueueItem
        {
            FilePath = "   ",
            IsOwnedTempFile = true
        }));
    }

    [Test]
    public void UploadQueueItem_IsOwnedTempFile_DefaultsFalse()
    {
        // Snapshot deserialization leaves missing bool fields as false, so a
        // restarted process never auto-deletes a stale path.
        var item = new UploadQueueItem { FilePath = "/tmp/x" };
        Assert.That(item.IsOwnedTempFile, Is.False);
    }
}
