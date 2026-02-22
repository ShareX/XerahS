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
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels
{
    public partial class TaskSettingsViewModel
    {
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
    }
}
