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

using ShareX.Ava.Platform.Abstractions;
using System;
using System.Drawing;
using System.IO;
using SkiaSharp;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShareX.Ava.Platform.Windows
{
    /// <summary>
    /// Windows implementation of IClipboardService using System.Windows.Forms.Clipboard
    /// </summary>
    public class WindowsClipboardService : IClipboardService
    {
        public void Clear()
        {
            try
            {
                Clipboard.Clear();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear clipboard: {ex.Message}");
            }
        }

        public bool ContainsText()
        {
            try
            {
                return Clipboard.ContainsText();
            }
            catch
            {
                return false;
            }
        }

        public bool ContainsImage()
        {
            try
            {
                return Clipboard.ContainsImage();
            }
            catch
            {
                return false;
            }
        }

        public bool ContainsFileDropList()
        {
            try
            {
                return Clipboard.ContainsFileDropList();
            }
            catch
            {
                return false;
            }
        }

        public string? GetText()
        {
            try
            {
                return Clipboard.GetText();
            }
            catch
            {
                return null;
            }
        }

        public void SetText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            try
            {
                RunInStaThread(() => Clipboard.SetText(text));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set clipboard text: {ex.Message}");
            }
        }

        public SKBitmap? GetImage()
        {
            try
            {
                using (var image = Clipboard.GetImage())
                {
                    if (image == null) return null;
                    
                    using (var ms = new MemoryStream())
                    {
                        image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;
                        return SKBitmap.Decode(ms);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public void SetImage(SKBitmap image)
        {
            if (image == null)
                return;

            try
            {
                // Convert SKBitmap to DIB (Device Independent Bitmap) format for clipboard
                using (var ms = new MemoryStream())
                {
                    // Encode to BMP
                    using (var skImage = SKImage.FromBitmap(image))
                    using (var data = skImage.Encode(SKEncodedImageFormat.Bmp, 100))
                    {
                        data.SaveTo(ms);
                    }
                    
                    ms.Position = 0;

                    // Read BMP header to skip it (clipboard wants DIB, not BMP)
                    byte[] bmpData = ms.ToArray();
                    
                    // BMP file header is 14 bytes, we need to skip it for DIB
                    int dibOffset = 14;
                    int dibSize = bmpData.Length - dibOffset;
                    
                    // Allocate global memory for clipboard
                    IntPtr hGlobal = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)dibSize);
                    if (hGlobal == IntPtr.Zero)
                        throw new InvalidOperationException("Failed to allocate global memory for clipboard");

                    try
                    {
                        // Lock the memory and copy DIB data
                        IntPtr pGlobal = NativeMethods.GlobalLock(hGlobal);
                        if (pGlobal == IntPtr.Zero)
                            throw new InvalidOperationException("Failed to lock global memory");

                        try
                        {
                            System.Runtime.InteropServices.Marshal.Copy(bmpData, dibOffset, pGlobal, dibSize);
                        }
                        finally
                        {
                            NativeMethods.GlobalUnlock(hGlobal);
                        }

                        // Open clipboard with retry logic
                        bool clipboardOpened = false;
                        for (int i = 0; i < 5; i++)
                        {
                            if (NativeMethods.OpenClipboard(IntPtr.Zero))
                            {
                                clipboardOpened = true;
                                break;
                            }
                            System.Threading.Thread.Sleep(100);
                        }

                        if (!clipboardOpened)
                            throw new InvalidOperationException("Failed to open clipboard after 5 attempts");

                        try
                        {
                            // Empty clipboard and set new data
                            NativeMethods.EmptyClipboard();
                            
                            IntPtr result = NativeMethods.SetClipboardData(NativeMethods.CF_DIB, hGlobal);
                            if (result == IntPtr.Zero)
                            {
                                throw new InvalidOperationException("Failed to set clipboard data");
                            }
                            
                            // Success - clipboard now owns the memory, don't free it
                            hGlobal = IntPtr.Zero;
                        }
                        finally
                        {
                            NativeMethods.CloseClipboard();
                        }
                    }
                    finally
                    {
                        // Only free if we still own the memory (SetClipboardData failed)
                        if (hGlobal != IntPtr.Zero)
                        {
                            NativeMethods.GlobalFree(hGlobal);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set clipboard image: {ex.Message}");
                throw;
            }
        }

        // Native methods for direct clipboard access
        private static class NativeMethods
        {
            public const uint GMEM_MOVEABLE = 0x0002;
            public const uint CF_DIB = 8;

            [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

            [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr GlobalLock(IntPtr hMem);

            [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool GlobalUnlock(IntPtr hMem);

            [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr GlobalFree(IntPtr hMem);

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool OpenClipboard(IntPtr hWndNewOwner);

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool CloseClipboard();

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool EmptyClipboard();

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        }

        public string[]? GetFileDropList()
        {
            try
            {
                var files = Clipboard.GetFileDropList();
                if (files != null && files.Count > 0)
                {
                    string[] result = new string[files.Count];
                    files.CopyTo(result, 0);
                    return result;
                }
            }
            catch
            {
                // Ignore
            }

            return null;
        }

        public void SetFileDropList(string[] files)
        {
            if (files == null || files.Length == 0)
                return;

            try
            {
                var fileCollection = new System.Collections.Specialized.StringCollection();
                fileCollection.AddRange(files);
                Clipboard.SetFileDropList(fileCollection);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set clipboard file drop list: {ex.Message}");
            }
        }

        public object? GetData(string format)
        {
            try
            {
                return Clipboard.GetData(format);
            }
            catch
            {
                return null;
            }
        }

        public void SetData(string format, object data)
        {
            if (string.IsNullOrEmpty(format) || data == null)
                return;

            try
            {
                Clipboard.SetData(format, data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set clipboard data: {ex.Message}");
            }
        }

        public bool ContainsData(string format)
        {
            if (string.IsNullOrEmpty(format))
                return false;

            try
            {
                return Clipboard.ContainsData(format);
            }
            catch
            {
                return false;
            }
        }

        public Task<string?> GetTextAsync()
        {
            // Windows Forms Clipboard is synchronous, but we provide async wrapper for consistency
            return Task.FromResult(GetText());
        }

        public Task SetTextAsync(string text)
        {
            return Task.Run(() => SetText(text));
        }

        private static void RunInStaThread(Action action)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                action();
                return;
            }

            Exception? captured = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join();

            if (captured != null)
            {
                throw captured;
            }
        }
    }
}
