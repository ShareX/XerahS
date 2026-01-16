
using System;
using System.Runtime.InteropServices;

namespace XerahS.Platform.Windows.Recording
{
    [ComImport]
    [Guid("905a0fef-bc53-11df-8c49-001e4fc686da")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IBufferByteAccess
    {
        [PreserveSig]
        int Buffer(out IntPtr value); // Correct signature is Buffer(byte** value) but IntPtr is easier
    }
}
