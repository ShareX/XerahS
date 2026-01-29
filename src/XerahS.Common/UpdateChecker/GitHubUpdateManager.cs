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

namespace XerahS.Common
{
    public class GitHubUpdateManager : IDisposable
    {
        public bool AllowAutoUpdate { get; set; } // ConfigureAutoUpdate function must be called after change this
        public bool AutoUpdateEnabled { get; set; } = true;
        public TimeSpan UpdateCheckInterval { get; private set; } = TimeSpan.FromHours(1);
        public string GitHubOwner { get; set; }
        public string GitHubRepo { get; set; }
        public bool IsPortable { get; set; } // If current build is portable then download URL will be opened in browser instead of downloading it
        public bool CheckPreReleaseUpdates { get; set; }

        /// <summary>
        /// Callback invoked when an update is available. Returns true if user accepted the update.
        /// Set this callback to integrate with UI for showing update dialogs.
        /// </summary>
        public Func<UpdateChecker, Task<bool>>? ShowUpdateDialogCallback { get; set; }

        private Timer? updateTimer = null;
        private readonly object updateTimerLock = new object();

        public GitHubUpdateManager()
        {
            GitHubOwner = string.Empty;
            GitHubRepo = string.Empty;
        }

        public GitHubUpdateManager(string owner, string repo, bool portable = false)
        {
            GitHubOwner = owner;
            GitHubRepo = repo;
            IsPortable = portable;
        }

        public void ConfigureAutoUpdate()
        {
            lock (updateTimerLock)
            {
                if (AllowAutoUpdate)
                {
                    if (updateTimer == null)
                    {
                        updateTimer = new Timer(TimerCallback, null, TimeSpan.Zero, UpdateCheckInterval);
                    }
                }
                else
                {
                    Dispose();
                }
            }
        }

        private async void TimerCallback(object? state)
        {
            await CheckUpdate();
        }

        private async Task CheckUpdate()
        {
            if (AutoUpdateEnabled)
            {
                UpdateChecker updateChecker = CreateUpdateChecker();
                await updateChecker.CheckUpdateAsync();

                if (updateChecker.Status == UpdateStatus.UpdateAvailable)
                {
                    DebugHelper.WriteLine($"Update available: {updateChecker.LatestVersion}");

                    if (ShowUpdateDialogCallback != null)
                    {
                        bool userAccepted = await ShowUpdateDialogCallback(updateChecker);
                        if (!userAccepted)
                        {
                            // User declined - disable auto-update for this session
                            AutoUpdateEnabled = false;
                        }
                    }
                }
            }
        }

        public virtual GitHubUpdateChecker CreateUpdateChecker()
        {
            return new GitHubUpdateChecker(GitHubOwner, GitHubRepo)
            {
                IsPortable = IsPortable,
                IncludePreRelease = CheckPreReleaseUpdates
            };
        }

        public void Dispose()
        {
            if (updateTimer != null)
            {
                updateTimer.Dispose();
                updateTimer = null;
            }
        }
    }
}
