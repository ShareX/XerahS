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

using Android.Content;
using Android.OS;
using SkiaSharp;
using XerahS.Platform.Abstractions;

namespace Ava.Platforms.Android;

public class AndroidClipboardService : IClipboardService
{
    private readonly Context _context;

    public AndroidClipboardService(Context context)
    {
        _context = context;
    }

    private ClipboardManager? GetClipboardManager()
        => _context.GetSystemService(Context.ClipboardService) as ClipboardManager;

    public void Clear()
    {
        var cm = GetClipboardManager();
        if (cm == null) return;
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
            cm.ClearPrimaryClip();
        else
            cm.PrimaryClip = ClipData.NewPlainText("", "");
    }

    public bool ContainsText() => GetClipboardManager()?.HasPrimaryClip == true;
    public bool ContainsImage() => false;
    public bool ContainsFileDropList() => false;
    public string? GetText() => GetClipboardManager()?.PrimaryClip?.GetItemAt(0)?.Text;

    public void SetText(string text)
    {
#pragma warning disable CA1416
        if (Looper.MainLooper?.IsCurrentThread == true)
            SetTextOnMainThread(text);
        else
        {
            using var handler = new Handler(Looper.MainLooper!);
            handler.Post(() => SetTextOnMainThread(text));
        }
#pragma warning restore CA1416
    }

    private void SetTextOnMainThread(string text)
    {
        var clip = ClipData.NewPlainText("XerahS", text);
        var cm = GetClipboardManager();
        if (cm != null)
            cm.PrimaryClip = clip;
    }

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
#pragma warning disable CA1416
        if (Looper.MainLooper?.IsCurrentThread == true)
            SetTextOnMainThread(text);
        else
        {
            using var handler = new Handler(Looper.MainLooper!);
            handler.Post(() => SetTextOnMainThread(text));
        }
#pragma warning restore CA1416
        return Task.CompletedTask;
    }
}
