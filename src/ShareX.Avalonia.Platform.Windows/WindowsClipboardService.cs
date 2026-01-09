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

using XerahS.Platform.Abstractions;
using SkiaSharp;

namespace XerahS.Platform.Windows
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
                // Optimized Clipboard set:
                // Create System.Drawing.Bitmap and copy pixels directly instead of encoding/decoding PNG.
                
                int width = image.Width;
                int height = image.Height;

                // Format32bppArgb in System.Drawing corresponds to BGRA8888 in SkiaSharp (on Windows/LittleEndian)
                using (var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    var bounds = new System.Drawing.Rectangle(0, 0, width, height);
                    var bmpData = bmp.LockBits(bounds, System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);

                    try
                    {
                        // Define the expected format in the destination (System.Drawing.Bitmap)
                        // This ensures that if the source SKBitmap is different, ReadPixels will convert it.
                        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        
                        // Copy pixels directly into the locked bitmap memory
                        var pixmap = image.PeekPixels();
                        if (pixmap != null)
                        {
                            if (!pixmap.ReadPixels(info, bmpData.Scan0, bmpData.Stride, 0, 0))
                            {
                                throw new Exception("Failed to read pixels from SKBitmap into System.Drawing.Bitmap buffer");
                            }
                        }
                        else
                        {
                           // Force pixel allocation if needed (unlikely for valid image)
                           image.GetPixels();
                           if (!image.PeekPixels()?.ReadPixels(info, bmpData.Scan0, bmpData.Stride, 0, 0) ?? false)
                           {
                                throw new Exception("Failed to read pixels from SKBitmap into System.Drawing.Bitmap buffer (fallback)");
                           }
                        }
                    }
                    finally
                    {
                        bmp.UnlockBits(bmpData);
                    }

                    RunInStaThread(() => Clipboard.SetImage(bmp));
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
