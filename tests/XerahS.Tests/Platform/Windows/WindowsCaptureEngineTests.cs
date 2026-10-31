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

using System.Drawing;
using System.Runtime.InteropServices;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Windows.Capture;
using XerahS.Platform.Windows.Capture.Engine;

namespace XerahS.Tests.Platform.Windows;

public class WindowsCaptureEngineTests
{
    private static readonly CaptureBackendKind[] AllBackends =
    {
        CaptureBackendKind.Gdi,
        CaptureBackendKind.GraphicsCapture,
        CaptureBackendKind.DesktopDuplication
    };

    [Test]
    public void ResolveChain_PrefersDesktopDuplicationThenGraphicsCaptureThenGdi()
    {
        var chain = CaptureBackendPolicy.ResolveChain(modernCaptureRequested: true, AllBackends);

        Assert.That(chain, Is.EqualTo(new[]
        {
            CaptureBackendKind.DesktopDuplication,
            CaptureBackendKind.GraphicsCapture,
            CaptureBackendKind.Gdi
        }));
    }

    [Test]
    public void ResolveChain_UsesOnlyGdiWhenModernCaptureIsOff()
    {
        var chain = CaptureBackendPolicy.ResolveChain(modernCaptureRequested: false, AllBackends);

        Assert.That(chain, Is.EqualTo(new[] { CaptureBackendKind.Gdi }));
    }

    [Test]
    public void ResolveChain_SkipsBackendsThisMachineCannotRun()
    {
        var available = new[] { CaptureBackendKind.Gdi, CaptureBackendKind.DesktopDuplication };

        var chain = CaptureBackendPolicy.ResolveChain(modernCaptureRequested: true, available);

        Assert.That(chain, Is.EqualTo(new[] { CaptureBackendKind.DesktopDuplication, CaptureBackendKind.Gdi }));
    }

    [Test]
    public void Capture_FallsBackInPolicyOrderUntilABackendReturnsAnImage()
    {
        var calls = new List<CaptureBackendKind>();
        using var expected = new SKBitmap(1, 1);
        var engine = new ScreenCaptureEngine(new IScreenCaptureBackend[]
        {
            new FakeBackend(CaptureBackendKind.Gdi, calls, () => throw new InvalidOperationException("must not run")),
            new FakeBackend(CaptureBackendKind.GraphicsCapture, calls, () => expected),
            new FakeBackend(CaptureBackendKind.DesktopDuplication, calls, () => throw new InvalidOperationException("lost device"))
        });

        SKBitmap? result = engine.Capture(ScreenCaptureRequest.ForVirtualDesktop(options: null));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(expected));
            Assert.That(calls, Is.EqualTo(new[] { CaptureBackendKind.DesktopDuplication, CaptureBackendKind.GraphicsCapture }));
        });
    }

    [Test]
    public void Capture_UsesOnlyGdiWhenModernCaptureIsOff()
    {
        var calls = new List<CaptureBackendKind>();
        var engine = new ScreenCaptureEngine(new IScreenCaptureBackend[]
        {
            new FakeBackend(CaptureBackendKind.DesktopDuplication, calls, () => new SKBitmap(1, 1)),
            new FakeBackend(CaptureBackendKind.Gdi, calls, () => null)
        });

        SKBitmap? result = engine.Capture(ScreenCaptureRequest.ForVirtualDesktop(new CaptureOptions { UseModernCapture = false }));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(calls, Is.EqualTo(new[] { CaptureBackendKind.Gdi }));
        });
    }

    [Test]
    public void Engine_RejectsTwoBackendsOfTheSameKind()
    {
        var calls = new List<CaptureBackendKind>();

        Assert.Throws<ArgumentException>(() => _ = new ScreenCaptureEngine(new IScreenCaptureBackend[]
        {
            new FakeBackend(CaptureBackendKind.Gdi, calls, () => null),
            new FakeBackend(CaptureBackendKind.Gdi, calls, () => null)
        }));
    }

    [TestCase(null, false, false)]
    [TestCase(true, true, false)]
    [TestCase(false, false, true)]
    public void Request_KeepsCursorSemanticsOfEachBackend(bool? showCursor, bool drawCursor, bool excluded)
    {
        CaptureOptions? options = showCursor.HasValue ? new CaptureOptions { ShowCursor = showCursor.Value } : null;

        var request = ScreenCaptureRequest.ForVirtualDesktop(options);

        Assert.Multiple(() =>
        {
            Assert.That(request.DrawCursor, Is.EqualTo(drawCursor));
            Assert.That(request.CursorExplicitlyExcluded, Is.EqualTo(excluded));
        });
    }

    [Test]
    public void ResolveCaptureRect_ClampsRectanglesToTheVirtualScreen()
    {
        var virtualBounds = new Rectangle(-100, 0, 300, 200);
        var request = ScreenCaptureRequest.ForRectangle(new SKRect(-150.5f, 10.2f, 50.1f, 400f), options: null);

        bool resolved = CaptureRectResolver.TryResolve(request, virtualBounds, out Rectangle captureRect);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(captureRect, Is.EqualTo(Rectangle.FromLTRB(-100, 10, 51, 200)));
        });
    }

    [Test]
    public void ResolveCaptureRect_ReadsWindowBoundsAsIs()
    {
        var virtualBounds = new Rectangle(0, 0, 100, 100);
        var windowBounds = new Rectangle(-20, 50, 80, 90);
        var request = ScreenCaptureRequest.ForWindowBounds(windowBounds, options: null);

        bool resolved = CaptureRectResolver.TryResolve(request, virtualBounds, out Rectangle captureRect);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(captureRect, Is.EqualTo(windowBounds));
        });
    }

    [Test]
    public void ResolveCaptureRect_UsesVirtualScreenForFullScreen()
    {
        var virtualBounds = new Rectangle(-1920, 0, 3840, 1080);

        bool resolved = CaptureRectResolver.TryResolve(ScreenCaptureRequest.ForVirtualDesktop(options: null), virtualBounds, out Rectangle captureRect);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(captureRect, Is.EqualTo(virtualBounds));
        });
    }

    [Test]
    public void TryMapCropToDesktop_MapsCropBackToDesktopCoordinatesAtOneToOneScale()
    {
        var desktopBounds = new Rectangle(-1920, -200, 3840, 1280);
        var virtualBounds = new Rectangle(-1920, -200, 3840, 1280);
        var rect = new SKRect(-10.5f, 0f, 100.2f, 50f);
        Assert.That(DxgiCropRectHelper.TryCreateCropRect(rect, virtualBounds, desktopBounds.Width, desktopBounds.Height, out SKRectI cropRect), Is.True);

        bool mapped = DxgiCropRectHelper.TryMapCropToDesktop(cropRect, desktopBounds, virtualBounds, out Rectangle desktopRect);

        Assert.Multiple(() =>
        {
            Assert.That(mapped, Is.True);
            Assert.That(desktopRect, Is.EqualTo(Rectangle.FromLTRB(-11, 0, 101, 50)));
        });
    }

    [Test]
    public void TryMapCropToDesktop_RefusesScaledCrops()
    {
        var desktopBounds = new Rectangle(0, 0, 3840, 2160);
        var virtualBounds = new Rectangle(0, 0, 1920, 1080);

        bool mapped = DxgiCropRectHelper.TryMapCropToDesktop(new SKRectI(0, 0, 10, 10), desktopBounds, virtualBounds, out _);

        Assert.That(mapped, Is.False);
    }

    [Test]
    public void SetOpaque_ForcesAlphaWithoutTouchingColourOrRowPadding()
    {
        const int width = 2;
        const int height = 2;
        const int stride = 12; // 8 bytes of pixels plus 4 bytes of padding per row
        byte[] pixels =
        {
            1, 2, 3, 0,   4, 5, 6, 7,   9, 9, 9, 9,
            10, 11, 12, 0, 13, 14, 15, 255, 9, 9, 9, 9
        };

        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            BgraRowCopyHelper.SetOpaque(handle.AddrOfPinnedObject(), stride, width, height);
        }
        finally
        {
            handle.Free();
        }

        Assert.That(pixels, Is.EqualTo(new byte[]
        {
            1, 2, 3, 255,   4, 5, 6, 255,   9, 9, 9, 9,
            10, 11, 12, 255, 13, 14, 15, 255, 9, 9, 9, 9
        }));
    }

    private sealed class FakeBackend : IScreenCaptureBackend
    {
        private readonly List<CaptureBackendKind> _calls;
        private readonly Func<SKBitmap?> _capture;

        public FakeBackend(CaptureBackendKind kind, List<CaptureBackendKind> calls, Func<SKBitmap?> capture)
        {
            Kind = kind;
            _calls = calls;
            _capture = capture;
        }

        public CaptureBackendKind Kind { get; }

        public string Name => Kind.ToString();

        public SKBitmap? TryCapture(ScreenCaptureRequest request)
        {
            _calls.Add(Kind);
            return _capture();
        }
    }
}
