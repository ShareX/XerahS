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
