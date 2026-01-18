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

namespace XerahS.Core.Managers
{
    public class WatchFolderManager : IDisposable
    {
        private static readonly Lazy<WatchFolderManager> _lazy = new(() => new WatchFolderManager());
        public static WatchFolderManager Instance => _lazy.Value;

        private readonly List<FileSystemWatcher> _watchers = new();
        private bool _isDisposed;

        private WatchFolderManager()
        {
        }

        public void UpdateWatchers()
        {
            StopWatchers();

            // TaskSettings access via SettingManager would be needed here
            var settings = SettingsManager.GetFirstWorkflow(HotkeyType.None)?.TaskSettings;

            if (settings != null && settings.WatchFolderEnabled)
            {
                foreach (var folder in settings.WatchFolderList)
                {
                    if (Directory.Exists(folder.FolderPath))
                    {
                        AddWatcher(folder);
                    }
                }
            }
        }

        private void AddWatcher(WatchFolderSettings settings)
        {
            try
            {
                var watcher = new FileSystemWatcher(settings.FolderPath);
                watcher.Filter = settings.Filter;
                watcher.IncludeSubdirectories = settings.IncludeSubdirectories;
                watcher.EnableRaisingEvents = true;
                watcher.Created += Watcher_Created;
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to watch folder {settings.FolderPath}: {ex.Message}");
            }
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            // TODO: Trigger Task execution for the new file
            DebugHelper.WriteLine($"New file detected: {e.FullPath}");

            // Logic to create a WorkerTask from the file would go here.
            // Since we are decoupling, we might fire an event or call TaskManager directly.
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
