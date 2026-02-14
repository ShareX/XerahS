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

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XerahS.Indexer
{
    /// <summary>
    /// Async streaming indexer that writes directly to file without building giant strings in memory.
    /// Prevents UI freezing when indexing large directories (8+ GiB).
    /// </summary>
    public abstract class IndexerAsync
    {
        protected IndexerSettings settings = null!;
        protected IProgress<IndexerProgress>? progress;
        protected CancellationToken cancellationToken;
        protected long totalFilesProcessed;
        protected long totalFoldersProcessed;
        protected long totalBytesProcessed;

        protected IndexerAsync(IndexerSettings indexerSettings, IProgress<IndexerProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            settings = indexerSettings ?? throw new ArgumentNullException(nameof(indexerSettings));
            this.progress = progress;
            this.cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Indexes a folder and writes output directly to a file.
        /// Uses streaming to minimize memory usage for large directories.
        /// </summary>
        public static async Task<IndexResult> IndexToFileAsync(
            string folderPath, 
            string outputFilePath,
            IndexerSettings settings,
            IProgress<IndexerProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);
            
            IndexerAsync indexer = settings.Output switch
            {
                IndexerOutput.Html => new IndexerHtmlAsync(settings, progress, cancellationToken),
                IndexerOutput.Txt => new IndexerTextAsync(settings, progress, cancellationToken),
                IndexerOutput.Xml => new IndexerXmlAsync(settings, progress, cancellationToken),
                IndexerOutput.Json => new IndexerJsonAsync(settings, progress, cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported indexer output: {settings.Output}")
            };

            return await indexer.IndexToFileAsync(folderPath, outputFilePath);
        }

        /// <summary>
        /// Indexes a folder and returns a preview (first N lines) along with full output path.
        /// Full output is written to file, only preview is kept in memory.
        /// </summary>
        public static async Task<(IndexResult Result, string Preview)> IndexWithPreviewAsync(
            string folderPath,
            string outputFilePath,
            IndexerSettings settings,
            int maxPreviewLines = 1000,
            IProgress<IndexerProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);
            
            IndexerAsync indexer = settings.Output switch
            {
                IndexerOutput.Html => new IndexerHtmlAsync(settings, progress, cancellationToken),
                IndexerOutput.Txt => new IndexerTextAsync(settings, progress, cancellationToken),
                IndexerOutput.Xml => new IndexerXmlAsync(settings, progress, cancellationToken),
                IndexerOutput.Json => new IndexerJsonAsync(settings, progress, cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported indexer output: {settings.Output}")
            };

            return await indexer.IndexWithPreviewAsync(folderPath, outputFilePath, maxPreviewLines);
        }

        protected abstract Task<IndexResult> IndexToFileAsync(string folderPath, string outputFilePath);

        protected abstract Task<(IndexResult Result, string Preview)> IndexWithPreviewAsync(string folderPath, string outputFilePath, int maxPreviewLines);

        protected async Task<FolderInfo> GetFolderInfoAsync(string folderPath, int level = 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            FolderInfo folderInfo = new FolderInfo(folderPath);

            if (settings.MaxDepthLevel == 0 || level < settings.MaxDepthLevel)
            {
                try
                {
                    DirectoryInfo currentDirectoryInfo = new DirectoryInfo(folderPath);

                    foreach (DirectoryInfo directoryInfo in currentDirectoryInfo.EnumerateDirectories())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (settings.SkipHiddenFolders && directoryInfo.Attributes.HasFlag(FileAttributes.Hidden))
                        {
                            continue;
                        }

                        FolderInfo subFolderInfo = await GetFolderInfoAsync(directoryInfo.FullName, level + 1);
                        folderInfo.Folders.Add(subFolderInfo);
                        subFolderInfo.Parent = folderInfo;
                        
                        totalFoldersProcessed++;
                        
                        // Report progress every 10 folders to avoid overwhelming the UI
                        if (totalFoldersProcessed % 10 == 0)
                        {
                            ReportProgress($"Scanning: {directoryInfo.FullName}", totalFilesProcessed, totalFoldersProcessed);
                            await Task.Yield(); // Allow UI to breathe
                        }
                    }

                    if (!settings.SkipFiles)
                    {
                        foreach (FileInfo fileInfo in currentDirectoryInfo.EnumerateFiles())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (settings.SkipHiddenFiles && fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
                            {
                                continue;
                            }

                            folderInfo.Files.Add(fileInfo);
                            totalFilesProcessed++;
                            totalBytesProcessed += fileInfo.Length;
                        }

                        folderInfo.Files.Sort((x, y) => x.Name.CompareTo(y.Name));
                    }
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return folderInfo;
        }

        protected void ReportProgress(string currentItem, long filesProcessed, long foldersProcessed)
        {
            progress?.Report(new IndexerProgress
            {
                CurrentItem = currentItem,
                FilesProcessed = filesProcessed,
                FoldersProcessed = foldersProcessed,
                TotalBytesProcessed = totalBytesProcessed,
                PercentComplete = null // Unknown until complete
            });
        }

        protected void ReportComplete(string outputPath)
        {
            progress?.Report(new IndexerProgress
            {
                CurrentItem = "Complete",
                FilesProcessed = totalFilesProcessed,
                FoldersProcessed = totalFoldersProcessed,
                TotalBytesProcessed = totalBytesProcessed,
                PercentComplete = 100,
                OutputFilePath = outputPath
            });
        }
    }

    /// <summary>
    /// Progress information for indexer operations.
    /// </summary>
    public class IndexerProgress
    {
        public string CurrentItem { get; set; } = string.Empty;
        public long FilesProcessed { get; set; }
        public long FoldersProcessed { get; set; }
        public long TotalBytesProcessed { get; set; }
        public int? PercentComplete { get; set; }
        public string? OutputFilePath { get; set; }
    }

    /// <summary>
    /// Result of an indexing operation.
    /// </summary>
    public class IndexResult
    {
        public bool Success { get; set; }
        public string OutputFilePath { get; set; } = string.Empty;
        public long TotalFiles { get; set; }
        public long TotalFolders { get; set; }
        public long TotalBytes { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
