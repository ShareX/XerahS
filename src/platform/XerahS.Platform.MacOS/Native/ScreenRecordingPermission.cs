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

using System.Diagnostics;
using System.Runtime.InteropServices;
using DebugHelper = XerahS.Common.DebugHelper;

namespace XerahS.Platform.MacOS.Native
{
    /// <summary>
    /// Screen Recording (TCC) permission preflight and guided request flow for macOS 10.15+.
    /// Without this check an unpermissioned capture silently produces wallpaper-only frames,
    /// which looks like a broken screenshot instead of an actionable permission problem (XIP0078 P3).
    /// </summary>
    internal static class ScreenRecordingPermission
    {
        private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

        private const string PrivacyPaneUrl =
            "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture";

        private static readonly Lock SyncLock = new();
        private static bool _requestedThisSession;
        private static bool _guidanceShownThisSession;

        [DllImport(CoreGraphicsLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CGPreflightScreenCaptureAccess();

        [DllImport(CoreGraphicsLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CGRequestScreenCaptureAccess();

        /// <summary>
        /// Returns true when the process already holds Screen Recording permission.
        /// Never triggers a system prompt.
        /// </summary>
        public static bool IsGranted()
        {
            if (!OperatingSystem.IsMacOSVersionAtLeast(10, 15))
            {
                return true; // No Screen Recording TCC gate before Catalina.
            }

            try
            {
                return CGPreflightScreenCaptureAccess();
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                // API unavailable; assume granted rather than blocking capture.
                return true;
            }
        }

        /// <summary>
        /// Ensures Screen Recording access before a capture attempt.
        /// If not yet granted, triggers the system prompt at most once per session.
        /// If still denied, optionally opens the Privacy &amp; Security pane once per session
        /// so the user gets an actionable path instead of a wallpaper-only screenshot.
        /// </summary>
        /// <returns>True when capture may proceed.</returns>
        public static bool EnsureAccess(bool showGuidance = true)
        {
            if (IsGranted())
            {
                return true;
            }

            bool granted = false;

            lock (SyncLock)
            {
                if (!_requestedThisSession)
                {
                    _requestedThisSession = true;
                    try
                    {
                        // Shows the system prompt once per TCC lifetime; returns current grant state.
                        granted = CGRequestScreenCaptureAccess();
                        DebugHelper.WriteLine($"[ScreenRecordingPermission] Requested Screen Recording access, granted={granted}");
                    }
                    catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
                    {
                        return true;
                    }
                }
            }

            if (granted || IsGranted())
            {
                return true;
            }

            DebugHelper.WriteLine(
                "[ScreenRecordingPermission] Screen Recording permission is denied. " +
                "Grant it in System Settings > Privacy & Security > Screen Recording, then restart XerahS.");

            if (showGuidance)
            {
                ShowGuidanceOnce();
            }

            return false;
        }

        private static void ShowGuidanceOnce()
        {
            lock (SyncLock)
            {
                if (_guidanceShownThisSession)
                {
                    return;
                }

                _guidanceShownThisSession = true;
            }

            try
            {
                new Services.MacOSNotificationService().ShowNotification(
                    "Screen Recording permission required",
                    "Enable XerahS in System Settings > Privacy & Security > Screen Recording, then restart the app.");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "[ScreenRecordingPermission] Failed to show permission notification");
            }

            try
            {
                using var process = Process.Start(new ProcessStartInfo("open", PrivacyPaneUrl)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                DebugHelper.WriteLine("[ScreenRecordingPermission] Opened Privacy & Security > Screen Recording pane");
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "[ScreenRecordingPermission] Failed to open Privacy & Security pane");
            }
        }
    }
}
