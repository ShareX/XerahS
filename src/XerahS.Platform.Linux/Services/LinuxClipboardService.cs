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
using System.Diagnostics;
using System.Linq;
using System.Text;
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services;

/// <summary>
/// Linux clipboard implementation using common Wayland/X11 utilities.
/// Prefers wl-copy / wl-paste, falls back to xclip when available.
/// </summary>
public sealed class LinuxClipboardService : IClipboardService
{
    private const string WlCopy = "wl-copy";
    private const string WlPaste = "wl-paste";
    private const string Xclip = "xclip";

    public void SetText(string text) => SetTextAsync(text).GetAwaiter().GetResult();

    public string? GetText() => GetTextAsync().GetAwaiter().GetResult();

    public async Task SetTextAsync(string text)
    {
        if (await TryPipeAsync(WlCopy, string.Empty, Encoding.UTF8.GetBytes(text)))
            return;

        await TryPipeAsync(Xclip, "-selection clipboard", Encoding.UTF8.GetBytes(text));
    }

    public async Task<string?> GetTextAsync()
    {
        var result = await ReadTextAsync(WlPaste, string.Empty);
        if (!string.IsNullOrWhiteSpace(result))
            return result;

        result = await ReadTextAsync(Xclip, "-selection clipboard -o");
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    public void SetImage(SKBitmap image)
    {
        using var stream = new MemoryStream();
        image.Encode(stream, SKEncodedImageFormat.Png, 100);
        stream.Position = 0;
        SetImageAsync(stream.ToArray()).GetAwaiter().GetResult();
    }

    public SKBitmap? GetImage() => GetImageAsync().GetAwaiter().GetResult();

    public async Task SetImageAsync(byte[] pngBytes)
    {
        if (await TryPipeAsync(WlCopy, "--type image/png", pngBytes))
            return;

        await TryPipeAsync(Xclip, "-selection clipboard -t image/png -i", pngBytes);
    }

    public async Task<SKBitmap?> GetImageAsync()
    {
        var bytes = await ReadBytesAsync(WlPaste, "--type image/png");
        if (bytes == null || bytes.Length == 0)
            bytes = await ReadBytesAsync(Xclip, "-selection clipboard -t image/png -o");

        if (bytes == null || bytes.Length == 0)
            return null;

        return SKBitmap.Decode(bytes);
    }

    public void SetFileDropList(string[] files)
    {
        // GNOME/KDE expect text/uri-list; best-effort implementation.
        var payload = string.Join("\n", files.Select(f => $"file://{f}"));
        SetData("text/uri-list", payload);
    }

    public string[]? GetFileDropList()
    {
        var data = GetData("text/uri-list") as string;
        if (string.IsNullOrWhiteSpace(data))
            return Array.Empty<string>();

        return data.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(uri => uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ? uri[7..] : uri)
            .ToArray();
    }

    public void Clear()
    {
        // Clearing is non-trivial across backends; overwrite with empty text.
        SetText(string.Empty);
    }

    public bool ContainsText() => !string.IsNullOrEmpty(GetText());

    public bool ContainsImage() => GetImage() != null;

    public bool ContainsFileDropList()
    {
        var list = GetFileDropList();
        return list != null && list.Length > 0;
    }

    public object? GetData(string format)
    {
        if (string.Equals(format, "text/uri-list", StringComparison.OrdinalIgnoreCase))
        {
            // Try wl-paste first
            var text = ReadTextAsync(WlPaste, "--type text/uri-list").GetAwaiter().GetResult();
            if (!string.IsNullOrWhiteSpace(text))
                return text;

            text = ReadTextAsync(Xclip, "-selection clipboard -t text/uri-list -o").GetAwaiter().GetResult();
            return text;
        }

        if (string.Equals(format, "text/plain", StringComparison.OrdinalIgnoreCase))
            return GetText();

        return null;
    }

    public void SetData(string format, object data)
    {
        if (data is not string textData)
            return;

        var args = string.Equals(format, "text/uri-list", StringComparison.OrdinalIgnoreCase)
            ? "--type text/uri-list"
            : string.Empty;

        if (TryPipeAsync(WlCopy, args, Encoding.UTF8.GetBytes(textData)).GetAwaiter().GetResult())
            return;

        TryPipeAsync(Xclip, $"-selection clipboard{(string.IsNullOrEmpty(args) ? string.Empty : " -t " + format)}", Encoding.UTF8.GetBytes(textData)).GetAwaiter().GetResult();
    }

    public bool ContainsData(string format)
    {
        if (string.Equals(format, "text/uri-list", StringComparison.OrdinalIgnoreCase))
            return ContainsFileDropList();

        if (string.Equals(format, "text/plain", StringComparison.OrdinalIgnoreCase))
            return ContainsText();

        return false;
    }

    private static async Task<bool> TryPipeAsync(string tool, string args, byte[] data)
    {
        try
        {
            using var process = CreateProcess(tool, args);
            if (process == null)
                return false;

            await process.StandardInput.BaseStream.WriteAsync(data, 0, data.Length);
            process.StandardInput.Close();

            var exited = await Task.Run(() => process.WaitForExit(2000));
            return exited && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string?> ReadTextAsync(string tool, string args)
    {
        var bytes = await ReadBytesAsync(tool, args);
        return bytes == null ? null : Encoding.UTF8.GetString(bytes);
    }

    private static async Task<byte[]?> ReadBytesAsync(string tool, string args)
    {
        try
        {
            using var process = CreateProcess(tool, args);
            if (process == null)
                return null;

            await using var ms = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(ms);
            var exited = await Task.Run(() => process.WaitForExit(2000));
            return exited && process.ExitCode == 0 ? ms.ToArray() : null;
        }
        catch
        {
            return null;
        }
    }

    private static Process? CreateProcess(string fileName, string args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            return Process.Start(startInfo);
        }
        catch
        {
            return null;
        }
    }
}
