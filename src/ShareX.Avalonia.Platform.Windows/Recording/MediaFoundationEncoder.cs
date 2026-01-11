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

using Vortice.DXGI;
using XerahS.ScreenCapture.ScreenRecording;
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
    private IntPtr _sinkWriter = IntPtr.Zero;
    private int _streamIndex;
    private long _sampleTime;
    private VideoFormat? _format;
    private string? _outputPath;
    private bool _initialized;
    private bool _disposed;
    private bool _finalized;
    private readonly object _lock = new();
    private static readonly bool _applyVerticalFlip = true; // default fix for upside-down output

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
        Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", "CreateSinkWriter() starting...");
        
        // Create attributes
        Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", "Calling MFCreateAttributes...");
        var hr = MFCreateAttributes(out var attributes, 1);
        Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", $"MFCreateAttributes returned HRESULT: 0x{hr:X8}");
        if (hr != 0) throw new COMException("Failed to create MF attributes", hr);

        try
        {
            // Set hardware encoding hint (Stage 3: will expose as option)
            Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", "Calling attributes.SetUINT32(MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS)...");
            var key = MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS;
            ComFunctions.SetUINT32(attributes, key, 1);
            Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", "✓ SetUINT32 succeeded");

            // Create sink writer
            Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", $"Calling MFCreateSinkWriterFromURL, outputPath={_outputPath}...");
            hr = MFCreateSinkWriterFromURL(_outputPath, IntPtr.Zero, attributes, out _sinkWriter);
            Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", $"MFCreateSinkWriterFromURL returned HRESULT: 0x{hr:X8}");
            if (hr != 0 || _sinkWriter == IntPtr.Zero)
            {
                throw new COMException("Failed to create MF sink writer. Driver issues or missing codecs.", hr);
            }

            // Configure output media type (container format)
            hr = MFCreateMediaType(out var outputMediaType);
            if (hr != 0) throw new COMException("Failed to create output media type", hr);

            try
            {
                // H.264 in MP4 container
                SetGuid(outputMediaType, MF_MT_MAJOR_TYPE, MFMediaType_Video);
                SetGuid(outputMediaType, MF_MT_SUBTYPE, MFVideoFormat_H264);
                SetUInt32(outputMediaType, MF_MT_AVG_BITRATE, (uint)_format!.Bitrate);
                SetUInt32(outputMediaType, MF_MT_INTERLACE_MODE, (uint)MFVideoInterlaceMode.Progressive);
                MFSetAttributeSize(outputMediaType, MF_MT_FRAME_SIZE, _format.Width, _format.Height);
                MFSetAttributeRatio(outputMediaType, MF_MT_FRAME_RATE, _format.FPS, 1);
                MFSetAttributeRatio(outputMediaType, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

                hr = ComFunctions.AddStream(_sinkWriter, outputMediaType, out _streamIndex);
                if (hr != 0) throw new COMException("Failed to add stream to sink writer", hr);
            }
            finally
            {
                ComFunctions.Release(outputMediaType);
            }

            // Configure input media type (what we provide)
            hr = MFCreateMediaType(out var inputMediaType);
            if (hr != 0) throw new COMException("Failed to create input media type", hr);

            try
            {
                // RGB32 input (BGRA from WGC)
                SetGuid(inputMediaType, MF_MT_MAJOR_TYPE, MFMediaType_Video);
                SetGuid(inputMediaType, MF_MT_SUBTYPE, MFVideoFormat_RGB32);
                SetUInt32(inputMediaType, MF_MT_INTERLACE_MODE, (uint)MFVideoInterlaceMode.Progressive);
                MFSetAttributeSize(inputMediaType, MF_MT_FRAME_SIZE, _format.Width, _format.Height);
                MFSetAttributeRatio(inputMediaType, MF_MT_FRAME_RATE, _format.FPS, 1);
                MFSetAttributeRatio(inputMediaType, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

                hr = ComFunctions.SetInputMediaType(_sinkWriter, _streamIndex, inputMediaType, IntPtr.Zero);
                if (hr != 0) throw new COMException("Failed to set input media type", hr);
            }
            finally
            {
                ComFunctions.Release(inputMediaType);
            }

            // Begin writing
            hr = ComFunctions.BeginWriting(_sinkWriter);
            if (hr != 0) throw new COMException("Failed to begin writing", hr);
        }
        finally
        {
            if (attributes != IntPtr.Zero)
                ComFunctions.Release(attributes);
        }
    }

    private void SetGuid(IntPtr ptr, Guid key, Guid value) => ComFunctions.SetGUID(ptr, key, value);
    private void SetUInt32(IntPtr ptr, Guid key, uint value) => ComFunctions.SetUINT32(ptr, key, value);

    private int _frameCount = 0;

    public void WriteFrame(FrameData frame)
    {
        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MediaFoundationEncoder));
            if (!_initialized) throw new InvalidOperationException("Encoder not initialized");
            if (_sinkWriter == IntPtr.Zero) throw new InvalidOperationException("Sink writer not created");

            try
            {
                if (_frameCount == 0 || _frameCount % 30 == 0)
                {
                    Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", $"WriteFrame[{_frameCount}] calling. Stride={frame.Stride}, Height={frame.Height}, SampleTime={_sampleTime}");
                    System.Console.WriteLine($"MF_ENCODER: WriteFrame[{_frameCount}] calling.");
                }

                // Create media buffer from frame data
                var hr = MFCreateMemoryBuffer(frame.Stride * frame.Height, out var buffer);
                if (hr != 0) throw new COMException("Failed to create media buffer", hr);

                try
                {
                    // Lock buffer and copy data
                    hr = ComFunctions.Lock(buffer, out var bufferPtr, out var maxLen, out var curLen);
                    if (hr != 0) throw new COMException("Failed to lock buffer", hr);

            try
            {
                CopyFrame(frame, bufferPtr);
            }
            finally
            {
                        ComFunctions.Unlock(buffer);
                    }

                    // Set length
                    hr = ComFunctions.SetCurrentLength(buffer, frame.Stride * frame.Height);
                    if (hr != 0) throw new COMException("Failed to set buffer length", hr);

                    // Create sample
                    hr = MFCreateSample(out var sample);
                    if (hr != 0) throw new COMException("Failed to create sample", hr);

                    try
                    {
                        hr = ComFunctions.AddBuffer(sample, buffer);
                        if (hr != 0) throw new COMException("Failed to add buffer to sample", hr);

                        // Set sample time and duration
                        hr = ComFunctions.SetSampleTime(sample, _sampleTime);
                        if (hr != 0) throw new COMException("Failed to set sample time", hr);

                        long duration = 10000000 / _format!.FPS; // 100ns units
                        hr = ComFunctions.SetSampleDuration(sample, duration);
                        if (hr != 0) throw new COMException("Failed to set sample duration", hr);

                        // Write sample
                        hr = ComFunctions.WriteSample(_sinkWriter, _streamIndex, sample);
                        if (hr != 0) throw new COMException("Failed to write sample", hr);

                        _sampleTime += duration;
                        _frameCount++;
                    }
                    finally
                    {
                        ComFunctions.Release(sample);
                    }
                }
                finally
                {
                    ComFunctions.Release(buffer);
                }
            }
            catch (Exception ex)
            {
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", $"WriteFrame FAILED: {ex.Message}");
                throw new InvalidOperationException("Failed to write frame to encoder", ex);
            }
        }
    }

    public void Finalize()
    {
        lock (_lock)
        {
            if (_finalized)
            {
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", "Finalize skipped (already finalized)");
                return;
            }

            _finalized = true;
            Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", $"Finalize called. Total frames: {_frameCount}");

            if (!_initialized || _sinkWriter == IntPtr.Zero)
            {
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", "Finalize skipped: encoder not initialized or sink writer missing");
                return;
            }

            try
            {
                var hr = ComFunctions.Finalize(_sinkWriter);
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", $"Finalize returned HRESULT: 0x{hr:X8}");
                if (hr != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Sink writer finalize returned error: {hr}");
                }

                if (!string.IsNullOrEmpty(_outputPath))
                {
                    try
                    {
                        var fileInfo = new System.IO.FileInfo(_outputPath);
                        Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", $"Finalize file state: exists={fileInfo.Exists}, size={fileInfo.Length} bytes");
                    }
                    catch (Exception sizeEx)
                    {
                        Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", $"Finalize size check failed: {sizeEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Helpers.TroubleshootingHelper.Log("ScreenRecorder", "MF_ENCODER", $"Finalize EXCEPTION: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error finalizing encoder: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }
    }

    private unsafe void CopyFrame(FrameData frame, IntPtr bufferPtr)
    {
        if (_applyVerticalFlip)
        {
            // [2026-01-10T14:37:00+08:00] Apply vertical flip only; prior 180° rotation fixed upside-down but left horizontal mirror.
            byte* srcBase = (byte*)frame.DataPtr.ToPointer();
            byte* dstBase = (byte*)bufferPtr.ToPointer();
            int width = frame.Width;
            int height = frame.Height;
            int stride = frame.Stride;

            for (int y = 0; y < height; y++)
            {
                byte* srcRow = srcBase + (height - 1 - y) * stride;
                byte* dstRow = dstBase + y * stride;
                Buffer.MemoryCopy(srcRow, dstRow, stride, stride);
            }
        }
        else
        {
            Buffer.MemoryCopy(frame.DataPtr.ToPointer(), bufferPtr.ToPointer(), frame.Stride * frame.Height, frame.Stride * frame.Height);
        }
    }

    private void Cleanup()
    {
        if (_sinkWriter != IntPtr.Zero)
        {
            try
            {
                ComFunctions.Release(_sinkWriter);
            }
            catch { }
            _sinkWriter = IntPtr.Zero;
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

        bool needsFinalize;
        lock (_lock)
        {
            _disposed = true;
            needsFinalize = !_finalized;
        }

        // [2026-01-10T14:02:37+08:00] Guard against duplicate sink finalization when Dispose follows StopRecordingAsync; outcome (2026-01-10T14:09:06+08:00) shows single finalize entry with valid mp4 size. [2026-01-10T14:37:00+08:00] Orientation fix adjusted to vertical-only flip to remove residual mirror.
        if (needsFinalize)
        {
            Finalize();
        }

        GC.SuppressFinalize(this);
    }

    #region Media Foundation P/Invoke and VTable Helpers

    private const uint MF_VERSION = 0x00020070; // Windows 10
    private const uint MFSTARTUP_FULL = 0;

    [DllImport("mfplat.dll", EntryPoint = "MFStartup", ExactSpelling = true)]
    private static extern int MFStartup(uint version, uint flags);

    [DllImport("mfplat.dll", EntryPoint = "MFShutdown", ExactSpelling = true)]
    private static extern int MFShutdown();

    [DllImport("mfplat.dll", EntryPoint = "MFCreateAttributes", ExactSpelling = true)]
    private static extern int MFCreateAttributes(out IntPtr attributes, uint initialSize);

    [DllImport("mfplat.dll", EntryPoint = "MFCreateMediaType", ExactSpelling = true)]
    private static extern int MFCreateMediaType(out IntPtr mediaType);

    [DllImport("mfreadwrite.dll", EntryPoint = "MFCreateSinkWriterFromURL", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int MFCreateSinkWriterFromURL(
        [MarshalAs(UnmanagedType.LPWStr)] string outputUrl,
        IntPtr byteStream,
        IntPtr attributes,
        out IntPtr sinkWriter);

    [DllImport("mfplat.dll", EntryPoint = "MFCreateMemoryBuffer", ExactSpelling = true)]
    private static extern int MFCreateMemoryBuffer(int maxLength, out IntPtr buffer);

    [DllImport("mfplat.dll", EntryPoint = "MFCreateSample", ExactSpelling = true)]
    private static extern int MFCreateSample(out IntPtr sample);

    private static void MFSetAttributeSize(IntPtr attributes, Guid key, int width, int height)
    {
        ulong value = ((ulong)(uint)width << 32) | (uint)height;
        ComFunctions.SetUINT64(attributes, key, value);
    }

    private static void MFSetAttributeRatio(IntPtr attributes, Guid key, int numerator, int denominator)
    {
        ulong value = ((ulong)(uint)numerator << 32) | (uint)denominator;
        ComFunctions.SetUINT64(attributes, key, value);
    }

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

    // COM VTable Helpers
    private static class ComFunctions
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int ReleaseDelegate(IntPtr thisPtr);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int SetUINT32Delegate(IntPtr thisPtr, [In] ref Guid key, uint value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int SetUINT64Delegate(IntPtr thisPtr, [In] ref Guid key, ulong value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int SetGUIDDelegate(IntPtr thisPtr, [In] ref Guid key, [In] ref Guid value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int AddStreamDelegate(IntPtr thisPtr, IntPtr mediaType, out int streamIndex);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int SetInputMediaTypeDelegate(IntPtr thisPtr, int streamIndex, IntPtr mediaType, IntPtr encodingParameters);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int BeginWritingDelegate(IntPtr thisPtr);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int WriteSampleDelegate(IntPtr thisPtr, int streamIndex, IntPtr sample);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int FinalizeDelegate(IntPtr thisPtr);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int LockDelegate(IntPtr thisPtr, out IntPtr buffer, out int maxLength, out int currentLength);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int UnlockDelegate(IntPtr thisPtr);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int SetCurrentLengthDelegate(IntPtr thisPtr, int currentLength);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int AddBufferDelegate(IntPtr thisPtr, IntPtr buffer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int SetSampleTimeDelegate(IntPtr thisPtr, long sampleTime);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int SetSampleDurationDelegate(IntPtr thisPtr, long sampleDuration);


        public static int Release(IntPtr ptr) => Call<ReleaseDelegate>(ptr, 2)(ptr);

        // IMFAttributes (IUnknown + Methods)
        // Correct Indices (Vortice/SharpDX standard):
        // SetUINT32 = 21
        // SetUINT64 = 22
        // SetGUID = 24
        
        public static int SetUINT32(IntPtr ptr, Guid key, uint value) => Call<SetUINT32Delegate>(ptr, 21)(ptr, ref key, value);
        public static int SetUINT64(IntPtr ptr, Guid key, ulong value) => Call<SetUINT64Delegate>(ptr, 22)(ptr, ref key, value);
        public static int SetGUID(IntPtr ptr, Guid key, Guid value) => Call<SetGUIDDelegate>(ptr, 24)(ptr, ref key, ref value);

        // IMFSinkWriter (IUnknown + 8 methods)
        // 3: AddStream
        // 4: SetInputMediaType
        // 5: BeginWriting
        // 6: WriteSample
        // 11: Finalize
        public static int AddStream(IntPtr ptr, IntPtr mediaType, out int streamIndex) => Call<AddStreamDelegate>(ptr, 3)(ptr, mediaType, out streamIndex);
        public static int SetInputMediaType(IntPtr ptr, int streamIndex, IntPtr mediaType, IntPtr encodingParameters) => Call<SetInputMediaTypeDelegate>(ptr, 4)(ptr, streamIndex, mediaType, encodingParameters);
        public static int BeginWriting(IntPtr ptr) => Call<BeginWritingDelegate>(ptr, 5)(ptr);
        public static int WriteSample(IntPtr ptr, int streamIndex, IntPtr sample) => Call<WriteSampleDelegate>(ptr, 6)(ptr, streamIndex, sample);
        public static int Finalize(IntPtr ptr) => Call<FinalizeDelegate>(ptr, 11)(ptr);

        // IMFMediaBuffer
        // 3: Lock
        // 4: Unlock
        // 5: SetCurrentLength
        public static int Lock(IntPtr ptr, out IntPtr buffer, out int maxLength, out int currentLength) => Call<LockDelegate>(ptr, 3)(ptr, out buffer, out maxLength, out currentLength);
        public static int Unlock(IntPtr ptr) => Call<UnlockDelegate>(ptr, 4)(ptr);
        public static int SetCurrentLength(IntPtr ptr, int currentLength) => Call<SetCurrentLengthDelegate>(ptr, 6)(ptr, currentLength);

        // IMFSample (Inherits IMFAttributes)
        // Correct start for IMFSample is 33.
        // 33: GetSampleFlags
        // 34: SetSampleFlags
        // 35: GetSampleTime
        // 36: SetSampleTime   <-- 36
        // 37: GetSampleDuration
        // 38: SetSampleDuration <-- 38
        // 39: GetBufferCount
        // 40: GetBufferByIndex
        // 41: ConvertToContiguousBuffer
        // 42: AddBuffer       <-- 42
        
        public static int SetSampleTime(IntPtr ptr, long sampleTime) => Call<SetSampleTimeDelegate>(ptr, 36)(ptr, sampleTime);
        public static int SetSampleDuration(IntPtr ptr, long sampleDuration) => Call<SetSampleDurationDelegate>(ptr, 38)(ptr, sampleDuration);
        public static int AddBuffer(IntPtr ptr, IntPtr buffer) => Call<AddBufferDelegate>(ptr, 42)(ptr, buffer);

        private static T Call<T>(IntPtr ptr, int index) where T : Delegate
        {
            var vtable = Marshal.ReadIntPtr(ptr);
            var methodPtr = Marshal.ReadIntPtr(vtable, index * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer<T>(methodPtr);
        }
    }

    #endregion
}
