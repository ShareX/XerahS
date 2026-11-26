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
using XerahS.Common;

namespace XerahS.Tests.Common;

[TestFixture, NonParallelizable]
public sealed class FileDownloaderIntegrityTests
{
    [Test]
    public void DeclaredLength_PrematureEndOfStream_IsRejected()
    {
        string destination = Path.GetTempFileName();
        try
        {
            var downloader = new FileDownloader("https://example.invalid/truncated", destination);
            using var source = new MemoryStream(new byte[17]);
            Assert.That(async () => await FileDownloaderTestAccessor.CopyToFileAsync(
                downloader, source, destination, declaredFileSize: 32),
                Throws.TypeOf<EndOfStreamException>());
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Test]
    public async Task DeclaredLength_AcrossBufferBoundary_DoesNotConsumeTrailingBytes()
    {
        string destination = Path.GetTempFileName();
        try
        {
            var downloader = new FileDownloader("https://example.invalid/extra", destination);
            using var source = new MemoryStream(new byte[90_000]);
            long copied = await FileDownloaderTestAccessor.CopyToFileAsync(
                downloader, source, destination, declaredFileSize: 50_000);

            Assert.That(copied, Is.EqualTo(50_000));
            Assert.That(source.Position, Is.EqualTo(50_000));
            Assert.That(new FileInfo(destination).Length, Is.EqualTo(50_000));
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Test]
    public async Task DeclaredZeroLength_DoesNotReadInput()
    {
        string destination = Path.GetTempFileName();
        try
        {
            var downloader = new FileDownloader("https://example.invalid/empty", destination);
            using var source = new MemoryStream(new byte[] { 1, 2, 3 });
            long copied = await FileDownloaderTestAccessor.CopyToFileAsync(
                downloader, source, destination, declaredFileSize: 0);

            Assert.That(copied, Is.EqualTo(0));
            Assert.That(source.Position, Is.Zero);
            Assert.That(new FileInfo(destination).Length, Is.Zero);
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Test]
    public void PartialDownload_DoesNotOverwriteExistingFileOrLeaveStagingFiles()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string destination = Path.Combine(directory, "installer.exe");
            byte[] previous = [1, 2, 3, 4];
            File.WriteAllBytes(destination, previous);
            var downloader = new FileDownloader("https://example.invalid/installer", destination);
            using var source = new MemoryStream(new byte[17]);

            Assert.That(async () => await downloader.SaveCompletedDownloadAsync(source, 32, CancellationToken.None),
                Throws.TypeOf<EndOfStreamException>());
            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(previous));
            Assert.That(Directory.GetFiles(directory), Is.EquivalentTo(new[] { destination }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task CompleteDownload_ReplacesExistingFile()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string destination = Path.Combine(directory, "installer.exe");
            File.WriteAllBytes(destination, [1, 2, 3]);
            byte[] payload = new byte[80_000];
            for (int i = 0; i < payload.Length; i++)
                payload[i] = (byte)(i % 251);

            var downloader = new FileDownloader("https://example.invalid/installer", destination);
            using var source = new MemoryStream(payload);
            await downloader.SaveCompletedDownloadAsync(source, payload.Length, CancellationToken.None);

            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(payload));
            Assert.That(Directory.GetFiles(directory), Is.EquivalentTo(new[] { destination }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task UnknownLength_CompletesAtEndOfStream()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string destination = Path.Combine(directory, "payload.bin");
            byte[] payload = new byte[90_000];
            for (int i = 0; i < payload.Length; i++)
                payload[i] = (byte)(i % 251);

            var downloader = new FileDownloader("https://example.invalid/stream", destination);
            using var source = new MemoryStream(payload);
            await downloader.SaveCompletedDownloadAsync(source, null, CancellationToken.None);

            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(payload));
            Assert.That(Directory.GetFiles(directory), Is.EquivalentTo(new[] { destination }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void CanceledDownload_DoesNotOverwriteExistingFile()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string destination = Path.Combine(directory, "installer.exe");
            byte[] previous = [1, 2, 3];
            File.WriteAllBytes(destination, previous);
            var downloader = new FileDownloader("https://example.invalid/installer", destination);
            downloader.StopDownload();
            using var source = new MemoryStream(new byte[64]);

            Assert.That(async () => await downloader.SaveCompletedDownloadAsync(source, 64, CancellationToken.None),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(previous));
            Assert.That(Directory.GetFiles(directory), Is.EquivalentTo(new[] { destination }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"XerahS-FileDownloader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
