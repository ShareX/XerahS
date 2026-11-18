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

using Vortice.Direct3D11;
using Vortice.DXGI;
using XerahS.Common;

namespace XerahS.Platform.Windows.Capture;

internal static class DxgiOutputDuplicationHelper
{
    /// <summary>
    /// Creates the output duplication through <see cref="IDXGIOutput1.DuplicateOutput"/>.
    /// <para>
    /// The fp16 overload (<see cref="IDXGIOutput5.DuplicateOutput1"/>) is deliberately not used.
    /// On an HDR desktop Windows composites in scRGB and already hands back
    /// R16G16B16A16_Float frames, so asking for the format buys nothing; on real drivers the call
    /// instead fails with DXGI_ERROR_UNSUPPORTED / E_INVALIDARG, and sometimes access-violates
    /// inside native code. That access violation cannot be caught and terminates the process
    /// (XerahS-20261118.log: 07:32 and 08:10 died in DuplicateOutput1_; the same build captured
    /// fp16 frames through DuplicateOutput right after the 07:35 failure).
    /// </para>
    /// </summary>
    public static IDXGIOutputDuplication Create(IDXGIOutput output, ID3D11Device device)
    {
        using var output1 = output.QueryInterface<IDXGIOutput1>();
        IDXGIOutputDuplication duplication = output1.DuplicateOutput(device);

        Format format = duplication.Description.ModeDescription.Format;
        DebugHelper.WriteLine(
            $"DxgiOutputDuplicationHelper: duplication created, format={format}" +
            (DxgiHdrToneMapper.IsHdrFormat(format) ? " (HDR)" : string.Empty));

        return duplication;
    }
}
