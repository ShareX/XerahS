#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

using System.Runtime.InteropServices;
using XerahS.ScreenCapture.ScreenRecording;

namespace XerahS.Platform.Windows.Recording;

/// <summary>
/// Media Foundation encoder for H.264/MP4 output
/// Uses IMFSinkWriter for hardware-accelerated encoding
/// Stage 1: H.264 only, Stage 3: Hardware encoder detection
/// </summary>
public class MediaFoundationEncoder : IVideoEncoder
{
    private IMFSinkWriter? _sinkWriter;
    private int _streamIndex;
    private long _sampleTime;
    private VideoFormat? _format;
    private string? _outputPath;
    private bool _initialized;
    private bool _disposed;
    private readonly object _lock = new();

    /// <summary>
    /// Check if Media Foundation is available on this system
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            try
            {
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF", "Checking Media Foundation availability...");

                // Try to initialize MF
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF", $"Calling MFStartup(version={MF_VERSION:X}, flags=MFSTARTUP_FULL)...");
                var startTime = System.Diagnostics.Stopwatch.StartNew();
                var hr = MFStartup(MF_VERSION, MFSTARTUP_FULL);
                startTime.Stop();

                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF", $"MFStartup() returned HRESULT: 0x{hr:X8} (took {startTime.ElapsedMilliseconds}ms)");

                if (hr == 0)
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF", "✓ MFStartup succeeded (S_OK)");
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF", "Calling MFShutdown()...");
                    MFShutdown();
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF", "✓ Media Foundation is available");
                    return true;
                }
                else
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF", $"✗ MFStartup failed with HRESULT 0x{hr:X8}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF", $"✗ EXCEPTION checking MF availability: {ex.GetType().Name}: {ex.Message}");
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF", $"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
    }

    public void Initialize(VideoFormat format, string outputPath)
    {
        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MediaFoundationEncoder));
            if (_initialized) throw new InvalidOperationException("Encoder already initialized");

            _format = format ?? throw new ArgumentNullException(nameof(format));
            _outputPath = outputPath ?? throw new ArgumentNullException(nameof(outputPath));

            try
            {
                // Initialize Media Foundation
                var hr = MFStartup(MF_VERSION, MFSTARTUP_FULL);
                if (hr != 0)
                {
                    throw new COMException("Failed to initialize Media Foundation", hr);
                }

                // Create sink writer
                CreateSinkWriter();

                _initialized = true;
                _sampleTime = 0;
            }
            catch (Exception ex)
            {
                Cleanup();
                throw new InvalidOperationException("Failed to initialize Media Foundation encoder", ex);
            }
        }
    }

    private void CreateSinkWriter()
    {
        // Create attributes
        var hr = MFCreateAttributes(out var attributes, 1);
        if (hr != 0) throw new COMException("Failed to create MF attributes", hr);

        try
        {
            // Set hardware encoding hint (Stage 3: will expose as option)
            attributes.SetUINT32(MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1);

            // Create sink writer
            hr = MFCreateSinkWriterFromURL(_outputPath, IntPtr.Zero, attributes, out _sinkWriter);
            if (hr != 0 || _sinkWriter == null)
            {
                throw new COMException("Failed to create MF sink writer. Driver issues or missing codecs.", hr);
            }

            // Configure output media type (container format)
            hr = MFCreateMediaType(out var outputMediaType);
            if (hr != 0) throw new COMException("Failed to create output media type", hr);

            try
            {
                // H.264 in MP4 container
                outputMediaType.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
                outputMediaType.SetGUID(MF_MT_SUBTYPE, MFVideoFormat_H264);
                outputMediaType.SetUINT32(MF_MT_AVG_BITRATE, (uint)_format!.Bitrate);
                outputMediaType.SetUINT32(MF_MT_INTERLACE_MODE, (uint)MFVideoInterlaceMode.Progressive);
                MFSetAttributeSize(outputMediaType, MF_MT_FRAME_SIZE, _format.Width, _format.Height);
                MFSetAttributeRatio(outputMediaType, MF_MT_FRAME_RATE, _format.FPS, 1);
                MFSetAttributeRatio(outputMediaType, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

                hr = _sinkWriter.AddStream(outputMediaType, out _streamIndex);
                if (hr != 0) throw new COMException("Failed to add stream to sink writer", hr);
            }
            finally
            {
                Marshal.ReleaseComObject(outputMediaType);
            }

            // Configure input media type (what we provide)
            hr = MFCreateMediaType(out var inputMediaType);
            if (hr != 0) throw new COMException("Failed to create input media type", hr);

            try
            {
                // RGB32 input (BGRA from WGC)
                inputMediaType.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
                inputMediaType.SetGUID(MF_MT_SUBTYPE, MFVideoFormat_RGB32);
                inputMediaType.SetUINT32(MF_MT_INTERLACE_MODE, (uint)MFVideoInterlaceMode.Progressive);
                MFSetAttributeSize(inputMediaType, MF_MT_FRAME_SIZE, _format.Width, _format.Height);
                MFSetAttributeRatio(inputMediaType, MF_MT_FRAME_RATE, _format.FPS, 1);
                MFSetAttributeRatio(inputMediaType, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

                hr = _sinkWriter.SetInputMediaType(_streamIndex, inputMediaType, IntPtr.Zero);
                if (hr != 0) throw new COMException("Failed to set input media type", hr);
            }
            finally
            {
                Marshal.ReleaseComObject(inputMediaType);
            }

            // Begin writing
            hr = _sinkWriter.BeginWriting();
            if (hr != 0) throw new COMException("Failed to begin writing", hr);
        }
        finally
        {
            if (attributes != null)
                Marshal.ReleaseComObject(attributes);
        }
    }

    public void WriteFrame(FrameData frame)
    {
        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MediaFoundationEncoder));
            if (!_initialized) throw new InvalidOperationException("Encoder not initialized");
            if (_sinkWriter == null) throw new InvalidOperationException("Sink writer not created");

            try
            {
                // Create media buffer from frame data
                var hr = MFCreateMemoryBuffer(frame.Stride * frame.Height, out var buffer);
                if (hr != 0) throw new COMException("Failed to create media buffer", hr);

                try
                {
                    // Lock buffer and copy data
                    hr = buffer.Lock(out var bufferPtr, out _, out _);
                    if (hr != 0) throw new COMException("Failed to lock buffer", hr);

                    try
                    {
                        // Copy frame data to buffer
                        unsafe
                        {
                            Buffer.MemoryCopy(
                                frame.DataPtr.ToPointer(),
                                bufferPtr.ToPointer(),
                                frame.Stride * frame.Height,
                                frame.Stride * frame.Height);
                        }

                        hr = buffer.SetCurrentLength(frame.Stride * frame.Height);
                        if (hr != 0) throw new COMException("Failed to set buffer length", hr);
                    }
                    finally
                    {
                        buffer.Unlock();
                    }

                    // Create sample
                    hr = MFCreateSample(out var sample);
                    if (hr != 0) throw new COMException("Failed to create sample", hr);

                    try
                    {
                        hr = sample.AddBuffer(buffer);
                        if (hr != 0) throw new COMException("Failed to add buffer to sample", hr);

                        // Set sample time and duration
                        hr = sample.SetSampleTime(_sampleTime);
                        if (hr != 0) throw new COMException("Failed to set sample time", hr);

                        long duration = 10000000 / _format!.FPS; // 100ns units
                        hr = sample.SetSampleDuration(duration);
                        if (hr != 0) throw new COMException("Failed to set sample duration", hr);

                        // Write sample
                        hr = _sinkWriter.WriteSample(_streamIndex, sample);
                        if (hr != 0) throw new COMException("Failed to write sample", hr);

                        _sampleTime += duration;
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(sample);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(buffer);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to write frame to encoder", ex);
            }
        }
    }

    public void Finalize()
    {
        lock (_lock)
        {
            if (!_initialized || _sinkWriter == null) return;

            try
            {
                var hr = _sinkWriter.Finalize();
                if (hr != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Sink writer finalize returned error: {hr}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finalizing encoder: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }
    }

    private void Cleanup()
    {
        if (_sinkWriter != null)
        {
            try
            {
                Marshal.ReleaseComObject(_sinkWriter);
            }
            catch { }
            _sinkWriter = null;
        }

        if (_initialized)
        {
            try
            {
                MFShutdown();
            }
            catch { }
            _initialized = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _disposed = true;
            Finalize();
        }

        GC.SuppressFinalize(this);
    }

    #region Media Foundation P/Invoke

    private const uint MF_VERSION = 0x00020070; // Windows 10
    private const uint MFSTARTUP_FULL = 0;

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFStartup(uint version, uint flags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFShutdown();

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateAttributes(out IMFAttributes attributes, uint initialSize);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateMediaType(out IMFMediaType mediaType);

    [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int MFCreateSinkWriterFromURL(
        [MarshalAs(UnmanagedType.LPWStr)] string outputUrl,
        IntPtr byteStream,
        IMFAttributes? attributes,
        out IMFSinkWriter? sinkWriter);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateMemoryBuffer(int maxLength, out IMFMediaBuffer buffer);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateSample(out IMFSample sample);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFSetAttributeSize(IMFMediaType attributes, Guid key, int width, int height);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFSetAttributeRatio(IMFMediaType attributes, Guid key, int numerator, int denominator);

    // Media Foundation GUIDs
    private static readonly Guid MF_MT_MAJOR_TYPE = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    private static readonly Guid MF_MT_SUBTYPE = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    private static readonly Guid MF_MT_AVG_BITRATE = new("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
    private static readonly Guid MF_MT_INTERLACE_MODE = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    private static readonly Guid MF_MT_FRAME_SIZE = new("1652c33d-d6b2-4012-b834-72030849a37d");
    private static readonly Guid MF_MT_FRAME_RATE = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    private static readonly Guid MF_MT_PIXEL_ASPECT_RATIO = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
    private static readonly Guid MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS = new("a634a91c-822b-41b9-a494-4de4643612b0");

    private static readonly Guid MFMediaType_Video = new("73646976-0000-0010-8000-00AA00389B71");
    private static readonly Guid MFVideoFormat_H264 = new("34363248-0000-0010-8000-00AA00389B71");
    private static readonly Guid MFVideoFormat_RGB32 = new("00000016-0000-0010-8000-00AA00389B71");

    private enum MFVideoInterlaceMode
    {
        Progressive = 2
    }

    // COM Interfaces
    [ComImport, Guid("5bc8a76b-869a-46a3-9b03-fa218a66aebe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFAttributes
    {
        void SetUINT32([In] Guid key, [In] uint value);
        // ... other methods omitted for brevity
    }

    [ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaType : IMFAttributes
    {
        new void SetUINT32([In] Guid key, [In] uint value);
        void SetGUID([In] Guid key, [In] Guid value);
        // ... other methods omitted
    }

    [ComImport, Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSinkWriter
    {
        [PreserveSig]
        int AddStream([In] IMFMediaType mediaType, out int streamIndex);

        [PreserveSig]
        int SetInputMediaType([In] int streamIndex, [In] IMFMediaType mediaType, [In] IntPtr encoderParameters);

        [PreserveSig]
        int BeginWriting();

        [PreserveSig]
        int WriteSample([In] int streamIndex, [In] IMFSample sample);

        [PreserveSig]
        int Finalize();
    }

    [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSample
    {
        [PreserveSig]
        int SetSampleTime([In] long sampleTime);

        [PreserveSig]
        int SetSampleDuration([In] long sampleDuration);

        [PreserveSig]
        int AddBuffer([In] IMFMediaBuffer buffer);
    }

    [ComImport, Guid("045fa593-8799-42b8-bc8d-8968c6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaBuffer
    {
        [PreserveSig]
        int Lock(out IntPtr buffer, out int maxLength, out int currentLength);

        [PreserveSig]
        int Unlock();

        [PreserveSig]
        int SetCurrentLength([In] int currentLength);
    }

    #endregion
}
