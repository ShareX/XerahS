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

namespace XerahS.Platform.Linux.Capture.Orchestration;

internal sealed class WaterfallCapturePolicy : ILinuxCapturePolicy
{
    private static readonly LinuxCaptureStage[] SandboxedOrder =
    {
        LinuxCaptureStage.Portal
    };

    private static readonly LinuxCaptureStage[] DefaultOrder =
    {
        LinuxCaptureStage.Portal,
        LinuxCaptureStage.DesktopDbus,
        LinuxCaptureStage.WaylandProtocol,
        LinuxCaptureStage.X11
    };

    // KDE Plasma Wayland: KWin's own ScreenShot2 interface captures silently once the app's desktop entry
    // declares it. The portal, by contrast, shows a "Screenshot Requested" permission dialog whose Allow button
    // stays disabled until the user first clicks Take Screenshot (xdg-desktop-portal-kde 6.4+), which reads as
    // an error. Try KWin first for whole-screen and window grabs, and keep the portal as the fallback for
    // when KWin refuses (no matching desktop entry).
    private static readonly LinuxCaptureStage[] KdeWaylandNonInteractiveOrder =
    {
        LinuxCaptureStage.DesktopDbus,
        LinuxCaptureStage.Portal,
        LinuxCaptureStage.WaylandProtocol,
        LinuxCaptureStage.X11
    };

    private static readonly LinuxCaptureStage[] X11RegionOrder =
    {
        LinuxCaptureStage.DesktopDbus,
        LinuxCaptureStage.X11,
        LinuxCaptureStage.Portal
    };

    private static readonly LinuxCaptureStage[] X11PortalPreferredRegionOrder =
    {
        LinuxCaptureStage.Portal,
        LinuxCaptureStage.DesktopDbus,
        LinuxCaptureStage.X11
    };

    private static readonly LinuxCaptureStage[] PortalFirstRegionOrder =
    {
        LinuxCaptureStage.Portal,
        LinuxCaptureStage.DesktopDbus,
        LinuxCaptureStage.WaylandProtocol,
        LinuxCaptureStage.X11
    };

    private static readonly LinuxCaptureStage[] PortalOnlyRegionOrder =
    {
        LinuxCaptureStage.Portal
    };

    private static readonly LinuxCaptureStage[] DesktopNativeOnlyRegionOrder =
    {
        LinuxCaptureStage.DesktopDbus
    };

    private static readonly LinuxCaptureStage[] SlurpOnlyRegionOrder =
    {
        LinuxCaptureStage.WaylandProtocol
    };

    private static readonly LinuxCaptureStage[] OmaSnapFirstRegionOrder =
    {
        LinuxCaptureStage.OmaSnap,
        LinuxCaptureStage.Portal,
        LinuxCaptureStage.DesktopDbus,
        LinuxCaptureStage.WaylandProtocol,
        LinuxCaptureStage.X11
    };

    public IReadOnlyList<LinuxCaptureStage> GetStageOrder(LinuxCaptureRequest request, ILinuxCaptureContext context)
    {
        // OmaSnap only when explicitly selected for a region capture (XIP0088). Automatic keeps the
        // existing order here; workflow jobs reach OmaSnap through the capture pipeline instead.
        // On OmaSnap failure the rest of the Wayland order still runs; a user cancel stays a cancel.
        if (!context.IsSandboxed && context.IsWayland && request.Kind == LinuxCaptureKind.Region &&
            request.SelectorPreference == LinuxInteractiveRegionSelectorPreference.OmaSnap)
        {
            return OmaSnapFirstRegionOrder;
        }

        if (context.IsSandboxed)
        {
            return SandboxedOrder;
        }

        if (!context.IsWayland && request.Kind == LinuxCaptureKind.Region)
        {
            switch (request.SelectorPreference)
            {
                case LinuxInteractiveRegionSelectorPreference.PortalDialog:
                    return PortalOnlyRegionOrder;
                case LinuxInteractiveRegionSelectorPreference.DesktopNative:
                    return DesktopNativeOnlyRegionOrder;
            }

            if (context.PrefersPortalForRegionCaptureOnX11)
            {
                return X11PortalPreferredRegionOrder;
            }

            return X11RegionOrder;
        }

        if (context.IsWayland && request.Kind == LinuxCaptureKind.Region)
        {
            // KDE Plasma Wayland, Automatic: the portal dialog has no region option, while KWin's
            // CaptureInteractive is a native region selector. Reached only when the XerahS overlay is
            // not in use; the portal remains the fallback if KWin refuses.
            if (string.Equals(context.Desktop, "KDE", StringComparison.OrdinalIgnoreCase) &&
                request.SelectorPreference == LinuxInteractiveRegionSelectorPreference.Automatic)
            {
                return KdeWaylandNonInteractiveOrder;
            }

            return request.SelectorPreference switch
            {
                LinuxInteractiveRegionSelectorPreference.Slurp => SlurpOnlyRegionOrder,
                LinuxInteractiveRegionSelectorPreference.PortalDialog => PortalOnlyRegionOrder,
                _ => DefaultOrder
            };
        }

        if (context.IsWayland &&
            string.Equals(context.Desktop, "KDE", StringComparison.OrdinalIgnoreCase) &&
            request.Kind is LinuxCaptureKind.FullScreen or LinuxCaptureKind.ActiveWindow)
        {
            return KdeWaylandNonInteractiveOrder;
        }

        return DefaultOrder;
    }
}
