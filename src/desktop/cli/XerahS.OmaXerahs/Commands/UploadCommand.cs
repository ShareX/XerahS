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

using System.CommandLine;
using System.Text;
using Newtonsoft.Json;
using XerahS.Common;
using XerahS.Common.Utilities;
using XerahS.Core;
using XerahS.Core.Tasks;
using XerahS.Core.Tasks.Processors;
using XerahS.OmaXerahs.Models;
using XerahS.OmaXerahs.Services;
using XerahS.Uploaders;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.OmaXerahs.Commands;

internal static class UploadCommand
{
    internal static Func<TaskInfo, CancellationToken, Task> ProcessUploadAsync { get; set; } =
        static (taskInfo, cancellationToken) => new UploadJobProcessor().ProcessAsync(taskInfo, cancellationToken);

    /// <summary>Starts XerahS services. Swappable so tests can run <see cref="UploadToDestinationAsync"/> offline.</summary>
    internal static Func<Task> EnsureBootstrapped { get; set; } = UploadHost.EnsureBootstrappedAsync;

    /// <summary>The usable File destinations. Swappable for tests.</summary>
    internal static Func<List<UploaderInstance>> GetFileDestinations { get; set; } = UploadHost.GetUsableFileInstances;

    internal static Func<string, ImageDestinationInspection> InspectImage { get; set; } =
        UploadHost.InspectImageDestination;

    internal static Command Create()
    {
        var command = new Command("upload", "Upload an image to the configured Image destination, or any file to a named --destination.");
        var pathArgument = new Argument<string?>("path")
        {
            Description = "Path to the file. Pass after -- so names starting with - are safe.",
            Arity = ArgumentArity.ZeroOrOne
        };
        var jsonOption = JsonStdout.CreateJsonOption();
        var urlOnlyOption = new Option<bool>("--url-only")
        {
            Description = "Print only the uploaded URL (for hosts such as OmaSnap pins); errors go to stderr."
        };
        var destinationOption = new Option<string?>("--destination")
        {
            Description = "File destination: an instance id, a unique id prefix (6+ characters), its display name, or its provider id such as kovaforge. Accepts any file type. The uploaded name is the file's name with the workflow's name pattern appended before the extension (page.html becomes page_<pattern>.html), so a re-upload gets a new URL. Use --keep-name to keep the exact name."
        };
        var keepNameOption = new Option<bool>("--keep-name")
        {
            Description = "With --destination: keep the exact source file name instead of appending the name pattern. Needed when a page must keep a stable name. Not valid without --destination."
        };
        command.Add(pathArgument);
        command.Add(jsonOption);
        command.Add(urlOnlyOption);
        command.Add(destinationOption);
        command.Add(keepNameOption);
        command.SetAction(parseResult =>
        {
            JsonStdout.UrlOnly = parseResult.GetValue(urlOnlyOption);
            JsonStdout.Enabled = !JsonStdout.UrlOnly && parseResult.GetValue(jsonOption);
            string? destination = parseResult.GetValue(destinationOption);
            bool keepName = parseResult.GetValue(keepNameOption);
            if (destination == null && keepName)
            {
                return JsonStdout.WriteFailureAndExit(CliErrorCodes.Usage, "--keep-name only applies with --destination.");
            }

            return (destination == null
                ? UploadAsync(parseResult.GetValue(pathArgument))
                : UploadToDestinationAsync(parseResult.GetValue(pathArgument), destination, keepName)).GetAwaiter().GetResult();
        });
        return command;
    }

    internal static bool TryValidateImagePath(string? path, out string canonicalPath, out string errorCode, out string errorMessage)
    {
        canonicalPath = string.Empty;
        errorCode = CliErrorCodes.InvalidPath;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            errorCode = CliErrorCodes.Usage;
            errorMessage = "Specify an image path after --.";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid path: {ex.Message}";
            return false;
        }

        string resolved = fullPath;
        try
        {
            var linkTarget = File.ResolveLinkTarget(fullPath, returnFinalTarget: true);
            if (linkTarget != null)
            {
                resolved = linkTarget.FullName;
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // The watch folder may already have moved this screenshot. Existence is resolved
            // again after startup, when history and the screenshots folder can be checked.
        }
        catch (Exception ex)
        {
            errorMessage = $"Unable to resolve path: {ex.Message}";
            return false;
        }

        if (!File.Exists(resolved))
        {
            if (!FileHelpers.IsImageFile(resolved) &&
                !string.Equals(Path.GetExtension(resolved), ".webp", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"File not found: {resolved}";
                return false;
            }

            canonicalPath = resolved;
            errorCode = string.Empty;
            return true;
        }

        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(resolved);
        }
        catch (Exception ex)
        {
            errorMessage = $"Unable to inspect path: {ex.Message}";
            return false;
        }

        if ((attributes & FileAttributes.Directory) != 0 ||
            (attributes & FileAttributes.Device) != 0)
        {
            errorMessage = "Path is not a regular file.";
            return false;
        }

        if (!FileHelpers.IsImageFile(resolved) && !string.Equals(Path.GetExtension(resolved), ".webp", StringComparison.OrdinalIgnoreCase))
        {
            errorCode = CliErrorCodes.UnsupportedType;
            errorMessage = "File is not a supported image type.";
            return false;
        }

        canonicalPath = resolved;
        errorCode = string.Empty;
        return true;
    }

    /// <summary>Any regular file, for uploads that name their destination.</summary>
    internal static bool TryValidateFilePath(string? path, out string canonicalPath, out string errorCode, out string errorMessage)
    {
        canonicalPath = string.Empty;
        errorCode = CliErrorCodes.InvalidPath;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            errorCode = CliErrorCodes.Usage;
            errorMessage = "Specify a file path after --.";
            return false;
        }

        try
        {
            string fullPath = Path.GetFullPath(path);
            string resolved = File.ResolveLinkTarget(fullPath, returnFinalTarget: true)?.FullName ?? fullPath;
            if (!File.Exists(resolved))
            {
                errorMessage = $"File not found: {resolved}";
                return false;
            }

            if ((File.GetAttributes(resolved) & (FileAttributes.Directory | FileAttributes.Device)) != 0)
            {
                errorMessage = "Path is not a regular file.";
                return false;
            }

            canonicalPath = resolved;
            errorCode = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            errorMessage = $"Unable to inspect path: {ex.Message}";
            return false;
        }
    }

    /// <summary>Longest file name most file systems and object stores accept, in UTF-8 bytes.</summary>
    internal const int MaxFileNameBytes = 255;

    /// <summary>
    /// Evaluates the task's NameFormatPattern (same machinery as the normal upload path: time zone, max length,
    /// invalid file name characters removed by the name parser) without an extension. Falls back to 10 random
    /// alphanumerics when the pattern is empty or evaluates to nothing.
    /// </summary>
    internal static string BuildNamePatternSuffix(TaskSettings settings)
    {
        string text = string.Empty;
        if (!string.IsNullOrWhiteSpace(settings.UploadSettings.NameFormatPattern))
        {
            text = TaskHelpers.GetFileName(settings, string.Empty, new TaskMetadata()).Trim();
        }

        return text.Length > 0 ? text : RandomGenerator.GetRandomAlphanumericString(10);
    }

    private static readonly string[] TarCompressions = [".gz", ".bz2", ".xz", ".zst", ".lz", ".lzma", ".z"];

    /// <summary>
    /// The extension to keep when renaming: ".tar.&lt;x&gt;" (gz, bz2, xz, zst, lz, lzma, Z) as one extension when
    /// the name ends that way (case-insensitive) and has a stem, otherwise whatever
    /// <paramref name="fallback"/> (InstanceManager.GetExtensionToKeep, which knows file type overrides) returns.
    /// </summary>
    internal static string GetExtensionForRename(string path, Func<string?, string> fallback)
    {
        string name = Path.GetFileName(path);
        foreach (string compression in TarCompressions)
        {
            string tail = ".tar" + compression;
            if (name.Length > tail.Length && name.EndsWith(tail, StringComparison.OrdinalIgnoreCase))
            {
                return name[^tail.Length..];
            }
        }

        return fallback(path);
    }

    /// <summary>
    /// <c>stem + "_" + suffix + extension</c>. <paramref name="extension"/> includes its dot (".tar.gz") and is
    /// ignored when it is the whole name (a dot-file such as ".env" is all stem). If the result would exceed 255
    /// bytes the stem is shortened, never the suffix or extension.
    /// </summary>
    internal static string WithNamePattern(string fileName, string extension, string suffix)
    {
        string ext = extension ?? string.Empty;
        string stem = fileName;
        if (ext.Length > 0 && fileName.Length > ext.Length && fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
        {
            stem = fileName[..^ext.Length];
        }
        else
        {
            ext = string.Empty;
        }

        string tail = "_" + suffix + ext;
        int tailBytes = Encoding.UTF8.GetByteCount(tail);
        if (tailBytes > MaxFileNameBytes)
        {
            // Not even an empty stem fits: keep the extension, shorten the suffix.
            int suffixBudget = MaxFileNameBytes - 1 - Encoding.UTF8.GetByteCount(ext);
            if (suffixBudget < 0)
            {
                return TrimToBytes(ext, MaxFileNameBytes);
            }

            return "_" + TrimToBytes(suffix, suffixBudget) + ext;
        }

        int budget = MaxFileNameBytes - Encoding.UTF8.GetByteCount(tail);
        return TrimToBytes(stem, budget) + tail;
    }

    private static string TrimToBytes(string text, int maxBytes)
    {
        while (text.Length > 0 && Encoding.UTF8.GetByteCount(text) > maxBytes)
        {
            int cut = text.Length - 1;
            if (cut > 0 && char.IsLowSurrogate(text[cut]) && char.IsHighSurrogate(text[cut - 1]))
            {
                cut--;
            }

            text = text[..cut];
        }

        return text;
    }

    internal static async Task<int> UploadToDestinationAsync(string? path, string destination, bool keepName = false)
    {
        if (!TryValidateFilePath(path, out string canonicalPath, out string errorCode, out string errorMessage))
        {
            return JsonStdout.WriteFailureAndExit(errorCode, errorMessage);
        }

        try
        {
            await EnsureBootstrapped();
            var usable = GetFileDestinations();
            var matches = UploadHost.MatchDestination(usable, destination);
            if (matches.Count == 0)
            {
                return JsonStdout.WriteFailureAndExit(
                    CliErrorCodes.NotFound,
                    $"No usable File destination matches \"{destination}\". Available: {string.Join(", ", usable.Select(i => i.DisplayName))}.");
            }

            if (matches.Count > 1)
            {
                return JsonStdout.WriteFailureAndExit(
                    CliErrorCodes.Ambiguous,
                    $"\"{destination}\" matches more than one File destination. Use an instance id: {string.Join(", ", matches.Select(i => $"{i.DisplayName} ({i.InstanceId})"))}.");
            }

            var instance = matches[0];
            var workflow = SettingsManager.GetFirstWorkflowOrDefault(WorkflowType.FileUpload);
            var taskInfo = new TaskInfo(CreateUploadTaskSettings(workflow.TaskSettings, instance))
            {
                DataType = EDataType.File,
                Job = TaskJob.FileUpload,
                FilePath = canonicalPath,
                KeepSourceFileName = true
            };

            string sourceName = Path.GetFileName(canonicalPath);
            string uploadName = sourceName;
            if (!keepName)
            {
                // KeepSourceFileName stays true so the processor does not replace the name with the bare
                // pattern; the name is set here, the same way ApplyNamingConvention sets it.
                string extension = GetExtensionForRename(canonicalPath, InstanceManager.Instance.GetExtensionToKeep);
                uploadName = WithNamePattern(sourceName, extension, BuildNamePatternSuffix(workflow.TaskSettings));
                taskInfo.SetFileName(uploadName);
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            try
            {
                await ProcessUploadAsync(taskInfo, cts.Token);
            }
            catch (Exception ex)
            {
                var mapped = ErrorMapper.FromException(ex, timeoutRequested: cts.IsCancellationRequested);
                return JsonStdout.WriteFailureAndExit(mapped.Code, mapped.Message);
            }

            string? url = taskInfo.Result?.URL;
            if (taskInfo.Result?.IsSuccess != true || !ErrorMapper.IsHttpUrl(url))
            {
                var mapped = ErrorMapper.FromUploadResult(taskInfo.Result);
                return JsonStdout.WriteFailureAndExit(mapped.Code, mapped.Message);
            }

            JsonStdout.Write(new UploadSuccessResponse
            {
                SchemaVersion = 1,
                Ok = true,
                Url = url!,
                Filename = string.IsNullOrEmpty(taskInfo.FileName) ? uploadName : taskInfo.FileName,
                SourceFilename = sourceName,
                Size = new FileInfo(canonicalPath).Length,
                Type = MimeTypes.GetMimeTypeFromFileName(canonicalPath),
                DataType = "file",
                ProviderId = instance.ProviderId,
                InstanceId = instance.InstanceId,
                DisplayName = instance.DisplayName
            });
            return 0;
        }
        catch (Exception ex)
        {
            var mapped = ErrorMapper.FromException(ex);
            return JsonStdout.WriteFailureAndExit(mapped.Code, mapped.Message);
        }
    }

    internal static async Task<int> UploadAsync(string? path)
    {
        if (!TryValidateImagePath(path, out string canonicalPath, out string errorCode, out string errorMessage))
        {
            return JsonStdout.WriteFailureAndExit(errorCode, errorMessage);
        }

        try
        {
            await UploadHost.EnsureBootstrappedAsync();
            var inspection = InspectImage(canonicalPath);
            if (!inspection.Ready || inspection.Instance == null)
            {
                return JsonStdout.WriteFailureAndExit(
                    CliErrorCodes.NotReady,
                    "No usable image or file uploader is configured in XerahS.");
            }

            var workflow = SettingsManager.GetFirstWorkflowOrDefault(WorkflowType.FileUpload);
            var taskSettings = CreateUploadTaskSettings(workflow.TaskSettings, inspection.Instance);

            var taskInfo = new TaskInfo(taskSettings)
            {
                DataType = EDataType.Image,
                Job = TaskJob.FileUpload,
                FilePath = canonicalPath
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            try
            {
                await ProcessUploadAsync(taskInfo, cts.Token);
            }
            catch (Exception ex)
            {
                var mapped = ErrorMapper.FromException(ex, timeoutRequested: cts.IsCancellationRequested);
                return JsonStdout.WriteFailureAndExit(mapped.Code, mapped.Message);
            }

            var url = taskInfo.Result?.URL;
            bool success = taskInfo.Result?.IsSuccess == true || !string.IsNullOrEmpty(url);
            if (!success || !ErrorMapper.IsHttpUrl(url))
            {
                var mapped = ErrorMapper.FromUploadResult(taskInfo.Result);
                if (!success && mapped.Code == CliErrorCodes.Provider && !inspection.Ready)
                {
                    mapped = (CliErrorCodes.NotReady, mapped.Message);
                }

                if (success && !ErrorMapper.IsHttpUrl(url))
                {
                    mapped = (CliErrorCodes.Provider, "Upload did not return an http:// or https:// URL.");
                }

                return JsonStdout.WriteFailureAndExit(mapped.Code, mapped.Message);
            }

            var instance = ResolveUploadedInstance(taskInfo, inspection.Instance);
            var fileInfo = new FileInfo(taskInfo.FilePath);
            JsonStdout.Write(new UploadSuccessResponse
            {
                SchemaVersion = 1,
                Ok = true,
                Url = url!,
                Filename = Path.GetFileName(taskInfo.FilePath),
                Size = fileInfo.Length,
                Type = GetContentType(taskInfo.FilePath),
                DataType = "image",
                ProviderId = instance?.ProviderId,
                InstanceId = instance?.InstanceId,
                DisplayName = instance?.DisplayName ?? taskInfo.UploaderHost
            });
            return 0;
        }
        catch (Exception ex)
        {
            var mapped = ErrorMapper.FromException(ex);
            return JsonStdout.WriteFailureAndExit(mapped.Code, mapped.Message);
        }
    }

    private static UploaderInstance? ResolveUploadedInstance(TaskInfo taskInfo, UploaderInstance fallback)
    {
        string? host = taskInfo.UploaderHost;
        if (!string.IsNullOrWhiteSpace(host))
        {
            var match = UploadHost.GetUsableImageUploadInstances()
                .FirstOrDefault(i => string.Equals(i.DisplayName, host, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }
        }

        return fallback;
    }

    internal static string GetContentType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tif" or ".tiff" => "image/tiff",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream"
        };
    }

    internal static TaskSettings CreateUploadTaskSettings(TaskSettings source, UploaderInstance instance)
    {
        var settings = CloneTaskSettings(source);
        settings.Job = WorkflowType.FileUpload;
        settings.AfterCaptureJob = AfterCaptureTasks.None;
        settings.AfterUploadJob = AfterUploadTasks.None;
        settings.DestinationInstanceId = instance.InstanceId;
        // OmaXerahs reports this destination before uploading (and its inspection already applies file
        // type overrides), so it is the destination for this one upload.
        settings.DestinationChosenForThisUpload = true;
        settings.AllowCrossCategoryFallback = false;
        settings.RoutedImageFileDestinationId = instance.Category == UploaderCategory.File ? instance.InstanceId : null;
        settings.GeneralSettings.ShowToastNotificationAfterTaskCompleted = false;
        return settings;
    }

    private static TaskSettings CloneTaskSettings(TaskSettings source)
    {
        var jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        string json = JsonConvert.SerializeObject(source, jsonSettings);
        return JsonConvert.DeserializeObject<TaskSettings>(json, jsonSettings) ?? new TaskSettings();
    }
}
