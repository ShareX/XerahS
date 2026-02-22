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

using XerahS.Common;
using System.Text;

namespace XerahS.Indexer
{
    /// <summary>
    /// Async streaming text indexer that writes tree structure directly to file.
    /// Minimizes memory usage for large directories by using StreamWriter.
    /// </summary>
    public class IndexerTextAsync : IndexerAsync
    {
        private readonly List<bool> _isLastStack = new();
        private StreamWriter? _writer;
        private readonly StringBuilder _previewBuilder = new();
        private int _previewLines;
        private int _maxPreviewLines;
        private bool _generatingPreview;

        public IndexerTextAsync(IndexerSettings indexerSettings, IProgress<IndexerProgress>? progress = null, CancellationToken cancellationToken = default)
            : base(indexerSettings, progress, cancellationToken)
        {
        }

        protected override async Task<IndexResult> IndexToFileAsync(string folderPath, string outputFilePath)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
                
                await using (var writer = new StreamWriter(outputFilePath, false, Encoding.UTF8))
                {
                    _writer = writer;
                    _generatingPreview = false;
                    
                    FolderInfo folderInfo = await GetFolderInfoAsync(folderPath);
                    await BuildTreeAsync(folderInfo, true);
                    
                    if (settings.AddFooter)
                    {
                        await WriteFooterAsync();
                    }
                }

                var result = new IndexResult
                {
                    Success = true,
                    OutputFilePath = outputFilePath,
                    TotalFiles = totalFilesProcessed,
                    TotalFolders = totalFoldersProcessed,
                    TotalBytes = totalBytesProcessed,
                    Duration = DateTime.UtcNow - startTime
                };

                ReportComplete(outputFilePath);
                return result;
            }
            catch (OperationCanceledException)
            {
                // Clean up partial file
                try { File.Delete(outputFilePath); } catch { }
                throw;
            }
            catch (Exception ex)
            {
                return new IndexResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Duration = DateTime.UtcNow - startTime
                };
            }
        }

        protected override async Task<(IndexResult Result, string Preview)> IndexWithPreviewAsync(
            string folderPath, string outputFilePath, int maxPreviewLines)
        {
            var startTime = DateTime.UtcNow;
            _maxPreviewLines = maxPreviewLines;
            _previewLines = 0;
            _previewBuilder.Clear();
            
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
                
                await using (var writer = new StreamWriter(outputFilePath, false, Encoding.UTF8))
                {
                    _writer = writer;
                    _generatingPreview = true;
                    
                    FolderInfo folderInfo = await GetFolderInfoAsync(folderPath);
                    await BuildTreeAsync(folderInfo, true);
                    
                    if (settings.AddFooter)
                    {
                        await WriteFooterAsync();
                    }
                }

                var result = new IndexResult
                {
                    Success = true,
                    OutputFilePath = outputFilePath,
                    TotalFiles = totalFilesProcessed,
                    TotalFolders = totalFoldersProcessed,
                    TotalBytes = totalBytesProcessed,
                    Duration = DateTime.UtcNow - startTime
                };

                ReportComplete(outputFilePath);
                return (result, _previewBuilder.ToString());
            }
            catch (OperationCanceledException)
            {
                try { File.Delete(outputFilePath); } catch { }
                throw;
            }
            catch (Exception ex)
            {
                var result = new IndexResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Duration = DateTime.UtcNow - startTime
                };
                return (result, _previewBuilder.ToString());
            }
        }

        private async Task BuildTreeAsync(FolderInfo dir, bool isLast)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Add current folder line
            await WriteTreeFolderRowAsync(dir, isLast);

            _isLastStack.Add(isLast);

            // Collect all items (folders + files) for tree display
            var items = new List<TreeItem>();
            
            foreach (var subdir in dir.Folders)
            {
                items.Add(new TreeItem(subdir.FolderName, subdir, null, true));
            }
            
            foreach (var file in dir.Files)
            {
                items.Add(new TreeItem(file.Name, null, file, false));
            }

            // Process items with tree connectors
            for (int i = 0; i < items.Count; i++)
            {
                bool itemIsLast = (i == items.Count - 1);
                var item = items[i];

                if (item.IsFolder && item.FolderInfo != null)
                {
                    await BuildTreeFolderAsync(item.FolderInfo, itemIsLast);
                }
                else if (item.FileInfo != null)
                {
                    await BuildTreeFileAsync(item.FileInfo, itemIsLast);
                }

                // Yield periodically to keep UI responsive
                if (i % 100 == 0)
                {
                    await Task.Yield();
                }
            }

            _isLastStack.RemoveAt(_isLastStack.Count - 1);
        }

        private async Task BuildTreeFolderAsync(FolderInfo dir, bool isLast)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await WriteTreeItemRowAsync(dir.FolderName, isLast, true);

            _isLastStack.Add(isLast);

            // Collect sub-items
            var items = new List<TreeItem>();
            foreach (var subdir in dir.Folders)
            {
                items.Add(new TreeItem(subdir.FolderName, subdir, null, true));
            }
            foreach (var file in dir.Files)
            {
                items.Add(new TreeItem(file.Name, null, file, false));
            }

            // Process sub-items
            for (int i = 0; i < items.Count; i++)
            {
                bool itemIsLast = (i == items.Count - 1);
                var item = items[i];

                if (item.IsFolder && item.FolderInfo != null)
                {
                    await BuildTreeFolderAsync(item.FolderInfo, itemIsLast);
                }
                else if (item.FileInfo != null)
                {
                    await BuildTreeFileAsync(item.FileInfo, itemIsLast);
                }

                if (i % 100 == 0)
                {
                    await Task.Yield();
                }
            }

            _isLastStack.RemoveAt(_isLastStack.Count - 1);
        }

        private async Task BuildTreeFileAsync(FileInfo file, bool isLast)
        {
            await WriteTreeItemRowAsync(file.Name, isLast, false, file);
        }

        private async Task WriteTreeFolderRowAsync(FolderInfo dir, bool isLast)
        {
            string name = dir.FolderName;
            
            if (settings.ShowSizeInfo && dir.Size > 0)
            {
                name += string.Format(" [{0}]", dir.Size.ToSizeString(settings.BinaryUnits));
            }
            
            string line;
            if (_isLastStack.Count == 0)
            {
                line = name;
            }
            else
            {
                line = GetTreePrefix(isLast) + name;
            }

            await WriteLineAsync(line);
        }

        private async Task WriteTreeItemRowAsync(string name, bool isLast, bool isFolder, FileInfo? fileInfo = null)
        {
            if (settings.ShowSizeInfo && fileInfo != null)
            {
                name += string.Format(" [{0}]", fileInfo.Length.ToSizeString(settings.BinaryUnits));
            }

            string line = GetTreePrefix(isLast) + name;
            await WriteLineAsync(line);
        }

        private async Task WriteLineAsync(string line)
        {
            if (_writer != null)
            {
                await _writer.WriteLineAsync(line);
            }

            // Build preview only if within limit
            if (_generatingPreview && _previewLines < _maxPreviewLines)
            {
                _previewBuilder.AppendLine(line);
                _previewLines++;
            }
            else if (_generatingPreview && _previewLines == _maxPreviewLines)
            {
                _previewBuilder.AppendLine($"... ({totalFilesProcessed + totalFoldersProcessed} more items - preview truncated)");
                _previewLines++;
            }
        }

        private async Task WriteFooterAsync()
        {
            string footer = $"Generated by ShareX Directory Indexer on {DateTime.UtcNow:yyyy-MM-dd 'at' HH:mm:ss 'UTC'}. Latest version can be downloaded from: {Links.XerahSWebsite}";
            await WriteLineAsync(string.Empty);
            await WriteLineAsync("_".Repeat(footer.Length) ?? new string('_', footer.Length));
            await WriteLineAsync(footer);
        }

        private string GetTreePrefix(bool isLast)
        {
            var sb = new StringBuilder();
            
            for (int i = 0; i < _isLastStack.Count; i++)
            {
                if (i == _isLastStack.Count - 1)
                {
                    sb.Append(isLast ? "└── " : "├── ");
                }
                else
                {
                    sb.Append(_isLastStack[i] ? "    " : "│   ");
                }
            }
            
            return sb.ToString();
        }

        private class TreeItem
        {
            public string Name { get; }
            public FolderInfo? FolderInfo { get; }
            public FileInfo? FileInfo { get; }
            public bool IsFolder { get; }

            public TreeItem(string name, FolderInfo? folderInfo, FileInfo? fileInfo, bool isFolder)
            {
                Name = name;
                FolderInfo = folderInfo;
                FileInfo = fileInfo;
                IsFolder = isFolder;
            }
        }
    }
}
