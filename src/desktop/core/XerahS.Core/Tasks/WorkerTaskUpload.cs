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
using XerahS.Core.Helpers;
using XerahS.Core.Tasks.Processors;
using XerahS.Platform.Abstractions;

namespace XerahS.Core.Tasks
{
    /// <summary>
    /// WorkerTask partial class for upload and clipboard operations.
    /// </summary>
    public partial class WorkerTask
    {
        private bool TryLoadClipboardContent(TaskSettings taskSettings, TaskMetadata metadata, out string[]? clipboardFiles)
        {
            clipboardFiles = null;
            var clipboard = PlatformServices.Clipboard;
            if (clipboard == null)
            {
                return false;
            }

            var content = ClipboardContentHelper.ParseClipboard(clipboard);
            if (content == null)
            {
                return false;
            }

            switch (content.DataType)
            {
                case EDataType.Image:
                    metadata.Image = content.Image;
                    Info.DataType = EDataType.Image;
                    Info.Job = TaskJob.DataUpload;
                    string imageExtension = EnumExtensions.GetDescription(taskSettings.ImageSettings.ImageFormat);
                    Info.SetFileName(TaskHelpers.GetFileName(taskSettings, imageExtension, metadata));
                    return true;

                case EDataType.Text:
                    Info.TextContent = content.Text;
                    Info.DataType = EDataType.Text;
                    Info.Job = TaskJob.TextUpload;
                    string textExtension = taskSettings.AdvancedSettings.TextFileExtension;
                    Info.SetFileName(TaskHelpers.GetFileName(taskSettings, textExtension, metadata));
                    return true;

                case EDataType.File:
                    clipboardFiles = content.Files;
                    if (clipboardFiles == null || clipboardFiles.Length == 0)
                    {
                        return false;
                    }
                    Info.FilePath = clipboardFiles[0];
                    Info.DataType = EDataType.File;
                    Info.Job = TaskJob.FileUpload;
                    return true;
            }

            return false;
        }

        private async Task UploadClipboardFilesAsync(TaskSettings taskSettings, string[] files, CancellationToken token)
        {
            var uploadProcessor = new UploadJobProcessor();
            TaskInfo? lastInfo = null;

            foreach (var filePath in files)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    continue;
                }

                var fileInfo = new TaskInfo(taskSettings)
                {
                    DataType = EDataType.File,
                    Job = TaskJob.FileUpload,
                    FilePath = filePath
                };

                await uploadProcessor.ProcessAsync(fileInfo, token);
                lastInfo = fileInfo;
            }

            if (lastInfo != null)
            {
                Info.DataType = lastInfo.DataType;
                Info.FilePath = lastInfo.FilePath;
                Info.Job = lastInfo.Job;
                Info.Result = lastInfo.Result;
                Info.Metadata.UploadURL = lastInfo.Metadata.UploadURL;
            }
        }

        private bool TryIndexFolder(TaskSettings taskSettings, out string? outputPath)
        {
            outputPath = null;

            if (taskSettings?.ToolsSettings == null)
            {
                DebugHelper.WriteLine("IndexFolder: ToolsSettings missing.");
                return false;
            }

            string folderPath = taskSettings.ToolsSettings.IndexerFolderPath;
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                DebugHelper.WriteLine($"IndexFolder: Folder path invalid: '{folderPath}'");
                return false;
            }

            try
            {
                var coreSettings = taskSettings.ToolsSettings.IndexerSettings ?? new IndexerSettings();
                var indexerSettings = BuildIndexerSettings(coreSettings);

                string output = XerahS.Indexer.Indexer.Index(folderPath, indexerSettings);
                outputPath = WriteIndexOutput(taskSettings, folderPath, output, coreSettings.Output);
                return !string.IsNullOrEmpty(outputPath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "IndexFolder: indexing failed");
                return false;
            }
        }

        private static XerahS.Indexer.IndexerSettings BuildIndexerSettings(IndexerSettings settings)
        {
            var indexerSettings = new XerahS.Indexer.IndexerSettings
            {
                Output = (XerahS.Indexer.IndexerOutput)settings.Output,
                SkipHiddenFolders = settings.SkipHiddenFolders,
                SkipHiddenFiles = settings.SkipHiddenFiles,
                SkipFiles = settings.SkipFiles,
                MaxDepthLevel = settings.MaxDepthLevel,
                ShowSizeInfo = settings.ShowSizeInfo,
                AddFooter = settings.AddFooter,
                IndentationText = settings.IndentationText,
                AddEmptyLineAfterFolders = settings.AddEmptyLineAfterFolders,
                UseCustomCSSFile = settings.UseCustomCSSFile,
                DisplayPath = settings.DisplayPath,
                DisplayPathLimited = settings.DisplayPathLimited,
                CustomCSSFilePath = settings.CustomCSSFilePath,
                UseAttribute = settings.UseAttribute,
                CreateParseableJson = settings.CreateParseableJson
            };

            indexerSettings.BinaryUnits = settings.BinaryUnits;
            return indexerSettings;
        }

        private static string WriteIndexOutput(TaskSettings taskSettings, string folderPath, string output, IndexerOutput outputType)
        {
            string extension = GetIndexFolderExtension(outputType);
            string screenshotsFolder = TaskHelpers.GetScreenshotsFolder(taskSettings);
            Directory.CreateDirectory(screenshotsFolder);

            string fileName = TaskHelpers.GetFileName(taskSettings, extension);
            string resolvedPath = TaskHelpers.HandleExistsFile(screenshotsFolder, fileName, taskSettings);
            File.WriteAllText(resolvedPath, output);
            return resolvedPath;
        }

        private static string GetIndexFolderExtension(IndexerOutput outputType)
        {
            return outputType switch
            {
                IndexerOutput.Html => "html",
                IndexerOutput.Txt => "txt",
                IndexerOutput.Xml => "xml",
                IndexerOutput.Json => "json",
                _ => "txt"
            };
        }
    }
}
