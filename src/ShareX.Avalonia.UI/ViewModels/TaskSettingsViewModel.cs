using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using XerahS.Core;
using XerahS.ScreenCapture.ScreenRecording;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using XerahS.UI.Views;

namespace XerahS.UI.ViewModels
{
    public partial class TaskSettingsViewModel : ObservableObject
    {
        private TaskSettings _settings;

        public ImageEffectsViewModel ImageEffects { get; private set; }

        public TaskSettingsViewModel(TaskSettings settings)
        {
            _settings = settings;
            ImageEffects = new ImageEffectsViewModel(Model.ImageSettings);
        }

        public IEnumerable<EImageFormat> ImageFormats => Enum.GetValues(typeof(EImageFormat)).Cast<EImageFormat>();
        public IEnumerable<System.Drawing.ContentAlignment> ContentAlignments => Enum.GetValues(typeof(System.Drawing.ContentAlignment)).Cast<System.Drawing.ContentAlignment>();
        public IEnumerable<ToastClickAction> ToastClickActions => Enum.GetValues(typeof(ToastClickAction)).Cast<ToastClickAction>();

        // Expose underlying model if needed
        public TaskSettings Model => _settings;

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

        [RelayCommand]
        private async Task OpenFFmpegOptionsAsync()
        {
            var taskSettings = SettingManager.GetOrCreateWorkflowTaskSettings(HotkeyType.ScreenRecorder);
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
            }
            else
            {
                window.Show();
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

        public System.Drawing.ContentAlignment ToastWindowPlacement
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
                    _settings.GeneralSettings.ToastWindowSize = new System.Drawing.Size(value, _settings.GeneralSettings.ToastWindowSize.Height);
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
                    _settings.GeneralSettings.ToastWindowSize = new System.Drawing.Size(_settings.GeneralSettings.ToastWindowSize.Width, value);
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
    }
}
