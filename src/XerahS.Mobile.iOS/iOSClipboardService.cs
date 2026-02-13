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

using SkiaSharp;
using UIKit;
using XerahS.Platform.Abstractions;

namespace XerahS.Mobile.iOS;

public class iOSClipboardService : IClipboardService
{
    public void Clear() => UIPasteboard.General.String = "";
    public bool ContainsText() => !string.IsNullOrEmpty(UIPasteboard.General.String);
    public bool ContainsImage() => false;
    public bool ContainsFileDropList() => false;

    public string? GetText() => UIPasteboard.General.String;
    public void SetText(string text) => UIPasteboard.General.String = text;

    public SKBitmap? GetImage() => null;
    public void SetImage(SKBitmap image) { }

    public string[]? GetFileDropList() => null;
    public void SetFileDropList(string[] files) { }

    public object? GetData(string format) => null;
    public void SetData(string format, object data) { }
    public bool ContainsData(string format) => false;

    public Task<string?> GetTextAsync() => Task.FromResult(GetText());
    public Task SetTextAsync(string text)
    {
        SetText(text);
        return Task.CompletedTask;
    }
}
