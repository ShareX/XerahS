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
using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.Editor;
using Common = XerahS.Common;
using XerahS.Core;
using XerahS.RegionCapture.ScreenRecording;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using XerahS.UI.Views;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels
{
    public partial class TaskSettingsViewModel : ObservableObject
    {
        private TaskSettings _settings;
        private EditorCore _effectsEditorCore;

        public ImageEffectsViewModel ImageEffects { get; private set; }

        public TaskSettingsViewModel(TaskSettings settings) : this(settings, null) { }

        public TaskSettingsViewModel(TaskSettings settings, EditorCore? editorCore)
        {
            _settings = settings;
            _effectsEditorCore = editorCore ?? new EditorCore();
            ImageEffects = new ImageEffectsViewModel(Model.ImageSettings, _effectsEditorCore);
            ImageEffects.UpdatePreview();
            RefreshFFmpegState();
        }

        public IEnumerable<EImageFormat> ImageFormats => Enum.GetValues(typeof(EImageFormat)).Cast<EImageFormat>();
        public IEnumerable<ContentPlacement> ContentAlignments => Enum.GetValues(typeof(ContentPlacement)).Cast<ContentPlacement>();
        public IEnumerable<ToastClickAction> ToastClickActions => Enum.GetValues(typeof(ToastClickAction)).Cast<ToastClickAction>();
        public IEnumerable<IndexerOutput> IndexerOutputs => Enum.GetValues(typeof(IndexerOutput)).Cast<IndexerOutput>();

        // Expose underlying model if needed
        public TaskSettings Model => _settings;

        public WorkflowType Job
        {
            get => _settings.Job;
            set
            {
                if (_settings.Job != value)
                {
                    _settings.Job = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsIndexFolderJob));
                }
            }
        }

        public bool IsIndexFolderJob => _settings.Job == WorkflowType.IndexFolder;

        #region Capture Settings

        public bool UseModernCapture
        {
            get => _settings.CaptureSettings.UseModernCapture;
            set
            {
                if (_settings.CaptureSettings.UseModernCapture != value)
                {
                    _settings.CaptureSettings.UseModernCapture = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowCursor
        {
            get => _settings.CaptureSettings.ShowCursor;
            set
            {
                if (_settings.CaptureSettings.ShowCursor != value)
                {
                    _settings.CaptureSettings.ShowCursor = value;
                    OnPropertyChanged();
                }
            }
        }

        public decimal ScreenshotDelay
        {
            get => _settings.CaptureSettings.ScreenshotDelay;
            set
            {
                if (_settings.CaptureSettings.ScreenshotDelay != value)
                {
                    _settings.CaptureSettings.ScreenshotDelay = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CaptureTransparent
        {
            get => _settings.CaptureSettings.CaptureTransparent;
            set
            {
                if (_settings.CaptureSettings.CaptureTransparent != value)
                {
                    _settings.CaptureSettings.CaptureTransparent = value;
                    // Shadow depends on transparent often, but UI handles enabling.
                    OnPropertyChanged();
                }
            }
        }

        public bool CaptureShadow
        {
            get => _settings.CaptureSettings.CaptureShadow;
            set
            {
                if (_settings.CaptureSettings.CaptureShadow != value)
                {
                    _settings.CaptureSettings.CaptureShadow = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CaptureClientArea
        {
            get => _settings.CaptureSettings.CaptureClientArea;
            set
            {
                if (_settings.CaptureSettings.CaptureClientArea != value)
                {
                    _settings.CaptureSettings.CaptureClientArea = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ScreenRecordFPS
        {
            get => _settings.CaptureSettings.ScreenRecordFPS;
            set
            {
                if (_settings.CaptureSettings.ScreenRecordFPS != value)
                {
                    _settings.CaptureSettings.ScreenRecordFPS = value;
                    OnPropertyChanged();
                }
            }
        }

        public float ScreenRecordDuration
        {
            get => _settings.CaptureSettings.ScreenRecordDuration;
            set
            {
                if (Math.Abs(_settings.CaptureSettings.ScreenRecordDuration - value) > 0.001f)
                {
                    _settings.CaptureSettings.ScreenRecordDuration = value;
                    OnPropertyChanged();
                }
            }
        }

        public float ScreenRecordStartDelay
        {
            get => _settings.CaptureSettings.ScreenRecordStartDelay;
            set
            {
                if (Math.Abs(_settings.CaptureSettings.ScreenRecordStartDelay - value) > 0.001f)
                {
                    _settings.CaptureSettings.ScreenRecordStartDelay = value;
                    OnPropertyChanged();
                }
            }
        }

        public IEnumerable<RecordingIntent> RecordingIntents => Enum.GetValues(typeof(RecordingIntent)).Cast<RecordingIntent>();

        public RecordingIntent RecordingIntent
        {
            get => _settings.CaptureSettings.ScreenRecordingSettings.RecordingIntent;
            set
            {
                if (_settings.CaptureSettings.ScreenRecordingSettings.RecordingIntent != value)
                {
                    _settings.CaptureSettings.ScreenRecordingSettings.RecordingIntent = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CaptureAutoHideTaskbar
        {
            get => _settings.CaptureSettings.CaptureAutoHideTaskbar;
            set
            {
                if (_settings.CaptureSettings.CaptureAutoHideTaskbar != value)
                {
                    _settings.CaptureSettings.CaptureAutoHideTaskbar = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CaptureCustomWindow
        {
            get => _settings.CaptureSettings.CaptureCustomWindow;
            set
            {
                if (_settings.CaptureSettings.CaptureCustomWindow != value)
                {
                    XerahS.Common.DebugHelper.WriteLine($"[DEBUG] Setting CaptureCustomWindow to: '{value}'");
                    _settings.CaptureSettings.CaptureCustomWindow = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region FFmpeg Options

        private string _ffmpegStatusText = "Missing.";
        private string _detectedFFmpegPath = string.Empty;
        private bool _isDownloadingFFmpeg;
        private double _ffmpegDownloadProgress;

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
                }
                else
                {
                    FFmpegStatusText = result.ErrorMessage ?? "FFmpeg download failed.";
                }
            }
            catch (Exception ex)
            {
                IsDownloadingFFmpeg = false;
                Common.DebugHelper.WriteException(ex, "FFmpeg download failed.");
                FFmpegStatusText = "FFmpeg download failed.";
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

        #region Upload / File Naming

        public string NameFormatPattern
        {
            get => _settings.UploadSettings.NameFormatPattern;
            set
            {
                if (_settings.UploadSettings.NameFormatPattern != value)
                {
                    _settings.UploadSettings.NameFormatPattern = value;
                    OnPropertyChanged();
                }
            }
        }

        public string NameFormatPatternActiveWindow
        {
            get => _settings.UploadSettings.NameFormatPatternActiveWindow;
            set
            {
                if (_settings.UploadSettings.NameFormatPatternActiveWindow != value)
                {
                    _settings.UploadSettings.NameFormatPatternActiveWindow = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool FileUploadUseNamePattern
        {
            get => _settings.UploadSettings.FileUploadUseNamePattern;
            set
            {
                if (_settings.UploadSettings.FileUploadUseNamePattern != value)
                {
                    _settings.UploadSettings.FileUploadUseNamePattern = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool FileUploadReplaceProblematicCharacters
        {
            get => _settings.UploadSettings.FileUploadReplaceProblematicCharacters;
            set
            {
                if (_settings.UploadSettings.FileUploadReplaceProblematicCharacters != value)
                {
                    _settings.UploadSettings.FileUploadReplaceProblematicCharacters = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool URLRegexReplace
        {
            get => _settings.UploadSettings.URLRegexReplace;
            set
            {
                if (_settings.UploadSettings.URLRegexReplace != value)
                {
                    _settings.UploadSettings.URLRegexReplace = value;
                    OnPropertyChanged();
                }
            }
        }

        public string URLRegexReplacePattern
        {
            get => _settings.UploadSettings.URLRegexReplacePattern;
            set
            {
                if (_settings.UploadSettings.URLRegexReplacePattern != value)
                {
                    _settings.UploadSettings.URLRegexReplacePattern = value;
                    OnPropertyChanged();
                }
            }
        }

        public string URLRegexReplaceReplacement
        {
            get => _settings.UploadSettings.URLRegexReplaceReplacement;
            set
            {
                if (_settings.UploadSettings.URLRegexReplaceReplacement != value)
                {
                    _settings.UploadSettings.URLRegexReplaceReplacement = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ClipboardUploadURLContents
        {
            get => _settings.UploadSettings.ClipboardUploadURLContents;
            set
            {
                if (_settings.UploadSettings.ClipboardUploadURLContents != value)
                {
                    _settings.UploadSettings.ClipboardUploadURLContents = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ClipboardUploadShortenURL
        {
            get => _settings.UploadSettings.ClipboardUploadShortenURL;
            set
            {
                if (_settings.UploadSettings.ClipboardUploadShortenURL != value)
                {
                    _settings.UploadSettings.ClipboardUploadShortenURL = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region After Capture Tasks

        public bool SaveImageToFile
        {
            get => _settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.SaveImageToFile);
            set
            {
                if (SaveImageToFile != value)
                {
                    UpdateAfterCaptureTask(AfterCaptureTasks.SaveImageToFile, value);
                    OnPropertyChanged();
                }
            }
        }

        public bool CopyImageToClipboard
        {
            get => _settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyImageToClipboard);
            set
            {
                if (CopyImageToClipboard != value)
                {
                    UpdateAfterCaptureTask(AfterCaptureTasks.CopyImageToClipboard, value);
                    OnPropertyChanged();
                }
            }
        }

        public bool UploadImageToHost
        {
            get => _settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost);
            set
            {
                if (UploadImageToHost != value)
                {
                    UpdateAfterCaptureTask(AfterCaptureTasks.UploadImageToHost, value);
                    OnPropertyChanged();
                }
            }
        }

        public bool AnnotateImage
        {
            get => _settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AnnotateImage);
            set
            {
                if (AnnotateImage != value)
                {
                    UpdateAfterCaptureTask(AfterCaptureTasks.AnnotateImage, value);
                    OnPropertyChanged();
                }
            }
        }

        public bool ApplyImageEffects
        {
            get => _settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AddImageEffects);
            set
            {
                if (ApplyImageEffects != value)
                {
                    UpdateAfterCaptureTask(AfterCaptureTasks.AddImageEffects, value);
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowAfterCaptureWindow
        {
            get => _settings.AfterCaptureJob.HasFlag(AfterCaptureTasks.ShowAfterCaptureWindow);
            set
            {
                if (ShowAfterCaptureWindow != value)
                {
                    UpdateAfterCaptureTask(AfterCaptureTasks.ShowAfterCaptureWindow, value);
                    OnPropertyChanged();
                }
            }
        }

        private void UpdateAfterCaptureTask(AfterCaptureTasks task, bool enabled)
        {
            if (enabled)
                _settings.AfterCaptureJob |= task;
            else
                _settings.AfterCaptureJob &= ~task;
        }

        #endregion

        #region After Upload Tasks

        public bool CopyURLToClipboard
        {
            get => _settings.AfterUploadJob.HasFlag(AfterUploadTasks.CopyURLToClipboard);
            set
            {
                if (CopyURLToClipboard != value)
                {
                    UpdateAfterUploadTask(AfterUploadTasks.CopyURLToClipboard, value);
                    OnPropertyChanged();
                }
            }
        }

        public bool UseURLShortener
        {
            get => _settings.AfterUploadJob.HasFlag(AfterUploadTasks.UseURLShortener);
            set
            {
                if (UseURLShortener != value)
                {
                    UpdateAfterUploadTask(AfterUploadTasks.UseURLShortener, value);
                    OnPropertyChanged();
                }
            }
        }

        public bool ShareURL
        {
            get => _settings.AfterUploadJob.HasFlag(AfterUploadTasks.ShareURL);
            set
            {
                if (ShareURL != value)
                {
                    UpdateAfterUploadTask(AfterUploadTasks.ShareURL, value);
                    OnPropertyChanged();
                }
            }
        }

        private void UpdateAfterUploadTask(AfterUploadTasks task, bool enabled)
        {
            if (enabled)
                _settings.AfterUploadJob |= task;
            else
                _settings.AfterUploadJob &= ~task;
        }

        #endregion

        #region General (Forwarded from TaskSettingsGeneral)

        public bool PlaySoundAfterCapture
        {
            get => _settings.GeneralSettings.PlaySoundAfterCapture;
            set
            {
                if (_settings.GeneralSettings.PlaySoundAfterCapture != value)
                {
                    _settings.GeneralSettings.PlaySoundAfterCapture = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowToastNotification
        {
            get => _settings.GeneralSettings.ShowToastNotificationAfterTaskCompleted;
            set
            {
                if (_settings.GeneralSettings.ShowToastNotificationAfterTaskCompleted != value)
                {
                    _settings.GeneralSettings.ShowToastNotificationAfterTaskCompleted = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool PlaySoundAfterUpload
        {
            get => _settings.GeneralSettings.PlaySoundAfterUpload;
            set
            {
                if (_settings.GeneralSettings.PlaySoundAfterUpload != value)
                {
                    _settings.GeneralSettings.PlaySoundAfterUpload = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool PlaySoundAfterAction
        {
            get => _settings.GeneralSettings.PlaySoundAfterAction;
            set
            {
                if (_settings.GeneralSettings.PlaySoundAfterAction != value)
                {
                    _settings.GeneralSettings.PlaySoundAfterAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool UseCustomCaptureSound
        {
            get => _settings.GeneralSettings.UseCustomCaptureSound;
            set
            {
                if (_settings.GeneralSettings.UseCustomCaptureSound != value)
                {
                    _settings.GeneralSettings.UseCustomCaptureSound = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CustomCaptureSoundPath
        {
            get => _settings.GeneralSettings.CustomCaptureSoundPath;
            set
            {
                if (_settings.GeneralSettings.CustomCaptureSoundPath != value)
                {
                    _settings.GeneralSettings.CustomCaptureSoundPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public float ToastWindowDuration
        {
            get => _settings.GeneralSettings.ToastWindowDuration;
            set
            {
                if (Math.Abs(_settings.GeneralSettings.ToastWindowDuration - value) > 0.001f)
                {
                    _settings.GeneralSettings.ToastWindowDuration = value;
                    OnPropertyChanged();
                }
            }
        }

        public float ToastWindowFadeDuration
        {
            get => _settings.GeneralSettings.ToastWindowFadeDuration;
            set
            {
                if (Math.Abs(_settings.GeneralSettings.ToastWindowFadeDuration - value) > 0.001f)
                {
                    _settings.GeneralSettings.ToastWindowFadeDuration = value;
                    OnPropertyChanged();
                }
            }
        }

        public ContentPlacement ToastWindowPlacement
        {
            get => _settings.GeneralSettings.ToastWindowPlacement;
            set
            {
                if (_settings.GeneralSettings.ToastWindowPlacement != value)
                {
                    _settings.GeneralSettings.ToastWindowPlacement = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ToastWindowWidth
        {
            get => _settings.GeneralSettings.ToastWindowSize.Width;
            set
            {
                if (_settings.GeneralSettings.ToastWindowSize.Width != value)
                {
                    _settings.GeneralSettings.ToastWindowSize = new SizeI(value, _settings.GeneralSettings.ToastWindowSize.Height);
                    OnPropertyChanged();
                }
            }
        }

        public int ToastWindowHeight
        {
            get => _settings.GeneralSettings.ToastWindowSize.Height;
            set
            {
                if (_settings.GeneralSettings.ToastWindowSize.Height != value)
                {
                    _settings.GeneralSettings.ToastWindowSize = new SizeI(_settings.GeneralSettings.ToastWindowSize.Width, value);
                    OnPropertyChanged();
                }
            }
        }

        public ToastClickAction ToastWindowLeftClickAction
        {
            get => _settings.GeneralSettings.ToastWindowLeftClickAction;
            set
            {
                if (_settings.GeneralSettings.ToastWindowLeftClickAction != value)
                {
                    _settings.GeneralSettings.ToastWindowLeftClickAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public ToastClickAction ToastWindowRightClickAction
        {
            get => _settings.GeneralSettings.ToastWindowRightClickAction;
            set
            {
                if (_settings.GeneralSettings.ToastWindowRightClickAction != value)
                {
                    _settings.GeneralSettings.ToastWindowRightClickAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public ToastClickAction ToastWindowMiddleClickAction
        {
            get => _settings.GeneralSettings.ToastWindowMiddleClickAction;
            set
            {
                if (_settings.GeneralSettings.ToastWindowMiddleClickAction != value)
                {
                    _settings.GeneralSettings.ToastWindowMiddleClickAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ToastWindowAutoHide
        {
            get => _settings.GeneralSettings.ToastWindowAutoHide;
            set
            {
                if (_settings.GeneralSettings.ToastWindowAutoHide != value)
                {
                    _settings.GeneralSettings.ToastWindowAutoHide = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Index Folder Commands

        [RelayCommand]
        private async Task BrowseIndexerFolderAsync()
        {
            var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (window?.StorageProvider == null)
            {
                return;
            }

            var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Folder to Index",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                IndexerFolderPath = folders[0].Path.LocalPath;
            }
        }

        [RelayCommand]
        private async Task BrowseIndexerCssFileAsync()
        {
            var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (window?.StorageProvider == null)
            {
                return;
            }

            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Custom CSS File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("CSS Files") { Patterns = new[] { "*.css" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count > 0)
            {
                IndexerCustomCssFilePath = files[0].Path.LocalPath;
            }
        }

        #endregion

        #region Image Settings

        public EImageFormat ImageFormat
        {
            get => _settings.ImageSettings.ImageFormat;
            set
            {
                if (_settings.ImageSettings.ImageFormat != value)
                {
                    _settings.ImageSettings.ImageFormat = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ImageJPEGQuality
        {
            get => _settings.ImageSettings.ImageJPEGQuality;
            set
            {
                if (_settings.ImageSettings.ImageJPEGQuality != value)
                {
                    _settings.ImageSettings.ImageJPEGQuality = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ThumbnailWidth
        {
            get => _settings.ImageSettings.ThumbnailWidth;
            set
            {
                if (_settings.ImageSettings.ThumbnailWidth != value)
                {
                    _settings.ImageSettings.ThumbnailWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ThumbnailHeight
        {
            get => _settings.ImageSettings.ThumbnailHeight;
            set
            {
                if (_settings.ImageSettings.ThumbnailHeight != value)
                {
                    _settings.ImageSettings.ThumbnailHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ThumbnailName
        {
            get => _settings.ImageSettings.ThumbnailName;
            set
            {
                if (_settings.ImageSettings.ThumbnailName != value)
                {
                    _settings.ImageSettings.ThumbnailName = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ThumbnailCheckSize
        {
            get => _settings.ImageSettings.ThumbnailCheckSize;
            set
            {
                if (_settings.ImageSettings.ThumbnailCheckSize != value)
                {
                    _settings.ImageSettings.ThumbnailCheckSize = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Index Folder Settings

        public string IndexerFolderPath
        {
            get => _settings.ToolsSettings.IndexerFolderPath;
            set
            {
                if (_settings.ToolsSettings.IndexerFolderPath != value)
                {
                    _settings.ToolsSettings.IndexerFolderPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public IndexerOutput IndexerOutput
        {
            get => _settings.ToolsSettings.IndexerSettings.Output;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.Output != value)
                {
                    _settings.ToolsSettings.IndexerSettings.Output = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerSkipHiddenFolders
        {
            get => _settings.ToolsSettings.IndexerSettings.SkipHiddenFolders;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.SkipHiddenFolders != value)
                {
                    _settings.ToolsSettings.IndexerSettings.SkipHiddenFolders = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerSkipHiddenFiles
        {
            get => _settings.ToolsSettings.IndexerSettings.SkipHiddenFiles;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.SkipHiddenFiles != value)
                {
                    _settings.ToolsSettings.IndexerSettings.SkipHiddenFiles = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerSkipFiles
        {
            get => _settings.ToolsSettings.IndexerSettings.SkipFiles;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.SkipFiles != value)
                {
                    _settings.ToolsSettings.IndexerSettings.SkipFiles = value;
                    OnPropertyChanged();
                }
            }
        }

        public int IndexerMaxDepthLevel
        {
            get => _settings.ToolsSettings.IndexerSettings.MaxDepthLevel;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.MaxDepthLevel != value)
                {
                    _settings.ToolsSettings.IndexerSettings.MaxDepthLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerShowSizeInfo
        {
            get => _settings.ToolsSettings.IndexerSettings.ShowSizeInfo;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.ShowSizeInfo != value)
                {
                    _settings.ToolsSettings.IndexerSettings.ShowSizeInfo = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerAddFooter
        {
            get => _settings.ToolsSettings.IndexerSettings.AddFooter;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.AddFooter != value)
                {
                    _settings.ToolsSettings.IndexerSettings.AddFooter = value;
                    OnPropertyChanged();
                }
            }
        }

        public string IndexerIndentationText
        {
            get => _settings.ToolsSettings.IndexerSettings.IndentationText;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.IndentationText != value)
                {
                    _settings.ToolsSettings.IndexerSettings.IndentationText = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerAddEmptyLineAfterFolders
        {
            get => _settings.ToolsSettings.IndexerSettings.AddEmptyLineAfterFolders;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.AddEmptyLineAfterFolders != value)
                {
                    _settings.ToolsSettings.IndexerSettings.AddEmptyLineAfterFolders = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerUseCustomCssFile
        {
            get => _settings.ToolsSettings.IndexerSettings.UseCustomCSSFile;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.UseCustomCSSFile != value)
                {
                    _settings.ToolsSettings.IndexerSettings.UseCustomCSSFile = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerDisplayPath
        {
            get => _settings.ToolsSettings.IndexerSettings.DisplayPath;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.DisplayPath != value)
                {
                    _settings.ToolsSettings.IndexerSettings.DisplayPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerDisplayPathLimited
        {
            get => _settings.ToolsSettings.IndexerSettings.DisplayPathLimited;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.DisplayPathLimited != value)
                {
                    _settings.ToolsSettings.IndexerSettings.DisplayPathLimited = value;
                    OnPropertyChanged();
                }
            }
        }

        public string IndexerCustomCssFilePath
        {
            get => _settings.ToolsSettings.IndexerSettings.CustomCSSFilePath;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.CustomCSSFilePath != value)
                {
                    _settings.ToolsSettings.IndexerSettings.CustomCSSFilePath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerUseAttribute
        {
            get => _settings.ToolsSettings.IndexerSettings.UseAttribute;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.UseAttribute != value)
                {
                    _settings.ToolsSettings.IndexerSettings.UseAttribute = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerCreateParseableJson
        {
            get => _settings.ToolsSettings.IndexerSettings.CreateParseableJson;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.CreateParseableJson != value)
                {
                    _settings.ToolsSettings.IndexerSettings.CreateParseableJson = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerBinaryUnits
        {
            get => _settings.ToolsSettings.IndexerSettings.BinaryUnits;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.BinaryUnits != value)
                {
                    _settings.ToolsSettings.IndexerSettings.BinaryUnits = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion
    }
}
