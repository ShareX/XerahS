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
using XerahS.RegionCapture.ScreenRecording;
using XerahS.UI.Helpers;

namespace XerahS.UI.ViewModels
{
    public partial class TaskSettingsViewModel
    {
        #region Capture Settings

        public bool ShowUseModernCaptureSetting => OperatingSystem.IsWindows();

        public bool IsMacOSPlatform => OperatingSystem.IsMacOS();

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

        public bool HDRScreenshotColorCorrection
        {
            get => _settings.CaptureSettings.HDRScreenshotColorCorrection;
            set
            {
                if (_settings.CaptureSettings.HDRScreenshotColorCorrection != value)
                {
                    _settings.CaptureSettings.HDRScreenshotColorCorrection = value;
                    OnPropertyChanged();
                }
            }
        }

        public LinuxInteractiveRegionSelectorPreference LinuxRegionSelectorPreference
        {
            get => LinuxRegionSelectorPreferenceSupport.NormalizeForCurrentSession(
                _settings.CaptureSettings.LinuxRegionSelectorPreference);
            set
            {
                if (_settings.CaptureSettings.LinuxRegionSelectorPreference != value)
                {
                    _settings.CaptureSettings.LinuxRegionSelectorPreference = value;
                    OnPropertyChanged();
                }
            }
        }

        public IReadOnlyList<LinuxInteractiveRegionSelectorPreference> LinuxRegionSelectorPreferences =>
            LinuxRegionSelectorPreferenceSupport.GetVisiblePreferences();

        /// <summary>True when the OmaSnap engine passed its probe in this session (XIP0088).</summary>
        public bool IsOmaSnapAvailable => PlatformServices.HostedCaptureEngine?.CurrentStatus.Available == true;

        public bool OmaSnapRegionOnly
        {
            get => _settings.CaptureSettings.OmaSnapRegionOnly;
            set
            {
                if (_settings.CaptureSettings.OmaSnapRegionOnly != value)
                {
                    _settings.CaptureSettings.OmaSnapRegionOnly = value;
                    OnPropertyChanged();
                }
            }
        }

        public MacOSInteractiveRegionSelectorPreference MacOSRegionSelectorPreference
        {
            get => _settings.CaptureSettings.MacOSRegionSelectorPreference;
            set
            {
                if (_settings.CaptureSettings.MacOSRegionSelectorPreference != value)
                {
                    _settings.CaptureSettings.MacOSRegionSelectorPreference = value;
                    OnPropertyChanged();
                }
            }
        }

        public MacOSInteractiveRegionSelectorPreference[] MacOSRegionSelectorPreferences =>
            Enum.GetValues<MacOSInteractiveRegionSelectorPreference>();

        public bool MacOSPlayCaptureSound
        {
            get => _settings.CaptureSettings.MacOSPlayCaptureSound;
            set
            {
                if (_settings.CaptureSettings.MacOSPlayCaptureSound != value)
                {
                    _settings.CaptureSettings.MacOSPlayCaptureSound = value;
                    OnPropertyChanged();
                }
            }
        }

        public LinuxRecordingBackendPreference LinuxRecordingBackendPreference
        {
            get => ResolveLinuxRecordingBackendPreference(_settings.CaptureSettings);
            set
            {
                if (ResolveLinuxRecordingBackendPreference(_settings.CaptureSettings) != value ||
                    _settings.CaptureSettings.LinuxRecordingBackendPreference == null)
                {
                    _settings.CaptureSettings.LinuxRecordingBackendPreference = value;
                    OnPropertyChanged();
                }
            }
        }

        public LinuxRecordingBackendPreference[] LinuxRecordingBackendPreferences =>
            Enum.GetValues<LinuxRecordingBackendPreference>();

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

        public int RegionCaptureBackgroundDimStrength
        {
            get => _settings.CaptureSettings.RegionCaptureOptions.BackgroundDimStrength;
            set
            {
                int clamped = Math.Clamp(value, 0, 100);
                if (_settings.CaptureSettings.RegionCaptureOptions.BackgroundDimStrength != clamped)
                {
                    _settings.CaptureSettings.RegionCaptureOptions.BackgroundDimStrength = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public bool RegionCaptureDimAllExceptSelection
        {
            get => _settings.CaptureSettings.RegionCaptureOptions.DimAllExceptSelection;
            set
            {
                if (_settings.CaptureSettings.RegionCaptureOptions.DimAllExceptSelection != value)
                {
                    _settings.CaptureSettings.RegionCaptureOptions.DimAllExceptSelection = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool RegionCaptureShowMagnifier
        {
            get => _settings.CaptureSettings.RegionCaptureOptions.ShowMagnifier;
            set
            {
                if (_settings.CaptureSettings.RegionCaptureOptions.ShowMagnifier != value)
                {
                    _settings.CaptureSettings.RegionCaptureOptions.ShowMagnifier = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool RegionCaptureUseSquareMagnifier
        {
            get => _settings.CaptureSettings.RegionCaptureOptions.UseSquareMagnifier;
            set
            {
                if (_settings.CaptureSettings.RegionCaptureOptions.UseSquareMagnifier != value)
                {
                    _settings.CaptureSettings.RegionCaptureOptions.UseSquareMagnifier = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool RegionCaptureShowInfo
        {
            get => _settings.CaptureSettings.RegionCaptureOptions.ShowInfo;
            set
            {
                if (_settings.CaptureSettings.RegionCaptureOptions.ShowInfo != value)
                {
                    _settings.CaptureSettings.RegionCaptureOptions.ShowInfo = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool RegionCaptureUseCustomInfoText
        {
            get => _settings.CaptureSettings.RegionCaptureOptions.UseCustomInfoText;
            set
            {
                if (_settings.CaptureSettings.RegionCaptureOptions.UseCustomInfoText != value)
                {
                    _settings.CaptureSettings.RegionCaptureOptions.UseCustomInfoText = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool RegionCaptureShowScreenCrosshair
        {
            get => _settings.CaptureSettings.RegionCaptureOptions.ShowScreenCrosshair;
            set
            {
                if (_settings.CaptureSettings.RegionCaptureOptions.ShowScreenCrosshair != value)
                {
                    _settings.CaptureSettings.RegionCaptureOptions.ShowScreenCrosshair = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Custom HUD text; line breaks are stored as the $n token, as in ShareX.</summary>
        public string RegionCaptureCustomInfoText
        {
            get => (_settings.CaptureSettings.RegionCaptureOptions.CustomInfoText ?? string.Empty).Replace("$n", Environment.NewLine);
            set
            {
                string stored = (value ?? string.Empty).Replace("\r\n", "$n").Replace("\n", "$n");
                if (_settings.CaptureSettings.RegionCaptureOptions.CustomInfoText != stored)
                {
                    _settings.CaptureSettings.RegionCaptureOptions.CustomInfoText = stored;
                    OnPropertyChanged();
                }
            }
        }

        public int RegionCaptureMagnifierPixelCount
        {
            get => _settings.CaptureSettings.RegionCaptureOptions.MagnifierPixelCount;
            set
            {
                int clamped = Math.Clamp(value, RegionCaptureOptions.MagnifierPixelCountMinimum, RegionCaptureOptions.MagnifierPixelCountMaximum) | 1;
                if (_settings.CaptureSettings.RegionCaptureOptions.MagnifierPixelCount != clamped)
                {
                    _settings.CaptureSettings.RegionCaptureOptions.MagnifierPixelCount = clamped;
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
        public IEnumerable<FFmpegVideoCodec> VideoCodecs => Enum.GetValues(typeof(FFmpegVideoCodec)).Cast<FFmpegVideoCodec>();

        public FFmpegVideoCodec ScreenRecordVideoCodec
        {
            get => _settings.CaptureSettings.FFmpegOptions?.VideoCodec ?? FFmpegVideoCodec.libx264;
            set
            {
                _settings.CaptureSettings.FFmpegOptions ??= new XerahS.Core.FFmpegOptions();
                if (_settings.CaptureSettings.FFmpegOptions.VideoCodec != value)
                {
                    _settings.CaptureSettings.FFmpegOptions.VideoCodec = value;
                    OnPropertyChanged();
                }
            }
        }

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

        private static LinuxRecordingBackendPreference ResolveLinuxRecordingBackendPreference(TaskSettingsCapture captureSettings)
        {
            return captureSettings.LinuxRecordingBackendPreference ??
                (captureSettings.UseModernCapture
                    ? LinuxRecordingBackendPreference.Automatic
                    : LinuxRecordingBackendPreference.FFmpeg);
        }

        #endregion
    }
}
