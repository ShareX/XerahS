using System;
using System.Runtime.InteropServices;

namespace XerahS.Platform.MacOS.Native
{
    internal static class Accessibility
    {
        private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const string ApplicationServicesLib = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFDictionaryCreate(
            IntPtr allocator,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] keys,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] values,
            long numValues,
            IntPtr keyCallBacks,
            IntPtr valueCallBacks);

        [DllImport(CoreFoundationLib)]
        private static extern void CFRelease(IntPtr cf);

        [DllImport(ApplicationServicesLib)]
        private static extern bool AXIsProcessTrustedWithOptions(IntPtr options);

        [DllImport("libSystem.dylib")]
        private static extern IntPtr dlopen(string path, int mode);

        [DllImport("libSystem.dylib")]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        private const int RTLD_LAZY = 0x00001;

        /// <summary>
        /// Checks if the process is trusted for accessibility.
        /// </summary>
        /// <param name="prompt">If true, the system will prompt the user to grant permission if not already trusted.</param>
        /// <returns>True if trusted, false otherwise.</returns>
        public static bool IsProcessTrusted(bool prompt)
        {
            if (!prompt)
            {
                // Simple check without options
                return AXIsProcessTrustedWithOptions(IntPtr.Zero);
            }

            // Load symbols
            IntPtr appServices = dlopen(ApplicationServicesLib, RTLD_LAZY);
            IntPtr coreFoundation = dlopen(CoreFoundationLib, RTLD_LAZY);

            if (appServices == IntPtr.Zero || coreFoundation == IntPtr.Zero) return false;

            IntPtr kAXTrustedCheckOptionPromptPtr = dlsym(appServices, "kAXTrustedCheckOptionPrompt");
            IntPtr kCFBooleanTruePtr = dlsym(coreFoundation, "kCFBooleanTrue");
            IntPtr kCFTypeDictionaryKeyCallBacksPtr = dlsym(coreFoundation, "kCFTypeDictionaryKeyCallBacks");
            IntPtr kCFTypeDictionaryValueCallBacksPtr = dlsym(coreFoundation, "kCFTypeDictionaryValueCallBacks");

            if (kAXTrustedCheckOptionPromptPtr == IntPtr.Zero || kCFBooleanTruePtr == IntPtr.Zero) return false;

            // Dereference the pointers to get the actual CF values
            IntPtr key = Marshal.ReadIntPtr(kAXTrustedCheckOptionPromptPtr);
            IntPtr value = Marshal.ReadIntPtr(kCFBooleanTruePtr);

            // Create dictionary
            IntPtr[] keys = new IntPtr[] { key };
            IntPtr[] values = new IntPtr[] { value };

            // Create dictionary with the prompt option
            IntPtr dict = CFDictionaryCreate(
                IntPtr.Zero, 
                keys, 
                values, 
                1, 
                kCFTypeDictionaryKeyCallBacksPtr, 
                kCFTypeDictionaryValueCallBacksPtr);
            
            if (dict == IntPtr.Zero) return false;

            try 
            {
                return AXIsProcessTrustedWithOptions(dict);
            }
            finally
            {
                CFRelease(dict);
            }
        }
    }
}
