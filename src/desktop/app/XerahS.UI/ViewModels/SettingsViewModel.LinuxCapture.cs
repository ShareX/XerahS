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

using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.ScreenRecording;
using XerahS.UI.Helpers;

namespace XerahS.UI.ViewModels
{
    public partial class SettingsViewModel
    {
        [ObservableProperty]
        private LinuxInteractiveRegionSelectorPreference _linuxRegionSelectorPreference;

        [ObservableProperty]
        private LinuxRecordingBackendPreference _linuxRecordingBackendPreference;

        [ObservableProperty]
        private string _linuxRegionSelectorCurrentSessionText = string.Empty;

        [ObservableProperty]
        private string _linuxRegionSelectorPortalBackendText = string.Empty;

        [ObservableProperty]
        private string _linuxRegionSelectorAvailableText = string.Empty;

        [ObservableProperty]
        private string _linuxRegionSelectorAutomaticText = string.Empty;

        [ObservableProperty]
        private string _linuxRegionSelectorLastDecisionText = string.Empty;

        [ObservableProperty]
        private bool _showLinuxClipboardCliWarning;

        [ObservableProperty]
        private string? _linuxClipboardCliWarningText;

        public bool IsLinuxPlatform => OperatingSystem.IsLinux();

        public IReadOnlyList<LinuxInteractiveRegionSelectorPreference> LinuxRegionSelectorPreferences =>
            LinuxRegionSelectorPreferenceSupport.GetVisiblePreferences();

        /// <summary>OmaSnap probe result, e.g. "OmaSnap 1.22.0 · Hyprland · ready" (XIP0088).</summary>
        [ObservableProperty]
        private string _linuxOmaSnapStatusText = string.Empty;

        [ObservableProperty]
        private bool _isOmaSnapAvailable;

        public LinuxAnnotationEditor[] LinuxAnnotationEditors => Enum.GetValues<LinuxAnnotationEditor>();

        public LinuxAnnotationEditor LinuxAnnotationEditor
        {
            get => SettingsManager.Settings.LinuxAnnotationEditor;
            set
            {
                if (SettingsManager.Settings.LinuxAnnotationEditor != value)
                {
                    SettingsManager.Settings.LinuxAnnotationEditor = value;
                    OnPropertyChanged();
                }
            }
        }

        private void RefreshLinuxOmaSnapStatus()
        {
            HostedCaptureEngineStatus? status = PlatformServices.HostedCaptureEngine?.CurrentStatus;
            IsOmaSnapAvailable = status?.Available == true;
            LinuxOmaSnapStatusText = status == null
                ? string.Empty
                : $"Capture engine: {status.Summary}";
        }

        public LinuxRecordingBackendPreference[] LinuxRecordingBackendPreferences =>
            Enum.GetValues<LinuxRecordingBackendPreference>();

        private void RefreshLinuxRegionSelectorDiagnostics()
        {
            if (!OperatingSystem.IsLinux())
            {
                LinuxRegionSelectorCurrentSessionText = string.Empty;
                LinuxRegionSelectorPortalBackendText = string.Empty;
                LinuxRegionSelectorAvailableText = string.Empty;
                LinuxRegionSelectorAutomaticText = string.Empty;
                LinuxRegionSelectorLastDecisionText = string.Empty;
                OnPropertyChanged(nameof(LinuxRegionSelectorPreferences));
                return;
            }

            RefreshLinuxOmaSnapStatus();
            var diagnostics = LinuxRegionSelectorPreferenceSupport.TryGetDiagnostics();
            if (diagnostics == null)
            {
                LinuxRegionSelectorCurrentSessionText = Resources.GetString("LinuxCapture_CurrentSessionUnavailable");
                LinuxRegionSelectorPortalBackendText = Resources.GetString("LinuxCapture_PortalBackendUnavailable");
                LinuxRegionSelectorAvailableText = Resources.GetString("LinuxCapture_AvailableSelectorsFallback");
                LinuxRegionSelectorAutomaticText = Resources.GetString("LinuxCapture_AutomaticFallback");
                LinuxRegionSelectorLastDecisionText = Resources.GetString("LinuxCapture_LastDecisionUnavailable");
                OnPropertyChanged(nameof(LinuxRegionSelectorPreferences));
                return;
            }

            string sessionType = FormatDiagnosticsValue(diagnostics.SessionType);
            string desktop = FormatDiagnosticsValue(diagnostics.Desktop);
            string compositor = FormatDiagnosticsValue(diagnostics.Compositor);
            LinuxRegionSelectorCurrentSessionText =
                Resources.GetString("LinuxCapture_CurrentSession", sessionType, desktop, compositor);
            LinuxRegionSelectorPortalBackendText = Resources.GetString("LinuxCapture_PortalBackend", FormatDiagnosticsValue(diagnostics.PortalBackendSummary));
            LinuxRegionSelectorAvailableText = Resources.GetString("LinuxCapture_AvailableSelectors", string.Join(", ", diagnostics.AvailablePreferences.Select(GetPreferenceDescription)));
            LinuxRegionSelectorAutomaticText = Resources.GetString("LinuxCapture_AutomaticPreference", GetPreferenceDescription(diagnostics.AutomaticPreference));
            LinuxRegionSelectorLastDecisionText = FormatLastDecision(diagnostics.LastDecision);
            OnPropertyChanged(nameof(LinuxRegionSelectorPreferences));
        }

        private void RefreshLinuxClipboardDiagnostics()
        {
            if (!OperatingSystem.IsLinux())
            {
                ShowLinuxClipboardCliWarning = false;
                LinuxClipboardCliWarningText = null;
                return;
            }

#if LINUX
            LinuxClipboardCliWarningText = XerahS.Platform.Linux.Services.LinuxClipboardCapabilities.UserFacingWarning;
            ShowLinuxClipboardCliWarning = !XerahS.Platform.Linux.Services.LinuxClipboardCapabilities.CliClipboardHealthy;
#else
            ShowLinuxClipboardCliWarning = false;
            LinuxClipboardCliWarningText = null;
#endif
        }

        private static string GetPreferenceDescription(LinuxInteractiveRegionSelectorPreference preference)
        {
            return preference.GetLocalizedDescription();
        }

        private static string FormatDiagnosticsValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? Resources.GetString("LinuxCapture_Unavailable") : value;
        }

        private static string FormatLastDecision(LinuxRegionSelectorRuntimeDecision? decision)
        {
            if (decision == null)
            {
                return Resources.GetString("LinuxCapture_LastDecisionNone");
            }

            string timestampText = decision.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            string requestedPreference = GetPreferenceDescription(decision.RequestedPreference);
            string effectivePreference = GetPreferenceDescription(decision.EffectivePreference);

            return decision.Outcome switch
            {
                "Succeeded" =>
                    Resources.GetString("LinuxCapture_LastDecisionSucceeded", decision.Operation, decision.ProviderDisplayName, decision.ProviderId, timestampText, requestedPreference, effectivePreference),
                "Cancelled" =>
                    Resources.GetString("LinuxCapture_LastDecisionCancelled", decision.Operation, decision.ProviderDisplayName, decision.ProviderId, timestampText, requestedPreference, effectivePreference),
                _ =>
                    Resources.GetString("LinuxCapture_LastDecisionFailed", decision.Operation, decision.ProviderDisplayName, decision.ProviderId, timestampText, requestedPreference, effectivePreference)
            };
        }
    }
}
