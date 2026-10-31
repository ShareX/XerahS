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

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture]
public class FFmpegDownloaderCancellationTests
{
    [Test]
    public async Task DownloadLatestAsync_PreCancelledToken_ReturnsCanceledFailureWithoutCreatingDestination()
    {
        // Arrange: use a temp folder that should NOT be created when cancellation is honoured up front.
        string destinationFolder = Path.Combine(Path.GetTempPath(), "xerahs-ffmpeg-cancel-" + Guid.NewGuid().ToString("N"));
        Assert.That(Directory.Exists(destinationFolder), Is.False, "Test precondition: destination must not exist.");

        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            // Act
            FFmpegDownloadResult result = await FFmpegDownloader.DownloadLatestAsync(destinationFolder, progress: null, cts.Token);

            // Assert
            Assert.That(result.Success, Is.False, "Pre-cancelled token must surface a failure result.");
            Assert.That(result.ErrorMessage, Is.EqualTo("FFmpeg download was canceled."));
            Assert.That(result.FFmpegPath, Is.Null);
            Assert.That(Directory.Exists(destinationFolder), Is.False, "Early-cancel path must not create the destination folder.");
        }
        finally
        {
            if (Directory.Exists(destinationFolder))
            {
                try { Directory.Delete(destinationFolder, recursive: true); } catch { }
            }
        }
    }

    [Test]
    public void DownloadLatestAsync_EmptyDestination_ReturnsDestinationFailure()
    {
        // Sanity guard: empty/whitespace destination is a different failure path from cancellation.
        // This test exists primarily to lock in that empty-destination failures still surface
        // and do not accidentally regress to "FFmpeg download was canceled." messages.
        FFmpegDownloadResult result = FFmpegDownloader.DownloadLatestAsync("", progress: null, CancellationToken.None).Result;
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("Destination folder was not provided."));
    }

    [Test]
    public async Task DownloadLatestAsync_WhitespaceDestination_ReturnsDestinationFailure()
    {
        FFmpegDownloadResult result = await FFmpegDownloader.DownloadLatestAsync("   ", progress: null, CancellationToken.None);
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("Destination folder was not provided."));
    }
}
