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

using CommunityToolkit.Mvvm.ComponentModel;
using XerahS.Core;

namespace XerahS.UI.ViewModels
{
    public partial class SettingsViewModel
    {
        // Task Settings - General
        [ObservableProperty]
        private bool _playSoundAfterCapture;

        [ObservableProperty]
        private bool _showToastNotification;

        // Task Settings - Capture
        [ObservableProperty]
        private bool _showCursor;

        [ObservableProperty]
        private double _screenshotDelay;

        [ObservableProperty]
        private bool _captureTransparent;

        [ObservableProperty]
        private bool _captureShadow;

        [ObservableProperty]
        private bool _captureClientArea;

        // Task Settings - Upload / File Naming
        [ObservableProperty]
        private string _nameFormatPattern = string.Empty;

        [ObservableProperty]
        private string _nameFormatPatternActiveWindow = string.Empty;

        [ObservableProperty]
        private bool _fileUploadUseNamePattern;

        [ObservableProperty]
        private bool _fileUploadReplaceProblematicCharacters;

        [ObservableProperty]
        private bool _uRLRegexReplace;

        [ObservableProperty]
        private string _uRLRegexReplacePattern = string.Empty;

        [ObservableProperty]
        private string _uRLRegexReplaceReplacement = string.Empty;

        // Task Settings - After Capture
        public bool SaveImageToFile
        {
            get => ActiveTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.SaveImageToFile);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterCaptureJob |= AfterCaptureTasks.SaveImageToFile;
                else
                    ActiveTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.SaveImageToFile;
                OnPropertyChanged();
            }
        }

        public bool CopyImageToClipboard
        {
            get => ActiveTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.CopyImageToClipboard);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterCaptureJob |= AfterCaptureTasks.CopyImageToClipboard;
                else
                    ActiveTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.CopyImageToClipboard;
                OnPropertyChanged();
            }
        }

        public bool UploadImageToHost
        {
            get => ActiveTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.UploadImageToHost);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterCaptureJob |= AfterCaptureTasks.UploadImageToHost;
                else
                    ActiveTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.UploadImageToHost;
                OnPropertyChanged();
            }
        }

        public bool AnnotateImage
        {
            get => ActiveTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.AnnotateImage);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterCaptureJob |= AfterCaptureTasks.AnnotateImage;
                else
                    ActiveTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.AnnotateImage;
                OnPropertyChanged();
            }
        }

        public bool ShowAfterCaptureWindow
        {
            get => ActiveTaskSettings.AfterCaptureJob.HasFlag(AfterCaptureTasks.ShowAfterCaptureWindow);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterCaptureJob |= AfterCaptureTasks.ShowAfterCaptureWindow;
                else
                    ActiveTaskSettings.AfterCaptureJob &= ~AfterCaptureTasks.ShowAfterCaptureWindow;
                OnPropertyChanged();
            }
        }

        // Task Settings - After Upload
        public bool ShowAfterUploadWindow
        {
            get => ActiveTaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.ShowAfterUploadWindow);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterUploadJob |= AfterUploadTasks.ShowAfterUploadWindow;
                else
                    ActiveTaskSettings.AfterUploadJob &= ~AfterUploadTasks.ShowAfterUploadWindow;
                OnPropertyChanged();
            }
        }

        public bool CopyURLToClipboard
        {
            get => ActiveTaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.CopyURLToClipboard);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterUploadJob |= AfterUploadTasks.CopyURLToClipboard;
                else
                    ActiveTaskSettings.AfterUploadJob &= ~AfterUploadTasks.CopyURLToClipboard;
                OnPropertyChanged();
            }
        }

        public bool UseURLShortener
        {
            get => ActiveTaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.UseURLShortener);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterUploadJob |= AfterUploadTasks.UseURLShortener;
                else
                    ActiveTaskSettings.AfterUploadJob &= ~AfterUploadTasks.UseURLShortener;
                OnPropertyChanged();
            }
        }

        public bool ShareURL
        {
            get => ActiveTaskSettings.AfterUploadJob.HasFlag(AfterUploadTasks.ShareURL);
            set
            {
                if (value)
                    ActiveTaskSettings.AfterUploadJob |= AfterUploadTasks.ShareURL;
                else
                    ActiveTaskSettings.AfterUploadJob &= ~AfterUploadTasks.ShareURL;
                OnPropertyChanged();
            }
        }
    }
}
