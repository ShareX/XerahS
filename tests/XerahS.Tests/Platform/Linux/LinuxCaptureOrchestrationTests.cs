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
using System.Threading;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.Contracts;
using XerahS.Platform.Linux.Capture.Orchestration;
using XerahS.Platform.Linux.Capture.Providers;

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
    public async Task Coordinator_TraceRecordsSkipFailSuccessInOrder()
    {
        var providers = new ILinuxCaptureProvider[]
        {
            new TestProvider("portal-skip", LinuxCaptureStage.Portal, canHandle: false, resultFactory: () => LinuxCaptureResult.Failure("portal-skip")),
            new TestProvider("portal-fail", LinuxCaptureStage.Portal, canHandle: true, resultFactory: () => LinuxCaptureResult.Failure("portal-fail")),
            new TestProvider("kde-success", LinuxCaptureStage.DesktopDbus, canHandle: true, resultFactory: () => LinuxCaptureResult.Success("kde-success", new SKBitmap(1, 1)))
        };

        var coordinator = new LinuxCaptureCoordinator(providers, new WaterfallCapturePolicy());
        var request = new LinuxCaptureRequest(LinuxCaptureKind.Region, options: null);
        var context = new LinuxCaptureContext(isWayland: false, desktop: "KDE", compositor: "X11", isSandboxed: false, hasScreenshotPortal: true);

        var execution = await coordinator.CaptureWithTraceAsync(request, context, CancellationToken.None);
        execution.Result.Bitmap?.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(execution.Result.ProviderId, Is.EqualTo("kde-success"));
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

    private sealed class TestProvider : ILinuxCaptureProvider
    {
        private readonly bool _canHandle;
        private readonly System.Func<LinuxCaptureResult> _resultFactory;

        public TestProvider(string providerId, LinuxCaptureStage stage, bool canHandle, System.Func<LinuxCaptureResult> resultFactory)
        {
            ProviderId = providerId;
            Stage = stage;
            _canHandle = canHandle;
            _resultFactory = resultFactory;
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
            return Task.FromResult(_resultFactory());
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
}
