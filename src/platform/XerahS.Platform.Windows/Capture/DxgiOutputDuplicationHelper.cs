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
    private static readonly Format[] PreferredFormats =
    [
        Format.R16G16B16A16_Float,
        Format.R10G10B10A2_UNorm,
        Format.B8G8R8A8_UNorm
    ];

    private static volatile bool _duplicateOutput1Failed;

    /// <summary>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 as a raw context handle.</summary>
    private static nint PerMonitorAwareV2 => -4;

    public static IDXGIOutputDuplication Create(IDXGIOutput output, ID3D11Device device)
    {
        // DuplicateOutput1 answers only on a DPI aware thread. On an unaware one it returns
        // DXGI_ERROR_UNSUPPORTED for every format, including formats plain DuplicateOutput accepts,
        // even on an HDR display; a repeated rejected request can also crash inside the driver.
        // Captures run on thread-pool threads, which can carry their own context, so set it for the
        // call and restore whatever the thread had afterwards.
        nint previousContext = SetThreadDpiAwarenessContext(PerMonitorAwareV2);

        try
        {
            if (DxgiOutputDuplicationPolicy.ShouldUseDuplicateOutput1(
                RuntimeInformation.ProcessArchitecture,
                HdrToneMapContext.IsHdrOutput(output),
                _duplicateOutput1Failed))
            {
                try
                {
                    using var output5 = output.QueryInterface<IDXGIOutput5>();
                    return output5.DuplicateOutput1(device, 0, PreferredFormats);
                }
                catch (Exception ex)
                {
                    // A failure here tends to repeat, and a repeat can be an uncatchable AV. Stop trying.
                    _duplicateOutput1Failed = true;
                    DebugHelper.WriteLine(
                        $"DxgiOutputDuplicationHelper: DuplicateOutput1 failed, using DuplicateOutput for the rest of this session. {ex.Message}");
                }
            }

            using var output1 = output.QueryInterface<IDXGIOutput1>();
            return output1.DuplicateOutput(device);
        }
        finally
        {
            if (previousContext != 0)
            {
                SetThreadDpiAwarenessContext(previousContext);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint dpiAwarenessContext);
}
