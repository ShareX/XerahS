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

using System;
using System.Collections.Generic;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.Contracts;

namespace XerahS.Platform.Linux.Capture.Detection;

internal static class LinuxRegionSelectorDiagnosticsDetector
{
    public static LinuxRegionSelectorDiagnostics Detect()
    {
        var context = LinuxRuntimeContextDetector.Detect();
        var support = LinuxRegionCaptureCapabilityDetector.ProbeSupportSnapshot(context);
        var capability = LinuxRegionCaptureCapabilityDetector.Detect(context, support);
        return Detect(context, support, capability, Services.LinuxDesktopProfile.Current.IsOmarchyLike);
    }

    internal static LinuxRegionSelectorDiagnostics Detect(
        ILinuxCaptureContext context,
        LinuxRegionCaptureSupportSnapshot support,
        LinuxRegionCaptureCapability capability,
        bool omaSnapAvailable = false)
    {
        List<LinuxInteractiveRegionSelectorPreference> availablePreferences =
        [
            LinuxInteractiveRegionSelectorPreference.Automatic
        ];

        if (capability.SupportsLegacyOverlayCapture)
        {
            availablePreferences.Add(LinuxInteractiveRegionSelectorPreference.XerahSOverlay);
        }

        if (support.HasGnomeShellScreenshot || support.HasKdeScreenShot2)
        {
            availablePreferences.Add(LinuxInteractiveRegionSelectorPreference.DesktopNative);
        }

        if (context.HasScreenshotPortal)
        {
            availablePreferences.Add(LinuxInteractiveRegionSelectorPreference.PortalDialog);
        }

        if (support.HasSlurp)
        {
            availablePreferences.Add(LinuxInteractiveRegionSelectorPreference.Slurp);
        }

        // Offered only after the OmaSnap host-mode probe succeeded (XIP0088). The Automatic
        // preference below is deliberately unchanged: workflow captures reach OmaSnap through
        // the capture pipeline, and every other system keeps today's order.
        if (omaSnapAvailable && context.IsWayland && !context.IsSandboxed)
        {
            availablePreferences.Add(LinuxInteractiveRegionSelectorPreference.OmaSnap);
        }

        return new LinuxRegionSelectorDiagnostics(
            SessionType: context.IsWayland ? "Wayland" : "X11",
            Desktop: context.Desktop ?? "Unknown",
            Compositor: context.Compositor ?? "Unknown",
            PortalBackendSummary: context.HasScreenshotPortal
                ? PortalBackendDetector.GetRunningBackendsSummary()
                : "not available",
            AutomaticPreference: ResolveAutomaticPreference(context, support, capability),
            AvailablePreferences: availablePreferences);
    }

    private static LinuxInteractiveRegionSelectorPreference ResolveAutomaticPreference(
        ILinuxCaptureContext context,
        LinuxRegionCaptureSupportSnapshot support,
        LinuxRegionCaptureCapability capability)
    {
        if (context.IsWayland)
        {
            // KDE Plasma Wayland (not sandboxed): the portal's screenshot dialog has no rectangular-region
            // option, so Automatic uses the XerahS overlay. Its full-screen pre-capture goes through KWin
            // ScreenShot2 first (WaterfallCapturePolicy), which is silent; the portal stays only as a fallback.
            if (!context.IsSandboxed &&
                string.Equals(context.Desktop, "KDE", StringComparison.OrdinalIgnoreCase) &&
                capability.SupportsLegacyOverlayCapture)
            {
                return LinuxInteractiveRegionSelectorPreference.XerahSOverlay;
            }

            if (context.HasScreenshotPortal)
            {
                return LinuxInteractiveRegionSelectorPreference.PortalDialog;
            }

            if (support.HasSlurp)
            {
                return LinuxInteractiveRegionSelectorPreference.Slurp;
            }

            return LinuxInteractiveRegionSelectorPreference.Automatic;
        }

        if (support.HasGnomeShellScreenshot || support.HasKdeScreenShot2)
        {
            return LinuxInteractiveRegionSelectorPreference.DesktopNative;
        }

        if (support.PrefersPortalForRegionCaptureOnX11)
        {
            return LinuxInteractiveRegionSelectorPreference.PortalDialog;
        }

        if (capability.SupportsLegacyOverlayCapture)
        {
            return LinuxInteractiveRegionSelectorPreference.XerahSOverlay;
        }

        return context.HasScreenshotPortal
            ? LinuxInteractiveRegionSelectorPreference.PortalDialog
            : LinuxInteractiveRegionSelectorPreference.Automatic;
    }
}
