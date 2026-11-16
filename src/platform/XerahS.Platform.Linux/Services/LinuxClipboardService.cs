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
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using XerahS.Common;
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
    private readonly Lock _clipboardOwnerLock = new();
    private Process? _clipboardOwnerProcess;
    private static readonly bool PreferWaylandClipboard =
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ||
        string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);

    public void SetText(string text) => SetTextAsync(text).GetAwaiter().GetResult();

    public string? GetText() => GetTextAsync().GetAwaiter().GetResult();

    public async Task SetTextAsync(string text)
    {
        if (await TrySetTextInternalAsync(text))
        {
            return;
        }

        LogClipboardFailure("text");
    }

    public async Task<string?> GetTextAsync()
    {
        if (PreferWaylandClipboard)
        {
            var result = await ReadTextAsync(WlPaste, string.Empty);
            if (!string.IsNullOrWhiteSpace(result))
                return result;
        }

        var fallback = await ReadTextAsync(Xclip, "-selection clipboard -o");
        if (!string.IsNullOrWhiteSpace(fallback))
            return fallback;

        if (!PreferWaylandClipboard)
        {
            var result = await ReadTextAsync(WlPaste, string.Empty);
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }

        return null;
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
        if (await TrySetImageInternalAsync(pngBytes))
        {
            return;
        }

        LogClipboardFailure("image");
    }

    internal async Task<bool> TrySetTextInternalAsync(string text)
    {
        if (PreferWaylandClipboard)
        {
            if (await TryPipeAsync(WlCopy, string.Empty, Encoding.UTF8.GetBytes(text)))
                return true;
        }

        if (await TryPipeAsync(Xclip, "-selection clipboard", Encoding.UTF8.GetBytes(text)))
            return true;

        if (!PreferWaylandClipboard && await TryPipeAsync(WlCopy, string.Empty, Encoding.UTF8.GetBytes(text)))
            return true;

        return false;
    }

    internal async Task<bool> TrySetImageInternalAsync(byte[] pngBytes)
    {
        if (PreferWaylandClipboard)
        {
            if (await TryPipeAsync(WlCopy, "--type image/png", pngBytes))
                return true;
        }

        if (await TryPipeAsync(Xclip, "-selection clipboard -t image/png -i", pngBytes))
            return true;

        if (!PreferWaylandClipboard && await TryPipeAsync(WlCopy, "--type image/png", pngBytes))
            return true;

        return false;
    }

    private static void LogClipboardFailure(string payloadKind)
    {
        string? hint = LinuxClipboardCapabilities.UserFacingWarning;
        if (string.IsNullOrWhiteSpace(hint))
        {
            hint = "Install wl-clipboard (Wayland) or xclip (X11) for CLI clipboard support.";
        }

        DebugHelper.WriteLine($"LinuxClipboardService: Failed to copy {payloadKind} to clipboard. {hint}");
    }

    public async Task<SKBitmap?> GetImageAsync()
    {
        byte[]? bytes = null;
        if (PreferWaylandClipboard)
            bytes = await ReadBytesAsync(WlPaste, "--type image/png");

        if (bytes == null || bytes.Length == 0)
            bytes = await ReadBytesAsync(Xclip, "-selection clipboard -t image/png -o");

        if ((bytes == null || bytes.Length == 0) && !PreferWaylandClipboard)
            bytes = await ReadBytesAsync(WlPaste, "--type image/png");

        if (bytes == null || bytes.Length == 0)
            return null;

        return SKBitmap.Decode(bytes);
    }

    public void SetFileDropList(string[] files)
    {
        if (files == null || files.Length == 0)
        {
            return;
        }

        // GNOME/KDE expect text/uri-list with properly escaped file URIs.
        var payload = string.Join("\n", files
            .Select(ToClipboardFileUri)
            .Where(uri => !string.IsNullOrWhiteSpace(uri)));

        if (!string.IsNullOrWhiteSpace(payload))
        {
            SetData("text/uri-list", payload);
        }
    }

    public string[]? GetFileDropList()
    {
        var data = GetData("text/uri-list") as string;
        return ParseFileDropList(data);
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
            if (PreferWaylandClipboard)
            {
                var text = ReadTextAsync(WlPaste, "--type text/uri-list").GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            var fallback = ReadTextAsync(Xclip, "-selection clipboard -t text/uri-list -o").GetAwaiter().GetResult();
            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback;

            if (!PreferWaylandClipboard)
                return ReadTextAsync(WlPaste, "--type text/uri-list").GetAwaiter().GetResult();

            return null;
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

        if (PreferWaylandClipboard)
        {
            if (TryPipeAsync(WlCopy, args, Encoding.UTF8.GetBytes(textData)).GetAwaiter().GetResult())
                return;
        }

        if (TryPipeAsync(Xclip, $"-selection clipboard{(string.IsNullOrEmpty(args) ? string.Empty : " -t " + format)}", Encoding.UTF8.GetBytes(textData)).GetAwaiter().GetResult())
            return;

        if (!PreferWaylandClipboard)
            TryPipeAsync(WlCopy, args, Encoding.UTF8.GetBytes(textData)).GetAwaiter().GetResult();
    }

    public bool ContainsData(string format)
    {
        if (string.Equals(format, "text/uri-list", StringComparison.OrdinalIgnoreCase))
            return ContainsFileDropList();

        if (string.Equals(format, "text/plain", StringComparison.OrdinalIgnoreCase))
            return ContainsText();

        return false;
    }

    internal static string[] ParseFileDropList(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return Array.Empty<string>();
        }

        return data
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseClipboardFileUri)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray()!;
    }

    internal static string? ToClipboardFileUri(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri.IsFile ? absoluteUri.AbsoluteUri : null
            : new Uri(Path.GetFullPath(path)).AbsoluteUri;
    }

    internal static string? ParseClipboardFileUri(string? uriText)
    {
        if (string.IsNullOrWhiteSpace(uriText))
        {
            return null;
        }

        return Uri.TryCreate(uriText.Trim(), UriKind.Absolute, out var uri) && uri.IsFile
            ? uri.LocalPath
            : uriText.Trim();
    }

    private async Task<bool> TryPipeAsync(string tool, string args, byte[] data)
    {
        Process? process = null;
        try
        {
            StopClipboardOwnerProcess();

            process = CreateProcess(tool, args);
            if (process == null)
                return false;

            await process.StandardInput.BaseStream.WriteAsync(data, 0, data.Length);
            process.StandardInput.Close();

            var exited = await Task.Run(() => process.WaitForExit(200));
            if (exited)
            {
                var success = process.ExitCode == 0;
                process.Dispose();
                return success;
            }

            // wl-copy/xclip often stay alive to own the selection.
            lock (_clipboardOwnerLock)
            {
                _clipboardOwnerProcess = process;
            }
            return true;
        }
        catch
        {
            process?.Dispose();
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
        const int clipboardReadTimeoutMs = 2000;

        try
        {
            using var process = CreateProcess(tool, args);
            if (process == null)
                return null;

            await using var ms = new MemoryStream();
            using var timeoutCts = new CancellationTokenSource(clipboardReadTimeoutMs);

            try
            {
                await process.StandardOutput.BaseStream.CopyToAsync(ms, timeoutCts.Token);
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore kill failures when read timed out.
                }

                return null;
            }

            return process.ExitCode == 0 ? ms.ToArray() : null;
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
                CreateNoWindow = true
            };

            return Process.Start(startInfo);
        }
        catch
        {
            return null;
        }
    }

    private void StopClipboardOwnerProcess()
    {
        lock (_clipboardOwnerLock)
        {
            if (_clipboardOwnerProcess == null)
                return;

            try
            {
                if (!_clipboardOwnerProcess.HasExited)
                    _clipboardOwnerProcess.Kill(entireProcessTree: true);

                // WaitForExit ensures the process has fully terminated before we dispose,
                // preventing orphaned/killed processes from lingering as zombie handles.
                if (!_clipboardOwnerProcess.WaitForExit(500))
                {
                    // Process did not exit within 500ms; it will be reaped by the OS.
                    // Still dispose the handle so it is not left dangling.
                }
            }
            catch
            {
                // Ignore kill/WaitForExit failures.
            }
            finally
            {
                _clipboardOwnerProcess.Dispose();
                _clipboardOwnerProcess = null;
            }
        }
    }
}
