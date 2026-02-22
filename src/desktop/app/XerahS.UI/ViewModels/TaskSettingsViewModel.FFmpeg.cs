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
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.UI.Views;

namespace XerahS.UI.ViewModels
{
    public partial class TaskSettingsViewModel
    {
        #region FFmpeg Options

        private string _ffmpegStatusText = "Missing.";
        private string _detectedFFmpegPath = string.Empty;
        private bool _isDownloadingFFmpeg;
        private double _ffmpegDownloadProgress;
        private string _expectedFFmpegDownloadUrl = string.Empty;
        private string _linuxRecordingDiagnosticsStatusText = string.Empty;

        public string FFmpegStatusText
        {
            get => _ffmpegStatusText;
            private set
            {
                if (_ffmpegStatusText != value)
                {
                    _ffmpegStatusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DetectedFFmpegPath
        {
            get => _detectedFFmpegPath;
            private set
            {
                if (_detectedFFmpegPath != value)
                {
                    _detectedFFmpegPath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsFFmpegDetected));
                    OnPropertyChanged(nameof(IsFFmpegMissing));
                    OnPropertyChanged(nameof(CanDownloadFFmpeg));
                    OnPropertyChanged(nameof(ShowDownloadFFmpeg));
                }
            }
        }

        public bool IsFFmpegDetected => !string.IsNullOrWhiteSpace(_detectedFFmpegPath);
        public bool IsFFmpegMissing => string.IsNullOrWhiteSpace(_detectedFFmpegPath);

        public bool IsDownloadingFFmpeg
        {
            get => _isDownloadingFFmpeg;
            private set
            {
                if (_isDownloadingFFmpeg != value)
                {
                    _isDownloadingFFmpeg = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanDownloadFFmpeg));
                }
            }
        }

        public double FFmpegDownloadProgress
        {
            get => _ffmpegDownloadProgress;
            private set
            {
                if (Math.Abs(_ffmpegDownloadProgress - value) > 0.001)
                {
                    _ffmpegDownloadProgress = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FFmpegDownloadProgressText));
                }
            }
        }

        public string FFmpegDownloadProgressText => $"{FFmpegDownloadProgress:0}%";

        public bool CanDownloadFFmpeg => IsFFmpegMissing && !IsDownloadingFFmpeg;
        public bool ShowDownloadFFmpeg => IsFFmpegMissing;

        public string ExpectedFFmpegDownloadUrl
        {
            get => _expectedFFmpegDownloadUrl;
            private set
            {
                if (_expectedFFmpegDownloadUrl != value)
                {
                    _expectedFFmpegDownloadUrl = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowExpectedFFmpegDownloadUrl));
                }
            }
        }

        public bool ShowExpectedFFmpegDownloadUrl => !string.IsNullOrWhiteSpace(_expectedFFmpegDownloadUrl);

        public bool IsLinuxPlatform => OperatingSystem.IsLinux();

        public string LinuxRecordingDiagnosticsStatusText
        {
            get => _linuxRecordingDiagnosticsStatusText;
            private set
            {
                if (_linuxRecordingDiagnosticsStatusText != value)
                {
                    _linuxRecordingDiagnosticsStatusText = value;
                    OnPropertyChanged();
                }
            }
        }

        [RelayCommand]
        private async Task OpenFFmpegOptionsAsync()
        {
            TaskSettings taskSettings = _settings;

            if (!string.IsNullOrEmpty(_settings.WorkflowId))
            {
                var workflow = SettingsManager.GetWorkflowById(_settings.WorkflowId);
                if (workflow != null)
                {
                    taskSettings = workflow.TaskSettings;
                }
            }

            var ffmpegOptions = taskSettings.CaptureSettings.FFmpegOptions ?? new FFmpegOptions();
            taskSettings.CaptureSettings.FFmpegOptions = ffmpegOptions;
            var vm = new FFmpegOptionsViewModel(ffmpegOptions);
            var window = new FFmpegOptionsWindow
            {
                DataContext = vm
            };

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                await window.ShowDialog(desktop.MainWindow);
                RefreshFFmpegState();
            }
            else
            {
                window.Closed += (_, _) => RefreshFFmpegState();
                window.Show();
            }
        }

        [RelayCommand]
        private async Task DownloadFFmpegAsync()
        {
            if (IsDownloadingFFmpeg || IsFFmpegDetected)
            {
                return;
            }

            IsDownloadingFFmpeg = true;
            FFmpegDownloadProgress = 0;
            FFmpegStatusText = "Downloading FFmpeg...";
            ExpectedFFmpegDownloadUrl = string.Empty;

            try
            {
                IProgress<double> progress = new Progress<double>(value =>
                {
                    FFmpegDownloadProgress = value;
                    FFmpegStatusText = $"Downloading FFmpeg... {FFmpegDownloadProgressText}";
                });

                Common.FFmpegDownloadResult result = await Common.FFmpegDownloader.DownloadLatestToToolsAsync(progress);

                IsDownloadingFFmpeg = false;

                if (result.Success)
                {
                    RefreshFFmpegState();
                    RefreshOpenFFmpegOptionsWindows();
                    ExpectedFFmpegDownloadUrl = string.Empty;
                }
                else
                {
                    FFmpegStatusText = result.ErrorMessage ?? "FFmpeg download failed.";
                    if (!string.IsNullOrWhiteSpace(result.ExpectedDownloadUrl))
                    {
                        ExpectedFFmpegDownloadUrl = result.ExpectedDownloadUrl;
                    }
                }
            }
            catch (Exception ex)
            {
                IsDownloadingFFmpeg = false;
                Common.DebugHelper.WriteException(ex, "FFmpeg download failed.");
                FFmpegStatusText = "FFmpeg download failed.";
                ExpectedFFmpegDownloadUrl = string.Empty;
            }
        }

        [RelayCommand]
        private async Task RunLinuxRecordingDiagnosticsAsync()
        {
            if (!IsLinuxPlatform)
            {
                return;
            }

            LinuxRecordingDiagnosticsStatusText = "Running Linux recording diagnostics...";

            try
            {
                string reportPath = await Task.Run(() =>
                    PlatformServices.Diagnostic.WriteRecordingDiagnostics(Common.PathsManager.PersonalFolder));

                if (!string.IsNullOrWhiteSpace(reportPath))
                {
                    LinuxRecordingDiagnosticsStatusText = $"Diagnostics report saved: {reportPath}";
                }
                else
                {
                    LinuxRecordingDiagnosticsStatusText = "Diagnostics failed. Unable to write report.";
                }
            }
            catch (Exception ex)
            {
                Common.DebugHelper.WriteException(ex, "Linux recording diagnostics failed.");
                LinuxRecordingDiagnosticsStatusText = "Diagnostics failed. Check logs for details.";
            }
        }

        private void RefreshFFmpegState()
        {
            DetectedFFmpegPath = Common.PathsManager.GetFFmpegPath();
            FFmpegStatusText = BuildFFmpegStatusText();
        }

        private string BuildFFmpegStatusText()
        {
            if (IsDownloadingFFmpeg)
            {
                return $"Downloading FFmpeg... {FFmpegDownloadProgressText}";
            }

            FFmpegOptions? options = _settings.CaptureSettings.FFmpegOptions;

            if (options?.OverrideCLIPath == true && !string.IsNullOrWhiteSpace(options.CLIPath))
            {
                return "Configured manually.";
            }

            if (!string.IsNullOrWhiteSpace(_detectedFFmpegPath))
            {
                return IsInToolsFolder(_detectedFFmpegPath) ? "Downloaded automatically." : "Configured manually.";
            }

            return "Missing.";
        }

        private static bool IsInToolsFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string toolsFolder = Path.GetFullPath(Common.PathsManager.ToolsFolder);

            if (!toolsFolder.EndsWith(Path.DirectorySeparatorChar))
            {
                toolsFolder += Path.DirectorySeparatorChar;
            }

            string fullPath = Path.GetFullPath(path);
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            return fullPath.StartsWith(toolsFolder, comparison);
        }

        private void RefreshOpenFFmpegOptionsWindows()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return;
            }

            foreach (var window in desktop.Windows.OfType<FFmpegOptionsWindow>())
            {
                if (window.DataContext is FFmpegOptionsViewModel vm)
                {
                    vm.RefreshDetectedPath();
                }
            }
        }

        #endregion
    }
}
