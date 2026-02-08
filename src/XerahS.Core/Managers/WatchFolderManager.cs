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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Concurrent;

namespace XerahS.Core.Managers
{
    public class WatchFolderManager : IDisposable
    {
        private static readonly Lazy<WatchFolderManager> _lazy = new(() => new WatchFolderManager());
        public static WatchFolderManager Instance => _lazy.Value;

        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly ConcurrentDictionary<string, byte> _inFlight = new(StringComparer.OrdinalIgnoreCase);
        private bool _isDisposed;

        private WatchFolderManager()
        {
        }

        public void UpdateWatchers()
        {
            StopWatchers();

            // TaskSettings access via SettingManager would be needed here
            var settings = SettingsManager.DefaultTaskSettings;

            if (settings != null && settings.WatchFolderEnabled)
            {
                foreach (var folder in settings.WatchFolderList)
                {
                    if (Directory.Exists(folder.FolderPath))
                    {
                        if (folder.Enabled && IsWorkflowValid(folder))
                        {
                            AddWatchers(folder);
                        }
                    }
                }
            }
        }

        private void AddWatchers(WatchFolderSettings settings)
        {
            foreach (var filter in ParseFilters(settings.Filter))
            {
                try
                {
                    var watcher = new FileSystemWatcher(settings.FolderPath)
                    {
                        Filter = filter,
                        IncludeSubdirectories = settings.IncludeSubdirectories,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite
                    };

                    watcher.Created += (sender, e) => OnFileDetected(settings, e.FullPath);
                    watcher.Renamed += (sender, e) => OnFileDetected(settings, e.FullPath);
                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteLine($"Failed to watch folder {settings.FolderPath}: {ex.Message}");
                }
            }
        }

        private void OnFileDetected(WatchFolderSettings settings, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return;
            }

            if (!_inFlight.TryAdd(fullPath, 0))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessFileAsync(settings, fullPath);
                }
                finally
                {
                    _inFlight.TryRemove(fullPath, out _);
                }
            });
        }

        private async Task ProcessFileAsync(WatchFolderSettings settings, string fullPath)
        {
            if (!settings.Enabled)
            {
                return;
            }

            if (!File.Exists(fullPath))
            {
                return;
            }

            try
            {
                var attributes = File.GetAttributes(fullPath);
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to read attributes for {fullPath}: {ex.Message}");
                return;
            }

            if (!await WaitForFileReadyAsync(fullPath))
            {
                DebugHelper.WriteLine($"WatchFolder: file not ready in time: {fullPath}");
                return;
            }

            var workflow = SettingsManager.GetWorkflowById(settings.WorkflowId);
            if (workflow?.TaskSettings == null)
            {
                DebugHelper.WriteLine($"WatchFolder: workflow not found for file {fullPath}");
                return;
            }

            var clonedSettings = CloneTaskSettings(workflow.TaskSettings);
            clonedSettings.Job = WorkflowType.FileUpload;

            string fileToProcess = fullPath;
            if (settings.MoveFilesToScreenshotsFolder)
            {
                fileToProcess = MoveToScreenshotsFolder(fullPath, clonedSettings);
            }

            await TaskManager.Instance.StartFileTask(clonedSettings, fileToProcess);
        }

        private static string MoveToScreenshotsFolder(string sourcePath, TaskSettings taskSettings)
        {
            try
            {
                string screenshotsFolder = TaskHelpers.GetScreenshotsFolder(taskSettings);
                Directory.CreateDirectory(screenshotsFolder);

                string fileName = Path.GetFileName(sourcePath);
                string targetPath = Path.Combine(screenshotsFolder, fileName);
                targetPath = TaskHelpers.HandleExistsFile(targetPath, taskSettings);

                File.Move(sourcePath, targetPath);
                return targetPath;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to move file to screenshots folder: {ex.Message}");
                return sourcePath;
            }
        }

        private static async Task<bool> WaitForFileReadyAsync(string fullPath, int timeoutMs = 15000, int pollMs = 300)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            long lastSize = -1;
            int stableCount = 0;

            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                if (!File.Exists(fullPath))
                {
                    return false;
                }

                long size;
                try
                {
                    size = new FileInfo(fullPath).Length;
                }
                catch
                {
                    size = -1;
                }

                if (size == lastSize && size > 0 && !IsFileLocked(fullPath))
                {
                    stableCount++;
                    if (stableCount >= 2)
                    {
                        return true;
                    }
                }
                else
                {
                    stableCount = 0;
                }

                lastSize = size;
                await Task.Delay(pollMs);
            }

            return false;
        }

        private static bool IsFileLocked(string fullPath)
        {
            try
            {
                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<string> ParseFilters(string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                yield return "*.*";
                yield break;
            }

            var separators = new[] { ';', '|', ',' };
            var filters = filter.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (filters.Length == 0)
            {
                yield return "*.*";
                yield break;
            }

            foreach (var item in filters)
            {
                yield return string.IsNullOrWhiteSpace(item) ? "*.*" : item;
            }
        }

        private static TaskSettings CloneTaskSettings(TaskSettings source)
        {
            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                Converters = new List<JsonConverter>
                {
                    new StringEnumConverter(),
                    new XerahS.Common.Converters.SkColorJsonConverter()
                }
            };

            string json = JsonConvert.SerializeObject(source, jsonSettings);
            return JsonConvert.DeserializeObject<TaskSettings>(json, jsonSettings) ?? new TaskSettings();
        }

        private static bool IsWorkflowValid(WatchFolderSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.WorkflowId))
            {
                return false;
            }

            return SettingsManager.GetWorkflowById(settings.WorkflowId) != null;
        }

        private void StopWatchers()
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                StopWatchers();
                _isDisposed = true;
            }
        }
    }
}
