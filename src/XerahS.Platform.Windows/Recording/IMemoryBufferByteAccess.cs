
using System;
using System.Runtime.InteropServices;

namespace XerahS.Platform.Windows.Recording
{
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMemoryBufferByteAccess
    {
        [PreserveSig]
        int GetBuffer(out IntPtr value, out uint capacity);
    }
}
