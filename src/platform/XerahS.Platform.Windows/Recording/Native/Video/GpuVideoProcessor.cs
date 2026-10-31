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

using SharpGen.Runtime;
using Vortice;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace XerahS.Platform.Windows.Recording.Native;

/// <summary>Crops BGRA surfaces and converts to limited-range BT.709 NV12 on the GPU.</summary>
internal sealed class GpuVideoProcessor : IDisposable
{
    private readonly GraphicsDevice graphics;
    private ID3D11VideoDevice videoDevice = null!;
    private ID3D11VideoContext videoContext = null!;
    private ID3D11VideoProcessorEnumerator enumerator = null!;
    private ID3D11VideoProcessor processor = null!;
    private ID3D11VideoProcessorInputView input = null!;
    private ID3D11RenderTargetView canvasView = null!;
    private readonly VideoProcessorStream[] streams = new VideoProcessorStream[1];
    public ID3D11Texture2D Canvas { get; private set; } = null!;
    public int Width { get; }
    public int Height { get; }

    public GpuVideoProcessor(GraphicsDevice graphics, int width, int height, int fps)
    {
        this.graphics = graphics;
        Width = width;
        Height = height;
        try
        {
            videoDevice = graphics.Device.QueryInterface<ID3D11VideoDevice>();
            videoContext = graphics.Context.QueryInterface<ID3D11VideoContext>();
            VideoProcessorContentDescription desc = new()
            {
                InputFrameFormat = VideoFrameFormat.Progressive,
                InputFrameRate = new((uint)fps, 1),
                OutputFrameRate = new((uint)fps, 1),
                InputWidth = (uint)width,
                InputHeight = (uint)height,
                OutputWidth = (uint)width,
                OutputHeight = (uint)height,
                Usage = VideoUsage.PlaybackNormal
            };
            enumerator = videoDevice.CreateVideoProcessorEnumerator(desc);
            if ((enumerator.CheckVideoProcessorFormat(Format.NV12) & VideoProcessorFormatSupport.Output) == 0)
                throw new NotSupportedException("The GPU cannot produce NV12 video surfaces.");
            processor = videoDevice.CreateVideoProcessor(enumerator, 0);
            Canvas = graphics.CreateTexture(width, height, Format.B8G8R8A8_UNorm, BindFlags.RenderTarget);
            canvasView = graphics.Device.CreateRenderTargetView(Canvas);
            Clear();
            input = videoDevice.CreateVideoProcessorInputView(Canvas, enumerator,
                new VideoProcessorInputViewDescription { ViewDimension = VideoProcessorInputViewDimension.Texture2D });
            streams[0] = new() { Enable = true, InputSurface = input };
            videoContext.VideoProcessorSetStreamColorSpace(processor, 0, new VideoProcessorColorSpace { RGB_Range = 0, YCbCr_Matrix = 1, Nominal_Range = 2 });
            videoContext.VideoProcessorSetOutputColorSpace(processor, new VideoProcessorColorSpace { YCbCr_Matrix = 1, Nominal_Range = 1 });
            videoContext.VideoProcessorSetStreamFrameFormat(processor, 0, VideoFrameFormat.Progressive);
            videoContext.VideoProcessorSetStreamAutoProcessingMode(processor, 0, false);
            RawRect rect = new(0, 0, width, height);
            videoContext.VideoProcessorSetStreamSourceRect(processor, 0, true, rect);
            videoContext.VideoProcessorSetStreamDestRect(processor, 0, true, rect);
            videoContext.VideoProcessorSetOutputTargetRect(processor, true, rect);
        }
        catch { Dispose(); throw; }
    }

    public void Clear() => graphics.Context.ClearRenderTargetView(canvasView, new Color4(0, 0, 0, 1));

    public ID3D11VideoProcessorOutputView CreateOutputView(ID3D11Texture2D texture) =>
        videoDevice.CreateVideoProcessorOutputView(texture, enumerator,
            new VideoProcessorOutputViewDescription { ViewDimension = VideoProcessorOutputViewDimension.Texture2D });

    public void Convert(ID3D11VideoProcessorOutputView output, uint frameNumber)
    {
        streams[0].InputSurface = input;
        videoContext.VideoProcessorBlt(processor, output, frameNumber, 1, streams).CheckError();
    }

    public void Dispose()
    {
        input?.Dispose(); canvasView?.Dispose(); Canvas?.Dispose(); processor?.Dispose();
        enumerator?.Dispose(); videoContext?.Dispose(); videoDevice?.Dispose();
    }
}
