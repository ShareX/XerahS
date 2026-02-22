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
using XerahS.Core;

namespace XerahS.UI.ViewModels
{
    public partial class TaskSettingsViewModel
    {
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
    }
}
