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

namespace XerahS.Indexer
{
    /// <summary>
    /// Async streaming XML indexer.
    /// </summary>
    public class IndexerXmlAsync : IndexerAsync
    {
        public IndexerXmlAsync(IndexerSettings indexerSettings, IProgress<IndexerProgress>? progress = null, CancellationToken cancellationToken = default)
            : base(indexerSettings, progress, cancellationToken)
        {
        }

        protected override async Task<IndexResult> IndexToFileAsync(string folderPath, string outputFilePath)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                var legacyIndexer = new IndexerXml(settings);
                string output = await Task.Run(() => legacyIndexer.Index(folderPath), cancellationToken);
                await File.WriteAllTextAsync(outputFilePath, output, cancellationToken);

                return new IndexResult
                {
                    Success = true,
                    OutputFilePath = outputFilePath,
                    Duration = DateTime.UtcNow - startTime
                };
            }
            catch (OperationCanceledException)
            {
                try { File.Delete(outputFilePath); } catch { }
                throw;
            }
            catch (Exception ex)
            {
                return new IndexResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        protected override async Task<(IndexResult Result, string Preview)> IndexWithPreviewAsync(
            string folderPath, string outputFilePath, int maxPreviewLines)
        {
            var result = await IndexToFileAsync(folderPath, outputFilePath);
            
            string preview = string.Empty;
            if (result.Success)
            {
                var lines = await File.ReadAllLinesAsync(outputFilePath, cancellationToken);
                preview = string.Join("\n", lines.Take(maxPreviewLines));
                if (lines.Length > maxPreviewLines)
                {
                    preview += $"\n... ({lines.Length - maxPreviewLines} more lines)";
                }
            }
            
            return (result, preview);
        }
    }
}
