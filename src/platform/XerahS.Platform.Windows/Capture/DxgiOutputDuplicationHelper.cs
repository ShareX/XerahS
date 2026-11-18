#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using XerahS.Common;

namespace XerahS.Platform.Windows.Capture;

internal static class DxgiOutputDuplicationHelper
{
    /// <summary>fp16 scRGB is the only format that carries HDR highlights.</summary>
    private static readonly Format[] HdrFormats = [Format.R16G16B16A16_Float];

    /// <summary>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 as a raw context handle.</summary>
    private static nint PerMonitorAwareV2 => -4;

    /// <summary>
    /// A rejected fp16 request tends to repeat, and a repeat can access-violate inside the native
    /// call, so a failure disables the fp16 path for the rest of the process.
    /// </summary>
    private static volatile bool _fp16DuplicationFailed;

    /// <summary>
    /// Creates the output duplication, requesting fp16 scRGB on an HDR output so the HDR range
    /// survives into the capture. Falls back to the plain 8-bit duplication.
    /// <para>
    /// The fp16 overload only answers on a DPI aware thread: an unaware one gets
    /// DXGI_ERROR_UNSUPPORTED for every format, including formats DuplicateOutput accepts, and a
    /// repeated request can crash inside the driver. Captures run on thread-pool threads, so the
    /// context is forced here rather than relying on what the thread inherited; measured on the
    /// reporter's machine as 3/3 successes aware versus 3/3 failures unaware.
    /// </para>
    /// </summary>
    public static IDXGIOutputDuplication Create(IDXGIOutput output, ID3D11Device device)
    {
        nint previousContext = SetThreadDpiAwarenessContext(PerMonitorAwareV2);

        try
        {
            if (HdrToneMapContext.IsHdrOutput(output) && IsDpiAware() && !_fp16DuplicationFailed)
            {
                try
                {
                    using var output5 = output.QueryInterface<IDXGIOutput5>();
                    IDXGIOutputDuplication duplication = output5.DuplicateOutput1(device, HdrFormats);

                    DebugHelper.WriteLine(
                        $"DxgiOutputDuplicationHelper: fp16 duplication created, format={duplication.Description.ModeDescription.Format}");
                    return duplication;
                }
                catch (Exception ex)
                {
                    _fp16DuplicationFailed = true;
                    DebugHelper.WriteLine(
                        $"DxgiOutputDuplicationHelper: DuplicateOutput1 failed, using DuplicateOutput for the rest of this session. {ex.Message}");
                }
            }

            using var output1 = output.QueryInterface<IDXGIOutput1>();
            IDXGIOutputDuplication fallback = output1.DuplicateOutput(device);

            Format format = fallback.Description.ModeDescription.Format;
            DebugHelper.WriteLine(
                $"DxgiOutputDuplicationHelper: duplication created, format={format}" +
                (DxgiHdrToneMapper.IsHdrFormat(format) ? " (HDR)" : string.Empty));

            return fallback;
        }
        finally
        {
            if (previousContext != 0)
            {
                SetThreadDpiAwarenessContext(previousContext);
            }
        }
    }

    /// <summary>DPI_AWARENESS_UNAWARE is 0; anything else can duplicate an HDR desktop.</summary>
    private static bool IsDpiAware()
    {
        try
        {
            return GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext()) != 0;
        }
        catch (Exception ex)
        {
            // Older Windows builds do not export these; assume aware and let the call decide.
            DebugHelper.WriteLine($"DxgiOutputDuplicationHelper: DPI awareness query failed, assuming aware. {ex.Message}");
            return true;
        }
    }

    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint dpiAwarenessContext);

    [DllImport("user32.dll")]
    private static extern nint GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    private static extern int GetAwarenessFromDpiAwarenessContext(nint dpiAwarenessContext);
}
