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
using XerahS.Platform.Abstractions;
using SkiaSharp;

namespace XerahS.UI.Services
{
    public class ClipboardService : IClipboardService
    {
        public void Clear()
        {
            // Avalonia requires TopLevel access for clipboard
            // This is a stub - full implementation needs window reference
        }

        public bool ContainsText()
        {
            // Avalonia clipboard is async-only, so we return false for sync methods
            // Full implementation would need platform-specific APIs
            return false;
        }

        public bool ContainsImage()
        {
            return false;
        }

        public bool ContainsFileDropList()
        {
            return false;
        }

        public string? GetText()
        {
            // Sync method - would need platform-specific synchronous API
            return null;
        }

        public void SetText(string text)
        {
            // Fire and forget for sync API
            _ = SetTextAsync(text);
        }

        public SKBitmap? GetImage()
        {
            return null;
        }

        public void SetImage(SKBitmap image)
        {
            // TODO: Implement
        }

        public string[]? GetFileDropList()
        {
            return null;
        }

        public void SetFileDropList(string[] files)
        {
            // TODO: Implement
        }

        public object? GetData(string format)
        {
            return null;
        }

        public void SetData(string format, object data)
        {
            // TODO: Implement
        }

        public bool ContainsData(string format)
        {
            return false;
        }

        public async Task<string?> GetTextAsync()
        {
            // TODO: Implement with proper TopLevel/Window reference
            await Task.CompletedTask;
            return null;
        }

        public async Task SetTextAsync(string text)
        {
            // TODO: Implement with proper TopLevel/Window reference
            await Task.CompletedTask;
        }
    }
}
