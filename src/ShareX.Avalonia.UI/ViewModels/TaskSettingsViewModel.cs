using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.Ava.Core;
using System;

namespace ShareX.Ava.UI.ViewModels
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

        #endregion
    }
}
