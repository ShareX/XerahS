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

namespace XerahS.Platform.Windows.Capture;

internal static class DxgiOutputDuplicationPolicy
{
    /// <summary>
    /// DuplicateOutput1 can access-violate (0xC0000005) inside the native call instead of
    /// returning a DXGI error. .NET cannot catch that, so the process dies without a log line.
    /// It was first seen on ARM64 GPU drivers, then on an x64 multi-monitor SDR desktop where
    /// every call returned E_INVALIDARG until one of them crashed.
    /// <para>
    /// Only call it when it can help: an HDR (PQ/BT.2020) output on an architecture where it
    /// has not been ruled out, and only until it has failed once in this process. SDR outputs
    /// get the same BGRA8 surface from DuplicateOutput.
    /// </para>
    /// </summary>
    internal static bool ShouldUseDuplicateOutput1(Architecture architecture, bool isHdrOutput, bool failedThisSession) =>
        architecture is Architecture.X64 or Architecture.X86 &&
        isHdrOutput &&
        !failedThisSession;
}
