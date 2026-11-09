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

using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux;
using XerahS.Platform.Linux.Capture.Contracts;
using XerahS.Platform.Linux.Capture.Detection;
using XerahS.Platform.Linux.Capture.Orchestration;
using XerahS.Platform.Linux.Capture.Portal;
using XerahS.Platform.Linux.Capture.Providers;
using XerahS.Platform.Linux.Recording;

namespace XerahS.Tests.Platform.Linux;

public class LinuxCaptureOrchestrationTests
{
    [Test]
    public void WaterfallPolicy_NonSandboxed_UsesFullStageOrder()
    {
        var policy = new WaterfallCapturePolicy();
        var request = new LinuxCaptureRequest(LinuxCaptureKind.FullScreen, options: null);
        var context = new LinuxCaptureContext(isWayland: false, desktop: "KDE", compositor: "X11", isSandboxed: false, hasScreenshotPortal: true);

        var order = policy.GetStageOrder(request, context);

        Assert.That(order, Is.EqualTo(new[]
        {
            LinuxCaptureStage.Portal,
            LinuxCaptureStage.DesktopDbus,
            LinuxCaptureStage.WaylandProtocol,
            LinuxCaptureStage.X11
        }));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void WaterfallPolicy_KdeWayland_WholeScreenAndWindow_TriesKWinBeforePortalPrompt(bool fullScreen)
    {
        var policy = new WaterfallCapturePolicy();
        var request = new LinuxCaptureRequest(fullScreen ? LinuxCaptureKind.FullScreen : LinuxCaptureKind.ActiveWindow, options: null);
        var context = new LinuxCaptureContext(isWayland: true, desktop: "KDE", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);

        var order = policy.GetStageOrder(request, context);

        Assert.That(order, Is.EqualTo(new[]
        {
            LinuxCaptureStage.DesktopDbus,
            LinuxCaptureStage.Portal,
            LinuxCaptureStage.WaylandProtocol,
            LinuxCaptureStage.X11
        }));
    }

    [Test]
    public void WaterfallPolicy_KdeWayland_Sandboxed_StaysPortalOnly()
    {
        var policy = new WaterfallCapturePolicy();
        var request = new LinuxCaptureRequest(LinuxCaptureKind.FullScreen, options: null);
        var context = new LinuxCaptureContext(isWayland: true, desktop: "KDE", compositor: "WAYLAND", isSandboxed: true, hasScreenshotPortal: true, isFlatpak: true);

        Assert.That(policy.GetStageOrder(request, context), Is.EqualTo(new[] { LinuxCaptureStage.Portal }));
    }

    [Test]
    public void WaterfallPolicy_GnomeWayland_FullScreen_KeepsPortalFirst()
    {
        var policy = new WaterfallCapturePolicy();
        var request = new LinuxCaptureRequest(LinuxCaptureKind.FullScreen, options: null);
        var context = new LinuxCaptureContext(isWayland: true, desktop: "GNOME", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);

        Assert.That(policy.GetStageOrder(request, context)[0], Is.EqualTo(LinuxCaptureStage.Portal));
    }

    [Test]
    public void WaterfallPolicy_X11Region_PrefersDesktopDbusAndX11BeforePortal()
    {
        var policy = new WaterfallCapturePolicy();
        var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, options: null);
        var context = new LinuxCaptureContext(isWayland: false, desktop: "CINNAMON", compositor: "X11", isSandboxed: false, hasScreenshotPortal: true);

        var order = policy.GetStageOrder(request, context);

        Assert.That(order, Is.EqualTo(new[]
        {
            LinuxCaptureStage.DesktopDbus,
            LinuxCaptureStage.X11,
            LinuxCaptureStage.Portal
        }));
    }

    [Test]
    public void WaterfallPolicy_X11Region_WithDesktopNativePortal_PrefersPortalFirst()
    {
        var policy = new WaterfallCapturePolicy();
        var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, options: null);
        var context = new LinuxCaptureContext(
            isWayland: false,
            desktop: "XFCE",
            compositor: "X11",
            isSandboxed: false,
            hasScreenshotPortal: true,
            prefersPortalForRegionCaptureOnX11: true);

        var order = policy.GetStageOrder(request, context);

        Assert.That(order, Is.EqualTo(new[]
        {
            LinuxCaptureStage.Portal,
            LinuxCaptureStage.DesktopDbus,
            LinuxCaptureStage.X11
        }));
    }

    [Test]
    public void WaterfallPolicy_X11Region_WithExplicitDesktopNative_UsesDesktopDbusOnly()
    {
        var policy = new WaterfallCapturePolicy();
        var request = new LinuxCaptureRequest(
            LinuxCaptureKind.Region,
            new CaptureOptions { LinuxRegionSelectorPreference = LinuxInteractiveRegionSelectorPreference.DesktopNative });
        var context = new LinuxCaptureContext(isWayland: false, desktop: "KDE", compositor: "X11", isSandboxed: false, hasScreenshotPortal: true);

        var order = policy.GetStageOrder(request, context);

        Assert.That(order, Is.EqualTo(new[] { LinuxCaptureStage.DesktopDbus }));
    }

    [Test]
    public void WaterfallPolicy_WaylandRegion_WithExplicitPortalDialog_UsesPortalOnly()
    {
        var policy = new WaterfallCapturePolicy();
        var request = new LinuxCaptureRequest(
            LinuxCaptureKind.Region,
            new CaptureOptions { LinuxRegionSelectorPreference = LinuxInteractiveRegionSelectorPreference.PortalDialog });
        var context = new LinuxCaptureContext(isWayland: true, desktop: "GNOME", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);

        var order = policy.GetStageOrder(request, context);

        Assert.That(order, Is.EqualTo(new[] { LinuxCaptureStage.Portal }));
    }

    [Test]
    public void WaterfallPolicy_WaylandRegion_WithExplicitSlurp_UsesWlrootsOnly()
    {
        var policy = new WaterfallCapturePolicy();
        var request = new LinuxCaptureRequest(
            LinuxCaptureKind.Region,
            new CaptureOptions { LinuxRegionSelectorPreference = LinuxInteractiveRegionSelectorPreference.Slurp });
        var context = new LinuxCaptureContext(isWayland: true, desktop: "SWAY", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);

        var order = policy.GetStageOrder(request, context);

        Assert.That(order, Is.EqualTo(new[] { LinuxCaptureStage.WaylandProtocol }));
    }

    [Test]
    public void WaterfallPolicy_Sandboxed_UsesPortalOnlyOrder()
    {
        var policy = new WaterfallCapturePolicy();
        var request = new LinuxCaptureRequest(LinuxCaptureKind.FullScreen, options: null);
        var context = new LinuxCaptureContext(isWayland: true, desktop: "GNOME", compositor: "WAYLAND", isSandboxed: true, hasScreenshotPortal: true);

        var order = policy.GetStageOrder(request, context);

        Assert.That(order, Is.EqualTo(new[] { LinuxCaptureStage.Portal }));
    }

    [Test]
    public void WaterfallPolicy_FlatpakX11_UsesPortalOnlyOrder()
    {
        var policy = new WaterfallCapturePolicy();
        var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, options: null);
        var context = new LinuxCaptureContext(
            isWayland: false,
            desktop: "KDE",
            compositor: "X11",
            isSandboxed: true,
            hasScreenshotPortal: true,
            isFlatpak: true);

        var order = policy.GetStageOrder(request, context);

        Assert.That(order, Is.EqualTo(new[] { LinuxCaptureStage.Portal }));
    }

    [Test]
    public async Task Coordinator_TraceRecordsSkipFailSuccessInOrder()
    {
        var providers = new ILinuxCaptureProvider[]
        {
            new TestProvider("portal-skip", LinuxCaptureStage.Portal, canHandle: false, resultFactory: () => LinuxCaptureResult.Failure("portal-skip")),
            new TestProvider("portal-fail", LinuxCaptureStage.Portal, canHandle: true, resultFactory: () => LinuxCaptureResult.Failure("portal-fail")),
            new TestProvider("gnome-success", LinuxCaptureStage.DesktopDbus, canHandle: true, resultFactory: () => LinuxCaptureResult.Success("gnome-success", new SKBitmap(1, 1)))
        };

        var coordinator = new LinuxCaptureCoordinator(providers, new WaterfallCapturePolicy());
        var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, options: null);
        var context = new LinuxCaptureContext(isWayland: true, desktop: "GNOME", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);

        var execution = await coordinator.CaptureWithTraceAsync(request, context, CancellationToken.None);
        execution.Result.Bitmap?.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(execution.Result.ProviderId, Is.EqualTo("gnome-success"));
            Assert.That(execution.Trace.FinalOutcome, Is.EqualTo(CaptureDecisionOutcome.Succeeded));
            Assert.That(execution.Trace.Steps.Count, Is.EqualTo(3));
            Assert.That(execution.Trace.Steps[0].Outcome, Is.EqualTo(CaptureDecisionOutcome.Skipped));
            Assert.That(execution.Trace.Steps[1].Outcome, Is.EqualTo(CaptureDecisionOutcome.Failed));
            Assert.That(execution.Trace.Steps[2].Outcome, Is.EqualTo(CaptureDecisionOutcome.Succeeded));
        });
    }

    [Test]
    public void ProviderLanes_RespectDesktopWaylandAndSandboxConstraints()
    {
        var runtime = new NoOpRuntime();
        var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, options: null);

        var kdeProvider = new KdeDbusCaptureProvider(runtime);
        var gnomeProvider = new GnomeDbusCaptureProvider(runtime);
        var wlrootsProvider = new WlrootsCaptureProvider(runtime);
        var x11Provider = new X11CaptureProvider(runtime);
        var cliProvider = new CliCaptureProvider(runtime);

        var kdeContext = new LinuxCaptureContext(isWayland: false, desktop: "KDE", compositor: "X11", isSandboxed: false, hasScreenshotPortal: false);
        var gnomeContext = new LinuxCaptureContext(isWayland: true, desktop: "GNOME", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);
        var wlrootsContext = new LinuxCaptureContext(isWayland: true, desktop: "SWAY", compositor: "SWAY", isSandboxed: false, hasScreenshotPortal: false);
        var sandboxContext = new LinuxCaptureContext(isWayland: true, desktop: "KDE", compositor: "WAYLAND", isSandboxed: true, hasScreenshotPortal: true);

        Assert.Multiple(() =>
        {
            Assert.That(kdeProvider.CanHandle(request, kdeContext), Is.True);
            Assert.That(kdeProvider.CanHandle(request, gnomeContext), Is.False);
            Assert.That(kdeProvider.CanHandle(request, sandboxContext), Is.False);

            Assert.That(gnomeProvider.CanHandle(request, gnomeContext), Is.True);
            Assert.That(gnomeProvider.CanHandle(request, kdeContext), Is.False);

            Assert.That(wlrootsProvider.CanHandle(request, wlrootsContext), Is.True);
            Assert.That(wlrootsProvider.CanHandle(request, kdeContext), Is.False);
            Assert.That(wlrootsProvider.CanHandle(request, sandboxContext), Is.False);

            Assert.That(x11Provider.CanHandle(request, kdeContext), Is.True);
            Assert.That(x11Provider.CanHandle(request, gnomeContext), Is.False);
            Assert.That(x11Provider.CanHandle(request, sandboxContext), Is.False);

            Assert.That(cliProvider.CanHandle(request, kdeContext), Is.True);
            Assert.That(cliProvider.CanHandle(request, gnomeContext), Is.False);
            Assert.That(cliProvider.CanHandle(request, sandboxContext), Is.False);
        });
    }

    [Test]
    public void PortalProvider_Wayland_AcceptsEvenWhenModernCaptureDisabled()
    {
        var runtime = new NoOpRuntime();
        var portalProvider = new PortalCaptureProvider(runtime);

        // UseModernCapture=false via explicit CaptureOptions
        var options = new CaptureOptions { UseModernCapture = false };
        var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, options);

        // Wayland + portal available → must still accept (portal is the only viable backend)
        var waylandWithPortal = new LinuxCaptureContext(isWayland: true, desktop: "GNOME", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);
        Assert.That(portalProvider.CanHandle(request, waylandWithPortal), Is.True,
            "Portal must handle Wayland captures even when UseModernCapture=false");

        // X11 + UseModernCapture=false → should NOT accept (X11/CLI tools can handle it)
        var x11Context = new LinuxCaptureContext(isWayland: false, desktop: "GNOME", compositor: "X11", isSandboxed: false, hasScreenshotPortal: true);
        Assert.That(portalProvider.CanHandle(request, x11Context), Is.False,
            "Portal should not force-accept on X11 when UseModernCapture=false");
    }

    [Test]
    public void PortalProvider_GnomeWaylandOverlayFollowUp_SkipsPortalReentry()
    {
        var runtime = new NoOpRuntime();
        var portalProvider = new PortalCaptureProvider(runtime);
        var request = new LinuxCaptureRequest(
            LinuxCaptureKind.FullScreen,
            new CaptureOptions { LinuxDisallowPortalAfterOverlaySelection = true });

        var gnomeContext = new LinuxCaptureContext(isWayland: true, desktop: "GNOME", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);
        var kdeContext = new LinuxCaptureContext(isWayland: true, desktop: "KDE", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);

        Assert.Multiple(() =>
        {
            Assert.That(portalProvider.CanHandle(request, gnomeContext), Is.False,
                "Portal must not reopen after an overlay selection on GNOME Wayland.");
            Assert.That(portalProvider.CanHandle(request, kdeContext), Is.True,
                "The guard is specific to GNOME overlay follow-up capture.");
        });
    }

    [Test]
    public async Task Coordinator_CancelledProvider_StopsFurtherFallback()
    {
        int portalCalls = 0;
        int x11Calls = 0;

        var providers = new ILinuxCaptureProvider[]
        {
            new TestProvider(
                "portal-cancel",
                LinuxCaptureStage.Portal,
                canHandle: true,
                resultFactory: () => LinuxCaptureResult.Cancelled("portal-cancel"),
                onTryCapture: () => portalCalls++),
            new TestProvider(
                "x11-success",
                LinuxCaptureStage.X11,
                canHandle: true,
                resultFactory: () => LinuxCaptureResult.Success("x11-success", new SKBitmap(1, 1)),
                onTryCapture: () => x11Calls++)
        };

        var coordinator = new LinuxCaptureCoordinator(providers, new WaterfallCapturePolicy());
        var request = new LinuxCaptureRequest(
            LinuxCaptureKind.Region,
            new CaptureOptions { LinuxRegionSelectorPreference = LinuxInteractiveRegionSelectorPreference.PortalDialog });
        var context = new LinuxCaptureContext(isWayland: true, desktop: "GNOME", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);

        var execution = await coordinator.CaptureWithTraceAsync(request, context, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(execution.Result.IsCancelled, Is.True);
            Assert.That(execution.Result.ProviderId, Is.EqualTo("portal-cancel"));
            Assert.That(execution.Trace.FinalOutcome, Is.EqualTo(CaptureDecisionOutcome.Cancelled));
            Assert.That(portalCalls, Is.EqualTo(1));
            Assert.That(x11Calls, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task Coordinator_WhenProviderThrows_FallsBackToNextProviderAndRecordsFailureReason()
    {
        var providers = new ILinuxCaptureProvider[]
        {
            new ThrowingProvider("portal-throws", LinuxCaptureStage.Portal),
            new TestProvider("gnome-success", LinuxCaptureStage.DesktopDbus, canHandle: true, resultFactory: () => LinuxCaptureResult.Success("gnome-success", new SKBitmap(1, 1)))
        };

        var coordinator = new LinuxCaptureCoordinator(providers, new WaterfallCapturePolicy());
        var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, options: null);
        var context = new LinuxCaptureContext(isWayland: true, desktop: "GNOME", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);

        var execution = await coordinator.CaptureWithTraceAsync(request, context, CancellationToken.None);
        execution.Result.Bitmap?.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(execution.Result.ProviderId, Is.EqualTo("gnome-success"));
            Assert.That(execution.Trace.FinalOutcome, Is.EqualTo(CaptureDecisionOutcome.Succeeded));
            Assert.That(execution.Trace.Steps.Count, Is.EqualTo(2));
            Assert.That(execution.Trace.Steps[0].ProviderId, Is.EqualTo("portal-throws"));
            Assert.That(execution.Trace.Steps[0].Outcome, Is.EqualTo(CaptureDecisionOutcome.Failed));
            Assert.That(execution.Trace.Steps[0].Reason, Does.Contain("InvalidOperationException"));
            Assert.That(execution.Trace.Steps[1].ProviderId, Is.EqualTo("gnome-success"));
            Assert.That(execution.Trace.Steps[1].Outcome, Is.EqualTo(CaptureDecisionOutcome.Succeeded));
        });
    }

    [Test]
    public async Task PortalProvider_WhenRuntimeReturnsCancelled_ResponseIsCancelled()
    {
        var runtime = new PortalRuntime(response: 1);
        var provider = new PortalCaptureProvider(runtime);
        var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, options: null);
        var context = new LinuxCaptureContext(isWayland: true, desktop: "KDE", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);

        var result = await provider.TryCaptureAsync(request, context, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCancelled, Is.True);
            Assert.That(result.Bitmap, Is.Null);
            Assert.That(result.ProviderId, Is.EqualTo("portal"));
        });
    }

    [Test]
    public void PortalScreenCapture_FallbackDecision_RespectsCancelResponse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PortalScreenCapture.ShouldTryFallbackLookup(PortalScreenCapture.PortalResponseSuccess), Is.False);
            Assert.That(PortalScreenCapture.ShouldTryFallbackLookup(PortalScreenCapture.PortalResponseCancelled), Is.False);
            Assert.That(PortalScreenCapture.ShouldTryFallbackLookup(PortalScreenCapture.PortalResponseFailed), Is.True);
        });
    }

    [Test]
    public void LinuxScreenCaptureService_GnomeWaylandTransparentOverlay_UsesDirectAreaCapture()
    {
        var context = new LinuxCaptureContext(
            isWayland: true,
            desktop: "GNOME",
            compositor: "WAYLAND",
            isSandboxed: false,
            hasScreenshotPortal: true);
        var options = new CaptureOptions { UseTransparentOverlay = true };

        Assert.That(LinuxScreenCaptureService.ShouldUseDirectGnomeAreaCapture(options, context), Is.True);
    }

    [Test]
    public void LinuxScreenCaptureService_GnomeWaylandOverlayFollowUp_UsesDirectAreaCaptureAndSkipsPortal()
    {
        var context = new LinuxCaptureContext(
            isWayland: true,
            desktop: "GNOME",
            compositor: "WAYLAND",
            isSandboxed: false,
            hasScreenshotPortal: true);
        var options = new CaptureOptions { LinuxDisallowPortalAfterOverlaySelection = true };

        Assert.Multiple(() =>
        {
            Assert.That(LinuxScreenCaptureService.ShouldUseDirectGnomeAreaCapture(options, context), Is.True);
            Assert.That(LinuxScreenCaptureService.ShouldSkipPortalAfterOverlaySelection(options, context), Is.True);
        });
    }

    [Test]
    public void LinuxScreenCaptureService_GnomeWaylandOverlayFollowUp_DirectAreaFailure_RestoresLegacyFullscreenFallback()
    {
        var context = new LinuxCaptureContext(
            isWayland: true,
            desktop: "GNOME",
            compositor: "WAYLAND",
            isSandboxed: false,
            hasScreenshotPortal: true);
        var options = new CaptureOptions
        {
            LinuxDisallowPortalAfterOverlaySelection = true,
            UseTransparentOverlay = true,
            WorkflowId = "rectangle-transparent",
            WorkflowCategory = "ScreenCapture",
            VirtualScreenBoundsForCrop = new Rectangle(10, 20, 300, 200),
            PhysicalVirtualScreenBoundsForCrop = new Rectangle(0, 0, 600, 400),
            PhysicalRectForCrop = new Rectangle(100, 120, 200, 140)
        };

        var fallbackOptions = LinuxScreenCaptureService.CreateFullScreenFallbackOptionsAfterDirectAreaFailure(options, context);

        Assert.Multiple(() =>
        {
            Assert.That(fallbackOptions, Is.Not.Null);
            Assert.That(fallbackOptions, Is.Not.SameAs(options));
            Assert.That(fallbackOptions!.LinuxDisallowPortalAfterOverlaySelection, Is.False);
            Assert.That(fallbackOptions.UseTransparentOverlay, Is.False);
            Assert.That(fallbackOptions.WorkflowId, Is.EqualTo("rectangle-transparent"));
            Assert.That(fallbackOptions.WorkflowCategory, Is.EqualTo("ScreenCapture"));
            Assert.That(fallbackOptions.VirtualScreenBoundsForCrop, Is.EqualTo(new Rectangle(10, 20, 300, 200)));
            Assert.That(fallbackOptions.PhysicalVirtualScreenBoundsForCrop, Is.EqualTo(new Rectangle(0, 0, 600, 400)));
            Assert.That(fallbackOptions.PhysicalRectForCrop, Is.EqualTo(new Rectangle(100, 120, 200, 140)));
            Assert.That(LinuxScreenCaptureService.ShouldSkipPortalAfterOverlaySelection(fallbackOptions, context), Is.False);
        });
    }

    [Test]
    public void LinuxScreenCaptureService_DirectAreaFailureFallback_NonGuardedContexts_ReturnOriginalOptions()
    {
        var kdeContext = new LinuxCaptureContext(
            isWayland: true,
            desktop: "KDE",
            compositor: "WAYLAND",
            isSandboxed: false,
            hasScreenshotPortal: true);
        var options = new CaptureOptions
        {
            LinuxDisallowPortalAfterOverlaySelection = true,
            UseTransparentOverlay = true
        };

        var fallbackOptions = LinuxScreenCaptureService.CreateFullScreenFallbackOptionsAfterDirectAreaFailure(options, kdeContext);

        Assert.That(fallbackOptions, Is.SameAs(options));
    }

    [Test]
    public void LinuxScreenCaptureService_DirectAreaCapture_IsNotUsedForKdeOrNonTransparentCapture()
    {
        var gnomeContext = new LinuxCaptureContext(
            isWayland: true,
            desktop: "GNOME",
            compositor: "WAYLAND",
            isSandboxed: false,
            hasScreenshotPortal: true);
        var kdeContext = new LinuxCaptureContext(
            isWayland: true,
            desktop: "KDE",
            compositor: "WAYLAND",
            isSandboxed: false,
            hasScreenshotPortal: true);

        Assert.Multiple(() =>
        {
            Assert.That(LinuxScreenCaptureService.ShouldUseDirectGnomeAreaCapture(new CaptureOptions { UseTransparentOverlay = false }, gnomeContext), Is.False);
            Assert.That(LinuxScreenCaptureService.ShouldUseDirectGnomeAreaCapture(new CaptureOptions { UseTransparentOverlay = true }, kdeContext), Is.False);
            Assert.That(LinuxScreenCaptureService.ShouldUseDirectGnomeAreaCapture(new CaptureOptions { UseTransparentOverlay = true }, new LinuxCaptureContext(false, "GNOME", "X11", false, true)), Is.False);
            Assert.That(LinuxScreenCaptureService.ShouldUseDirectGnomeAreaCapture(new CaptureOptions { LinuxDisallowPortalAfterOverlaySelection = true }, kdeContext), Is.False);
            Assert.That(LinuxScreenCaptureService.ShouldSkipPortalAfterOverlaySelection(new CaptureOptions { LinuxDisallowPortalAfterOverlaySelection = true }, kdeContext), Is.False);
            Assert.That(LinuxScreenCaptureService.ShouldSkipPortalAfterOverlaySelection(new CaptureOptions { LinuxDisallowPortalAfterOverlaySelection = true }, new LinuxCaptureContext(false, "GNOME", "X11", false, true)), Is.False);
        });
    }

    [Test]
    public void LinuxScreenCaptureService_CreateDirectAreaCaptureRect_RoundsOutward()
    {
        var rect = LinuxScreenCaptureService.CreateDirectAreaCaptureRect(new SKRect(10.2f, 20.4f, 110.6f, 220.1f));

        Assert.That(rect, Is.EqualTo(new SKRectI(10, 20, 111, 221)));
    }

    [Test]
    public void LinuxRegionCaptureCapabilityDetector_X11WithoutNativeSelector_UsesOverlayFallback()
    {
        var context = new LinuxCaptureContext(isWayland: false, desktop: "XFCE", compositor: "X11", isSandboxed: false, hasScreenshotPortal: true);
        var capability = LinuxRegionCaptureCapabilityDetector.Detect(
            context,
            new LinuxRegionCaptureSupportSnapshot(
                HasGnomeShellScreenshot: false,
                HasKdeScreenShot2: false,
                HasSlurp: false));

        Assert.Multiple(() =>
        {
            Assert.That(capability.SupportsNativeRegionCapture, Is.False);
            Assert.That(capability.SupportsLegacyOverlayCapture, Is.True);
            Assert.That(capability.Reason, Does.Contain("overlay fallback"));
        });
    }

    [Test]
    public void LinuxRegionCaptureCapabilityDetector_X11GnomeSelector_SupportsNativeAndOverlay()
    {
        var context = new LinuxCaptureContext(isWayland: false, desktop: "CINNAMON", compositor: "X11", isSandboxed: false, hasScreenshotPortal: true);
        var capability = LinuxRegionCaptureCapabilityDetector.Detect(
            context,
            new LinuxRegionCaptureSupportSnapshot(
                HasGnomeShellScreenshot: true,
                HasKdeScreenShot2: false,
                HasSlurp: false));

        Assert.Multiple(() =>
        {
            Assert.That(capability.SupportsNativeRegionCapture, Is.True);
            Assert.That(capability.SupportsLegacyOverlayCapture, Is.True);
            Assert.That(capability.Reason, Does.Contain("org.gnome.Shell.Screenshot"));
        });
    }

    [Test]
    public void LinuxRegionCaptureCapabilityDetector_X11DesktopNativePortal_SupportsNativeAndOverlay()
    {
        var context = new LinuxCaptureContext(isWayland: false, desktop: "XFCE", compositor: "X11", isSandboxed: false, hasScreenshotPortal: true);
        var capability = LinuxRegionCaptureCapabilityDetector.Detect(
            context,
            new LinuxRegionCaptureSupportSnapshot(
                HasGnomeShellScreenshot: false,
                HasKdeScreenShot2: false,
                HasSlurp: false,
                HasKnownGoodX11PortalBackend: true,
                PrefersPortalForRegionCaptureOnX11: true,
                X11PortalBackendLabel: "xapp"));

        Assert.Multiple(() =>
        {
            Assert.That(capability.SupportsNativeRegionCapture, Is.True);
            Assert.That(capability.SupportsLegacyOverlayCapture, Is.True);
            Assert.That(capability.Reason, Does.Contain("xapp"));
            Assert.That(capability.Reason, Does.Contain("XDG portal backend"));
        });
    }

    [Test]
    public void LinuxRegionCaptureCapabilityDetector_WaylandWithPortal_SupportsNativeAndOverlay()
    {
        var context = new LinuxCaptureContext(isWayland: true, desktop: "GNOME", compositor: "WAYLAND", isSandboxed: false, hasScreenshotPortal: true);
        var capability = LinuxRegionCaptureCapabilityDetector.Detect(
            context,
            new LinuxRegionCaptureSupportSnapshot(
                HasGnomeShellScreenshot: false,
                HasKdeScreenShot2: false,
                HasSlurp: false));

        Assert.Multiple(() =>
        {
            Assert.That(capability.SupportsNativeRegionCapture, Is.True);
            Assert.That(capability.SupportsLegacyOverlayCapture, Is.True);
            Assert.That(capability.Reason, Does.Contain("XDG Screenshot portal"));
        });
    }

    [Test]
    public void LinuxRegionSelectorDiagnosticsDetector_X11Kde_OnlyExposesSupportedSelectors()
    {
        var context = new LinuxCaptureContext(isWayland: false, desktop: "KDE", compositor: "X11", isSandboxed: false, hasScreenshotPortal: true);
        var support = new LinuxRegionCaptureSupportSnapshot(
            HasGnomeShellScreenshot: false,
            HasKdeScreenShot2: true,
            HasSlurp: false);
        var capability = LinuxRegionCaptureCapabilityDetector.Detect(context, support);

        var diagnostics = LinuxRegionSelectorDiagnosticsDetector.Detect(context, support, capability);

        Assert.That(diagnostics.AvailablePreferences, Is.EqualTo(new[]
        {
            LinuxInteractiveRegionSelectorPreference.Automatic,
            LinuxInteractiveRegionSelectorPreference.XerahSOverlay,
            LinuxInteractiveRegionSelectorPreference.DesktopNative,
            LinuxInteractiveRegionSelectorPreference.PortalDialog
        }));
    }

    [Test]
    public void PortalBackendDetector_X11CinnamonWithXappBackend_PrefersPortal()
    {
        var support = PortalBackendDetector.DetectX11RegionSupport(
            desktop: "CINNAMON",
            hasScreenshotPortal: true,
            hasGnomeShellScreenshot: false,
            hasKdeScreenShot2: false,
            backends: new PortalBackendSnapshot(
                HasKde: false,
                HasGnome: false,
                HasGtk: false,
                HasWlr: false,
                HasHyprland: false,
                HasLxqt: false,
                HasXapp: true));

        Assert.Multiple(() =>
        {
            Assert.That(support.HasKnownGoodX11PortalBackend, Is.True);
            Assert.That(support.PrefersPortalForRegionCaptureOnX11, Is.True);
            Assert.That(support.BackendLabel, Is.EqualTo("xapp"));
        });
    }

    [Test]
    public void PortalBackendDetector_X11GtkBackendOnly_DoesNotTreatPortalAsKnownGood()
    {
        var support = PortalBackendDetector.DetectX11RegionSupport(
            desktop: "CINNAMON",
            hasScreenshotPortal: true,
            hasGnomeShellScreenshot: false,
            hasKdeScreenShot2: false,
            backends: new PortalBackendSnapshot(
                HasKde: false,
                HasGnome: false,
                HasGtk: true,
                HasWlr: false,
                HasHyprland: false,
                HasLxqt: false,
                HasXapp: false));

        Assert.Multiple(() =>
        {
            Assert.That(support.HasKnownGoodX11PortalBackend, Is.False);
            Assert.That(support.PrefersPortalForRegionCaptureOnX11, Is.False);
            Assert.That(support.BackendLabel, Is.Null);
        });
    }

    [Test]
    public void WaylandPortalRecordingService_GnomeWayland_PrefersCpuGStreamer()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WaylandPortalRecordingService.ShouldPreferCpuGStreamerPath("wayland", "GNOME", null, null), Is.True);
            Assert.That(WaylandPortalRecordingService.ShouldPreferCpuGStreamerPath("wayland", "ubuntu:GNOME", null, null), Is.True);
            Assert.That(WaylandPortalRecordingService.ShouldPreferCpuGStreamerPath("wayland", null, "gnome", null), Is.True);
        });
    }

    [Test]
    public void WaylandPortalRecordingService_NonGnomeOrNonWayland_DoesNotPreferCpuGStreamer()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WaylandPortalRecordingService.ShouldPreferCpuGStreamerPath("wayland", "Hyprland", null, null), Is.False);
            Assert.That(WaylandPortalRecordingService.ShouldPreferCpuGStreamerPath("wayland", null, null, "sway"), Is.False);
            Assert.That(WaylandPortalRecordingService.ShouldPreferCpuGStreamerPath("x11", "GNOME", null, null), Is.False);
        });
    }

    private sealed class TestProvider : ILinuxCaptureProvider
    {
        private readonly bool _canHandle;
        private readonly System.Func<LinuxCaptureResult> _resultFactory;
        private readonly System.Action? _onTryCapture;

        public TestProvider(
            string providerId,
            LinuxCaptureStage stage,
            bool canHandle,
            System.Func<LinuxCaptureResult> resultFactory,
            System.Action? onTryCapture = null)
        {
            ProviderId = providerId;
            Stage = stage;
            _canHandle = canHandle;
            _resultFactory = resultFactory;
            _onTryCapture = onTryCapture;
        }

        public string ProviderId { get; }

        public LinuxCaptureStage Stage { get; }

        public bool CanHandle(LinuxCaptureRequest request, ILinuxCaptureContext context)
        {
            return _canHandle;
        }

        public Task<LinuxCaptureResult> TryCaptureAsync(
            LinuxCaptureRequest request,
            ILinuxCaptureContext context,
            CancellationToken cancellationToken = default)
        {
            _onTryCapture?.Invoke();
            return Task.FromResult(_resultFactory());
        }
    }

    private sealed class ThrowingProvider : ILinuxCaptureProvider
    {
        public ThrowingProvider(string providerId, LinuxCaptureStage stage)
        {
            ProviderId = providerId;
            Stage = stage;
        }

        public string ProviderId { get; }

        public LinuxCaptureStage Stage { get; }

        public bool CanHandle(LinuxCaptureRequest request, ILinuxCaptureContext context)
        {
            return true;
        }

        public Task<LinuxCaptureResult> TryCaptureAsync(
            LinuxCaptureRequest request,
            ILinuxCaptureContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class NoOpRuntime : ILinuxCaptureRuntime
    {
        public uint PortalCancelledResponseCode => 1;

        public Task<(SKBitmap? bitmap, uint response)> TryPortalCaptureAsync(LinuxCaptureKind kind, CaptureOptions? options)
        {
            return Task.FromResult<(SKBitmap?, uint)>((null, 2));
        }

        public Task<SKBitmap?> TryKdeDbusCaptureAsync(LinuxCaptureKind kind, CaptureOptions? options)
        {
            return Task.FromResult<SKBitmap?>(null);
        }

        public Task<SKBitmap?> TryGnomeDbusCaptureAsync(LinuxCaptureKind kind, CaptureOptions? options)
        {
            return Task.FromResult<SKBitmap?>(null);
        }

        public Task<SKBitmap?> TryWlrootsCaptureAsync(LinuxCaptureKind kind, string? desktop, CaptureOptions? options)
        {
            return Task.FromResult<SKBitmap?>(null);
        }

        public Task<SKBitmap?> TryX11NativeCaptureAsync(LinuxCaptureKind kind, IWindowService? windowService, CaptureOptions? options)
        {
            return Task.FromResult<SKBitmap?>(null);
        }

        public Task<SKBitmap?> TryCliCaptureAsync(
            LinuxCaptureKind kind,
            string? desktop,
            IWindowService? windowService,
            CaptureOptions? options)
        {
            return Task.FromResult<SKBitmap?>(null);
        }
    }

    private sealed class PortalRuntime : ILinuxCaptureRuntime
    {
        private readonly uint _response;

        public PortalRuntime(uint response)
        {
            _response = response;
        }

        public uint PortalCancelledResponseCode => 1;

        public Task<(SKBitmap? bitmap, uint response)> TryPortalCaptureAsync(LinuxCaptureKind kind, CaptureOptions? options)
        {
            return Task.FromResult<(SKBitmap?, uint)>((null, _response));
        }

        public Task<SKBitmap?> TryKdeDbusCaptureAsync(LinuxCaptureKind kind, CaptureOptions? options)
        {
            return Task.FromResult<SKBitmap?>(null);
        }

        public Task<SKBitmap?> TryGnomeDbusCaptureAsync(LinuxCaptureKind kind, CaptureOptions? options)
        {
            return Task.FromResult<SKBitmap?>(null);
        }

        public Task<SKBitmap?> TryWlrootsCaptureAsync(LinuxCaptureKind kind, string? desktop, CaptureOptions? options)
        {
            return Task.FromResult<SKBitmap?>(null);
        }

        public Task<SKBitmap?> TryX11NativeCaptureAsync(LinuxCaptureKind kind, IWindowService? windowService, CaptureOptions? options)
        {
            return Task.FromResult<SKBitmap?>(null);
        }

        public Task<SKBitmap?> TryCliCaptureAsync(
            LinuxCaptureKind kind,
            string? desktop,
            IWindowService? windowService,
            CaptureOptions? options)
        {
            return Task.FromResult<SKBitmap?>(null);
        }
    }
}
