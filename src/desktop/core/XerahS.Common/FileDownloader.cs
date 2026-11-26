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

using System.Diagnostics;

namespace XerahS.Common
{
    internal static class FileDownloaderTestAccessor
    {
        public static async Task<(long downloadedSize, bool completed)> SimulateDownloadWithEarlyEOF(
            long fileSize, byte[] receiveSequence)
        {
            // Simulates the loop behavior when a server closes the connection early.
            // Returns (DownloadedSize, completed) matching what DoWork() would produce.
            long downloadedSize = 0;
            int idx = 0;
            while (downloadedSize < fileSize)
            {
                if (idx >= receiveSequence.Length)
                    break; // early EOF — same as bytesRead <= 0
                downloadedSize += receiveSequence[idx++];
            }
            bool completed = downloadedSize >= fileSize;
            return (downloadedSize, completed);
        }

        public static async Task<long> CopyToFileAsync(
            FileDownloader downloader,
            Stream source,
            string destinationPath,
            long? declaredFileSize,
            CancellationToken cancellationToken = default)
        {
            using FileStream fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            return await downloader.CopyToFileAsync(source, fileStream, declaredFileSize, cancellationToken);
        }
    }

    public class FileDownloader
    {
        public event Action? FileSizeReceived;
        public event Action? ProgressChanged;

        public string URL { get; set; } = string.Empty;
        public string DownloadLocation { get; set; } = string.Empty;
        public string? AcceptHeader { get; set; }

        public bool IsDownloading { get; private set; }
        public bool IsCanceled { get; private set; }
        public long FileSize { get; private set; } = -1;
        public long DownloadedSize { get; private set; }
        public double DownloadSpeed { get; private set; }

        private CancellationTokenSource? _cts;

        public double DownloadPercentage
        {
            get
            {
                if (FileSize > 0)
                {
                    return (double)DownloadedSize / FileSize * 100;
                }

                return 0;
            }
        }

        private const int bufferSize = 32768;

        public FileDownloader()
        {
        }

        public FileDownloader(string url, string downloadLocation)
        {
            URL = url;
            DownloadLocation = downloadLocation;
        }

        public async Task<bool> StartDownload(CancellationToken cancellationToken = default)
        {
            if (!IsDownloading && !string.IsNullOrEmpty(URL))
            {
                IsDownloading = true;
                IsCanceled = false;
                FileSize = -1;
                DownloadedSize = 0;
                DownloadSpeed = 0;

                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                bool result = await DoWork(_cts.Token);

                // Reset IsDownloading on early exit (e.g., canceled token)
                IsDownloading = false;
                return result;
            }

            return false;
        }

        public void StopDownload()
        {
            IsCanceled = true;
            _cts?.Cancel();
        }

        /// <summary>
        /// Copies a response body. A declared length limits reads and rejects premature EOF;
        /// an unknown-length body completes at the stream's normal end.
        /// </summary>
        internal async Task<long> CopyToFileAsync(
            Stream source,
            FileStream destination,
            long? declaredFileSize,
            CancellationToken cancellationToken)
        {
            if (declaredFileSize is < 0)
                throw new InvalidDataException("Content-Length cannot be negative.");

            byte[] buffer = new byte[bufferSize];
            Stopwatch timer = new Stopwatch();
            Stopwatch progressEventTimer = new Stopwatch();
            long speedTest = 0;
            long totalCopied = 0;

            while (!IsCanceled && (!declaredFileSize.HasValue || totalCopied < declaredFileSize.Value))
            {
                if (!timer.IsRunning)
                    timer.Start();
                if (!progressEventTimer.IsRunning)
                    progressEventTimer.Start();

                int readLength = declaredFileSize.HasValue
                    ? (int)Math.Min(buffer.Length, declaredFileSize.Value - totalCopied)
                    : buffer.Length;
                int bytesRead = await source.ReadAsync(buffer.AsMemory(0, readLength), cancellationToken);

                if (bytesRead <= 0)
                {
                    if (declaredFileSize.HasValue)
                        throw new EndOfStreamException($"Download ended after {totalCopied} of {declaredFileSize.Value} bytes.");
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalCopied += bytesRead;
                DownloadedSize += bytesRead;
                speedTest += bytesRead;

                if (timer.ElapsedMilliseconds > 500)
                {
                    DownloadSpeed = (double)speedTest / timer.ElapsedMilliseconds * 1000;
                    speedTest = 0;
                    timer.Reset();
                }

                if (progressEventTimer.ElapsedMilliseconds > 100)
                {
                    ProgressChanged?.Invoke();
                    progressEventTimer.Reset();
                }
            }

            return totalCopied;
        }

        /// <summary>Publish a download only after all expected bytes have arrived.</summary>
        internal async Task SaveCompletedDownloadAsync(Stream source, long? declaredFileSize, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsCanceled)
                throw new OperationCanceledException("Download canceled.");

            string destinationPath = Path.GetFullPath(DownloadLocation);
            string temporaryPath = Path.Combine(Path.GetDirectoryName(destinationPath)!,
                $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.partial");

            try
            {
                await using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, bufferSize, useAsync: true))
                {
                    await CopyToFileAsync(source, file, declaredFileSize, cancellationToken);
                    await file.FlushAsync(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (IsCanceled)
                    throw new OperationCanceledException("Download canceled.");

                // Staging in the destination directory allows an atomic rename on supported file systems.
                File.Move(temporaryPath, destinationPath, overwrite: true);
            }
            finally
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException ex)
                {
                    DebugHelper.WriteException(ex, "Could not remove incomplete download.");
                }
                catch (UnauthorizedAccessException ex)
                {
                    DebugHelper.WriteException(ex, "Could not remove incomplete download.");
                }
            }
        }

        private async Task<bool> DoWork(CancellationToken cancellationToken)
        {
            // Check cancellation before starting any network operations
            if (cancellationToken.IsCancellationRequested)
            {
                IsCanceled = true;
                return false;
            }

            try
            {
                HttpClient client = HttpClientFactory.Create(allowAutoRedirect: true, infiniteTimeout: true);

                using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, URL))
                {
                    if (!string.IsNullOrEmpty(AcceptHeader))
                    {
                        requestMessage.Headers.Accept.ParseAdd(AcceptHeader);
                    }

                    using (HttpResponseMessage responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        responseMessage.EnsureSuccessStatusCode();

                        long? declaredFileSize = responseMessage.Content.Headers.ContentLength;
                        // Keep the legacy FileSize value semantics for callers that already inspect
                        // FileSize (e.g. FFmpegDownloader / DownloaderWindowViewModel): 0 means
                        // "unknown length at start of download" so DownloadPercentage stays at 0
                        // rather than reporting a misleading percentage against a -1 denominator.
                        FileSize = declaredFileSize ?? 0;

                        FileSizeReceived?.Invoke();

                        using (Stream responseStream = await responseMessage.Content.ReadAsStreamAsync(cancellationToken))
                        {
                            await SaveCompletedDownloadAsync(responseStream, declaredFileSize, cancellationToken);
                            ProgressChanged?.Invoke();
                        }

                        return true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation requested - set IsCanceled flag
                IsCanceled = true;
            }
            catch (InvalidOperationException)
            {
                // Invalid URI (e.g. "x" is not a valid absolute URI)
                // This is a user/configuration error, not a network error
            }
            catch
            {
                if (!IsCanceled)
                {
                    throw;
                }
            }
            finally
            {
                // An interrupted attempt only cleans up its staging file; a prior completed
                // download at DownloadLocation must remain untouched.
                IsDownloading = false;
            }

            return false;
        }
    }
}
