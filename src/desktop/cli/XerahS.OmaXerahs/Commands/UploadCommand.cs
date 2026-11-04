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
using Newtonsoft.Json;
using XerahS.Common;
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
            Description = "File destination: an instance id, a unique id prefix (6+ characters), its display name, or its provider id such as kovaforge. Accepts any file type and keeps the file's name."
        };
        command.Add(pathArgument);
        command.Add(jsonOption);
        command.Add(urlOnlyOption);
        command.Add(destinationOption);
        command.SetAction(parseResult =>
        {
            JsonStdout.UrlOnly = parseResult.GetValue(urlOnlyOption);
            JsonStdout.Enabled = !JsonStdout.UrlOnly && parseResult.GetValue(jsonOption);
            string? destination = parseResult.GetValue(destinationOption);
            return (destination == null
                ? UploadAsync(parseResult.GetValue(pathArgument))
                : UploadToDestinationAsync(parseResult.GetValue(pathArgument), destination)).GetAwaiter().GetResult();
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

    internal static async Task<int> UploadToDestinationAsync(string? path, string destination)
    {
        if (!TryValidateFilePath(path, out string canonicalPath, out string errorCode, out string errorMessage))
        {
            return JsonStdout.WriteFailureAndExit(errorCode, errorMessage);
        }

        try
        {
            await UploadHost.EnsureBootstrappedAsync();
            var usable = UploadHost.GetUsableFileInstances();
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
                Filename = Path.GetFileName(canonicalPath),
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
