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

using System.Runtime.Loader;
using XerahS.Bootstrap;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Platform.Abstractions;
using XerahS.WatchFolder.Daemon.Services;

#if WINDOWS
using System.ServiceProcess;
#endif

namespace XerahS.WatchFolder.Daemon;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            DaemonOptions options = DaemonOptions.Parse(args);

            if (options.Scope == WatchFolderDaemonScope.System && string.IsNullOrWhiteSpace(options.SettingsFolder))
            {
                Console.Error.WriteLine("--settings-folder is required for system scope.");
                return 2;
            }

#if WINDOWS
            if (options.RunAsService && OperatingSystem.IsWindows())
            {
                ServiceBase.Run(new WindowsWatchFolderService(options));
                return 0;
            }
#endif

            return RunConsoleAsync(options).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Watch folder daemon failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunConsoleAsync(DaemonOptions options)
    {
        using var shutdownCts = new CancellationTokenSource();
        RegisterShutdownHandlers(shutdownCts);
        return await RunDaemonAsync(options, shutdownCts.Token);
    }

    private static void RegisterShutdownHandlers(CancellationTokenSource shutdownCts)
    {
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdownCts.Cancel();
        };

        AssemblyLoadContext.Default.Unloading += _ =>
        {
            shutdownCts.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            shutdownCts.Cancel();
        };
    }

    private static async Task<int> RunDaemonAsync(DaemonOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.SettingsFolder))
        {
            SettingsManager.PersonalFolder = options.SettingsFolder;
        }

        var bootstrapOptions = new BootstrapOptions
        {
            EnableLogging = true,
            InitializeRecording = false,
            LogPath = BuildDaemonLogPath(),
            UIService = new HeadlessUIService(),
            ToastService = new HeadlessToastService()
        };

        BootstrapResult bootstrap = await ShareXBootstrap.InitializeAsync(bootstrapOptions);
        if (!bootstrap.ConfigurationLoaded || !bootstrap.PlatformServicesInitialized)
        {
            Console.Error.WriteLine("Failed to initialize daemon runtime.");
            return 1;
        }

        WatchFolderManager.Instance.StartOrReloadFromCurrentSettings();

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }

        bool stopped = await WatchFolderManager.Instance.StopAsync(TimeSpan.FromSeconds(options.StopTimeoutSeconds));
        return stopped ? 0 : 1;
    }

    private static string BuildDaemonLogPath()
    {
        string baseFolder = SettingsManager.PersonalFolder;
        string logsFolder = Path.Combine(baseFolder, "Logs", DateTime.Now.ToString("yyyy-MM"));
        Directory.CreateDirectory(logsFolder);
        return Path.Combine(logsFolder, $"watchfolder-daemon-{DateTime.Now:yyyyMMdd}.log");
    }

#if WINDOWS
    private sealed class WindowsWatchFolderService : ServiceBase
    {
        private readonly DaemonOptions _options;
        private CancellationTokenSource? _shutdownCts;
        private Task<int>? _runTask;

        public WindowsWatchFolderService(DaemonOptions options)
        {
            _options = options;
            ServiceName = "XerahSWatchFolder";
            CanStop = true;
            CanShutdown = true;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            _shutdownCts = new CancellationTokenSource();
            _runTask = Task.Run(() => RunDaemonAsync(_options, _shutdownCts.Token));
        }

        protected override void OnStop()
        {
            if (_shutdownCts == null)
            {
                return;
            }

            _shutdownCts.Cancel();
            try
            {
                _runTask?.GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        protected override void OnShutdown()
        {
            OnStop();
            base.OnShutdown();
        }
    }
#endif
}
