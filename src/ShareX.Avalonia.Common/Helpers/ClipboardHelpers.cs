#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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

#if NET6_0_OR_GREATER
#endif
using XerahS.Platform.Abstractions;
using SkiaSharp;

namespace XerahS.Common.Helpers
{
    public static class ClipboardHelpers
    {
        private static IClipboardService? Clipboard => PlatformServices.IsInitialized ? PlatformServices.Clipboard : null;

        public static async Task SetTextAsync(string text)
        {
            if (Clipboard != null)
            {
                await Clipboard.SetTextAsync(text);
            }
        }

        public static async Task<string?> GetTextAsync()
        {
            if (Clipboard != null)
            {
                return await Clipboard.GetTextAsync();
            }
            return null;
        }

        // Keep sync wrappers for now if needed, or remove them if we want to force async.
        // User said "Don't need to match legacy", so we prefer Async.
        // We will remove synchronous text methods to encourage correct usage.

        public static bool ContainsText()
        {
            return Clipboard != null && Clipboard.ContainsText();
        }

        public static bool ContainsImage()
        {
            return Clipboard != null && Clipboard.ContainsImage();
        }

        public static bool ContainsFileDropList()
        {
            return Clipboard != null && Clipboard.ContainsFileDropList();
        }

        public static string[] GetFileDropList()
        {
            // IClipboardService definition viewed earlier didn't show GetFileDropListAsync, only GetTextAsync.
            // We will check IClipboardService again to be sure, but for now use sync if that's all that is exposed.
            return Clipboard?.GetFileDropList();
        }

        public static void SetFileDropList(string[] files)
        {
            Clipboard?.SetFileDropList(files);
        }

        public static SKBitmap? GetImage()
        {
            return Clipboard?.GetImage();
        }

        public static void SetImage(SKBitmap image)
        {
            Clipboard?.SetImage(image);
        }

        public static void Clear()
        {
            Clipboard?.Clear();
        }
    }
}
