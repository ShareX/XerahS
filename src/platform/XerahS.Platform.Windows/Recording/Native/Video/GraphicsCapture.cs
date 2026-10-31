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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using global::Windows.Graphics.Capture;
using global::Windows.Graphics.DirectX;
using global::Windows.Security.Authorization.AppCapabilityAccess;
using WinRT;
using Box = Vortice.Mathematics.Box;

namespace XerahS.Platform.Windows.Recording.Native;

internal sealed unsafe class GraphicsCapture : IDisposable
{
    internal sealed record Target(GraphicsCaptureItem Item, Rectangle Source, Point Destination, nint Monitor, nint WindowHandle = 0);
    private sealed class Source : IDisposable
    {
        public required Target Target;
        public required Direct3D11CaptureFramePool Pool;
        public required GraphicsCaptureSession Session;
        public required int InitialWidth;
        public required int InitialHeight;
        public global::Windows.Foundation.TypedEventHandler<GraphicsCaptureItem, object>? ClosedHandler;
        public bool HasFrame;
        public int Closed;
        public void Dispose() { Target.Item.Closed -= ClosedHandler; Session.Dispose(); Pool.Dispose(); }
    }

    private readonly GraphicsDevice graphics;
    private readonly GpuVideoProcessor processor;
    private readonly bool cursor, borderless;
    private readonly int fps;
    private readonly List<Source> sources = new();
    public int Width { get; }
    public int Height { get; }
    public bool HasFrame => sources.All(x => x.HasFrame);
    public long LatestTimestamp { get; private set; }
    public bool HasEnded => sources.Any(x => Volatile.Read(ref x.Closed) != 0);

    public static List<Target> GetTargets(NativeRecordingOptions options, out int width, out int height)
    {
        if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("Windows Graphics Capture is unavailable in this Windows session.");
        if (options.WindowHandle != 0)
        {
            if (!RecordingInterop.IsWindow(options.WindowHandle)) throw new ArgumentException("The capture window no longer exists.");
            GraphicsCaptureItem item = CreateItem(options.WindowHandle, true);
            Rectangle windowRegion = RecordingRegionLayout.Normalize(new Rectangle(0, 0, item.Size.Width, item.Size.Height));
            width = windowRegion.Width;
            height = windowRegion.Height;
            nint monitor = RecordingInterop.MonitorFromWindow(options.WindowHandle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            return [new(item, new Rectangle(0, 0, width, height), Point.Empty, monitor, options.WindowHandle)];
        }

        Rectangle region = RecordingRegionLayout.Normalize(options.Region);
        width = region.Width;
        height = region.Height;
        List<Target> targets = new();
        foreach (RecordingRegionSlice slice in ComputeLayout(region, EnumerateMonitors()))
        {
            GraphicsCaptureItem item = CreateItem(slice.Monitor, false);
            targets.Add(new(item, slice.Source, slice.Destination, slice.Monitor));
        }
        if (targets.Count == 0) throw new ArgumentException("The capture region does not intersect a display.");
        return targets;
    }

    /// <summary>One display's share of a desktop region: the crop in monitor space and its offset in the output canvas.</summary>
    internal static List<RecordingRegionSlice> ComputeLayout(Rectangle region, IEnumerable<(nint Monitor, Rectangle Bounds)> monitors) =>
        RecordingRegionLayout.Compute(region, monitors);

    internal static List<(nint Monitor, Rectangle Bounds)> EnumerateMonitors()
    {
        List<(nint Monitor, Rectangle Bounds)> monitors = new();
        GCHandle handle = GCHandle.Alloc(monitors);
        try
        {
            if (!RecordingInterop.EnumDisplayMonitors(0, null, &CollectMonitor, GCHandle.ToIntPtr(handle)))
                throw new System.ComponentModel.Win32Exception();
        }
        finally { handle.Free(); }
        return monitors;
    }

    /// <summary>
    /// Moves a desktop-region capture without recreating its WGC sessions. Returns false when the moved region
    /// needs a different set of displays; the caller then retargets.
    /// </summary>
    public bool TryMove(Rectangle region)
    {
        if (sources.Count == 0 || sources.Any(x => x.Target.WindowHandle != 0)) return false;
        region = new Rectangle(region.Location, new Size(Width, Height));
        List<RecordingRegionSlice> slices = ComputeLayout(region, EnumerateMonitors());
        if (slices.Count != sources.Count || slices.Any(slice => !sources.Any(x => x.Target.Monitor == slice.Monitor))) return false;
        foreach (Source source in sources)
        {
            RecordingRegionSlice slice = slices.First(x => x.Monitor == source.Target.Monitor);
            source.Target = source.Target with { Source = slice.Source, Destination = slice.Destination };
        }
        // Uncovered canvas areas (off-desktop parts of the region) must not keep pixels from the previous position.
        processor.Clear();
        return true;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL CollectMonitor(nint monitor, nint dc, RECT* bounds, nint state)
    {
        List<(nint Monitor, Rectangle Bounds)> monitors = (List<(nint, Rectangle)>)GCHandle.FromIntPtr(state).Target!;
        monitors.Add((monitor, Rectangle.FromLTRB(bounds->left, bounds->top, bounds->right, bounds->bottom)));
        return true;
    }

    private static GraphicsCaptureItem CreateItem(nint handle, bool window)
    {
        const string name = "Windows.Graphics.Capture.GraphicsCaptureItem";
        nint className;
        fixed (char* chars = name) RecordingInterop.WindowsCreateString(chars, (uint)name.Length, &className).ThrowOnFailure();
        try
        {
            Guid iid = typeof(IGraphicsCaptureItemInterop).GUID;
            void* rawFactory;
            RecordingInterop.RoGetActivationFactory(className, &iid, &rawFactory).ThrowOnFailure();
            using ComPtr<IGraphicsCaptureItemInterop> factory = new((IGraphicsCaptureItemInterop*)rawFactory);
            Guid itemIid = new("79c3f95b-31f7-4ec2-a464-632ef5d30760");
            void* item;
            if (window) factory.Pointer->CreateForWindow(handle, &itemIid, &item).ThrowOnFailure();
            else factory.Pointer->CreateForMonitor(handle, &itemIid, &item).ThrowOnFailure();
            try { return MarshalInspectable<GraphicsCaptureItem>.FromAbi((nint)item); }
            finally { ((IUnknown*)item)->Release(); }
        }
        finally { RecordingInterop.WindowsDeleteString(className); }
    }

    public GraphicsCapture(GraphicsDevice graphics, GpuVideoProcessor processor, List<Target> targets, bool cursor, int fps, bool hideBorder = true)
    {
        this.graphics = graphics;
        this.processor = processor;
        this.cursor = cursor;
        this.fps = fps;
        Width = processor.Width;
        Height = processor.Height;
        try
        {
            borderless = hideBorder && RequestBorderlessAccess();
            CreateSources(targets);
        }
        catch { Dispose(); throw; }
    }

    private void CreateSources(List<Target> targets)
    {
        foreach (Target target in targets)
        {
            Direct3D11CaptureFramePool pool = Direct3D11CaptureFramePool.CreateFreeThreaded(graphics.WinRTDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, target.Item.Size);
            GraphicsCaptureSession session;
            try { session = pool.CreateCaptureSession(target.Item); }
            catch { pool.Dispose(); throw; }
            Source source = new() { Target = target, Pool = pool, Session = session, InitialWidth = target.Item.Size.Width, InitialHeight = target.Item.Size.Height };
            sources.Add(source);
            ConfigureCaptureInterval(session, fps);
            session.IsCursorCaptureEnabled = cursor;
            if (borderless && IsBorderRequiredSupported)
            {
                try { session.IsBorderRequired = false; }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Windows could not hide the capture border: {ex.Message}"); }
            }
            source.ClosedHandler = (_, _) => Interlocked.Exchange(ref source.Closed, 1);
            target.Item.Closed += source.ClosedHandler;
        }
    }

    /// <summary>Called on the recording worker while media time is paused.</summary>
    public void Retarget(List<Target> targets)
    {
        DisposeSources();
        processor.Clear();
        LatestTimestamp = 0;
        try
        {
            CreateSources(targets);
            Start();
        }
        catch { DisposeSources(); throw; }
    }

    private static void ConfigureCaptureInterval(GraphicsCaptureSession session, int fps)
    {
        using var marshaler = MarshalInspectable<GraphicsCaptureSession>.CreateMarshaler(session);
        Guid iid = typeof(IGraphicsCaptureSession5).GUID;
        void* rawSession;
        HRESULT result = ((IUnknown*)marshaler.ThisPtr)->QueryInterface(&iid, &rawSession);
        if (result.Value == unchecked((int)0x80004002)) return; // E_NOINTERFACE: older Windows uses its existing capture cadence.
        if (result.Failed) return; // Capture interval control is an optional driver/OS hint.
        using ComPtr<IGraphicsCaptureSession5> extended = new((IGraphicsCaptureSession5*)rawSession);
        // Newer Windows can otherwise throttle WGC to 60 FPS even on a high-refresh display.
        extended.Pointer->SetMinUpdateInterval(TimeSpan.TicksPerSecond / fps);
    }

    private static readonly bool IsBorderRequiredSupported =
        global::Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent("Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired");

    private static bool RequestBorderlessAccess()
    {
        // Windows 10 before 20348 has no borderless capture API; the yellow border is then always shown.
        if (!IsBorderRequiredSupported ||
            !global::Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Graphics.Capture.GraphicsCaptureAccess")) return false;
        try
        {
            // Request access before creating sessions so Windows does not briefly show the border.
            // This constructor runs on the recorder's MTA worker, keeping consent off the UI thread.
            return GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless)
                .AsTask().GetAwaiter().GetResult() == AppCapabilityAccessStatus.Allowed;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
            // Borderless access is optional; preserve recording when a host or policy disallows it.
            System.Diagnostics.Debug.WriteLine($"Borderless screen capture is unavailable: {ex.Message}");
            return false;
        }
    }

    public void Start()
    {
        foreach (Source source in sources) source.Session.StartCapture();
    }

    /// <summary>Only this worker uses the immediate context. Frame-pool surfaces are released after GPU copying.</summary>
    public void Update()
    {
        foreach (Source source in sources)
        {
            if (Volatile.Read(ref source.Closed) != 0) continue;
            // Closed is not always delivered before capture stops publishing frames. HWND validity
            // gives the worker a deterministic end condition even without a WinRT event dispatcher.
            if (source.Target.WindowHandle != 0 && !RecordingInterop.IsWindow(source.Target.WindowHandle))
            {
                Interlocked.Exchange(ref source.Closed, 1);
                continue;
            }
            Direct3D11CaptureFrame? latest = null;
            try
            {
                // Bounded drain: the producer may continue publishing while this thread consumes.
                for (int i = 0; i < 2; i++)
                {
                    Direct3D11CaptureFrame? next = source.Pool.TryGetNextFrame();
                    if (next == null) break;
                    latest?.Dispose(); latest = next;
                }
                if (latest == null) continue;
                if (latest.ContentSize.Width != source.InitialWidth || latest.ContentSize.Height != source.InitialHeight)
                {
                    Interlocked.Exchange(ref source.Closed, 1);
                    continue;
                }
                Rectangle crop = source.Target.Source;
                int copyWidth = Math.Min(crop.Width, latest.ContentSize.Width - crop.X);
                int copyHeight = Math.Min(crop.Height, latest.ContentSize.Height - crop.Y);
                if (copyWidth <= 0 || copyHeight <= 0) continue;
                using var surface = MarshalInterface<global::Windows.Graphics.DirectX.Direct3D11.IDirect3DSurface>.CreateMarshaler(latest.Surface);
                Guid iid = typeof(IDirect3DDxgiInterfaceAccess).GUID;
                void* rawAccess;
                ((IUnknown*)surface.ThisPtr)->QueryInterface(&iid, &rawAccess).ThrowOnFailure();
                using IDirect3DDxgiInterfaceAccess access = new((nint)rawAccess);
                using ID3D11Texture2D texture = access.GetInterface<ID3D11Texture2D>();
                // Clear padding outside the source (odd dimensions are cropped to even NV12 dimensions).
                if (sources.Count == 1 && source.Target.Source.Location == Point.Empty) processor.Clear();
                Box box = new(crop.X, crop.Y, 0, crop.X + copyWidth, crop.Y + copyHeight, 1);
                graphics.Context.CopySubresourceRegion(processor.Canvas, 0,
                    (uint)source.Target.Destination.X, (uint)source.Target.Destination.Y, 0, texture, 0, box);
                source.HasFrame = true;
                LatestTimestamp = Math.Max(LatestTimestamp, latest.SystemRelativeTime.Ticks);
            }
            finally { latest?.Dispose(); }
        }
    }

    private void DisposeSources()
    {
        foreach (Source source in sources)
        {
            source.Dispose();
        }
        sources.Clear();
    }

    public void Dispose() => DisposeSources();
}
