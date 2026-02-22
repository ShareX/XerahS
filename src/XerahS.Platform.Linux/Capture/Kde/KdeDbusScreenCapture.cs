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

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using SkiaSharp;
using Tmds.DBus;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Linux.Capture.Contracts;

namespace XerahS.Platform.Linux.Capture.Kde;

internal static class KdeDbusScreenCapture
{
    private const string KdeScreenShotBusName = "org.kde.KWin.ScreenShot2";
    private static readonly ObjectPath KdeScreenShotObjectPath = new("/org/kde/KWin/ScreenShot2");

    private enum KdeCaptureKind
    {
        InteractiveRegion,
        ActiveWindow,
        Workspace
    }

    private enum KdeInteractiveKind : uint
    {
        Window = 0,
        Screen = 1
    }

    public static async Task<SKBitmap?> CaptureAsync(LinuxCaptureKind kind, CaptureOptions? options)
    {
        var captureKind = kind switch
        {
            LinuxCaptureKind.Region => KdeCaptureKind.InteractiveRegion,
            LinuxCaptureKind.FullScreen => KdeCaptureKind.Workspace,
            LinuxCaptureKind.ActiveWindow => KdeCaptureKind.ActiveWindow,
            _ => (KdeCaptureKind?)null
        };

        if (captureKind == null)
        {
            return null;
        }

        return await CaptureWithKdeScreenShot2Async(captureKind.Value, options).ConfigureAwait(false);
    }

    private static async Task<SKBitmap?> CaptureWithKdeScreenShot2Async(KdeCaptureKind captureKind, CaptureOptions? options)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sharex_kwin_raw_{Guid.NewGuid():N}.bin");

        try
        {
            using var connection = new Connection(Address.Session);
            await connection.ConnectAsync().ConfigureAwait(false);

            var proxy = connection.CreateProxy<IKdeScreenShot2>(KdeScreenShotBusName, KdeScreenShotObjectPath);
            var kdeOptions = BuildKdeScreenShotOptions(captureKind, options);

            IDictionary<string, object> results;
            using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                results = captureKind switch
                {
                    KdeCaptureKind.InteractiveRegion => await proxy.CaptureInteractiveAsync((uint)KdeInteractiveKind.Screen, kdeOptions, stream.SafeFileHandle).ConfigureAwait(false),
                    KdeCaptureKind.ActiveWindow => await proxy.CaptureActiveWindowAsync(kdeOptions, stream.SafeFileHandle).ConfigureAwait(false),
                    KdeCaptureKind.Workspace => await proxy.CaptureWorkspaceAsync(kdeOptions, stream.SafeFileHandle).ConfigureAwait(false),
                    _ => new Dictionary<string, object>()
                };
            }

            if (!TryGetStringResult(results, "type", out var type) ||
                !string.Equals(type, "raw", StringComparison.OrdinalIgnoreCase))
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: KDE ScreenShot2 returned unsupported image type.");
                return null;
            }

            if (!TryGetUInt32Result(results, "width", out var width) ||
                !TryGetUInt32Result(results, "height", out var height) ||
                !TryGetUInt32Result(results, "stride", out var stride) ||
                !TryGetUInt32Result(results, "format", out var format))
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: KDE ScreenShot2 returned incomplete raw metadata.");
                return null;
            }

            long expectedBytes = (long)stride * height;
            if (expectedBytes <= 0)
            {
                return null;
            }

            if (!await WaitForFileLengthAsync(tempFile, expectedBytes, TimeSpan.FromSeconds(3)).ConfigureAwait(false))
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: KDE ScreenShot2 raw output timed out.");
                return null;
            }

            var rawData = await File.ReadAllBytesAsync(tempFile).ConfigureAwait(false);
            var bitmap = DecodeKdeRawBitmap(rawData, (int)width, (int)height, (int)stride, format);
            if (bitmap != null)
            {
                DebugHelper.WriteLine($"LinuxScreenCaptureService: KDE ScreenShot2 capture succeeded ({width}x{height}, format={format}).");
            }

            return bitmap;
        }
        catch (DBusException ex)
        {
            if (string.Equals(ex.ErrorName, "org.kde.KWin.ScreenShot2.Error.Cancelled", StringComparison.Ordinal))
            {
                DebugHelper.WriteLine("LinuxScreenCaptureService: KDE ScreenShot2 capture cancelled by user.");
                return null;
            }
            DebugHelper.WriteLine($"LinuxScreenCaptureService: KDE ScreenShot2 D-Bus capture failed: {ex.ErrorName} ({ex.ErrorMessage})");
            return null;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: KDE ScreenShot2 capture failed: {ex.Message}");
            return null;
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    private static IDictionary<string, object> BuildKdeScreenShotOptions(KdeCaptureKind captureKind, CaptureOptions? options)
    {
        var includeCursor = options?.ShowCursor == true;
        var dbusOptions = new Dictionary<string, object>
        {
            ["include-cursor"] = includeCursor,
            ["native-resolution"] = true
        };
        if (captureKind == KdeCaptureKind.ActiveWindow)
        {
            dbusOptions["include-decoration"] = true;
            dbusOptions["include-shadow"] = true;
        }
        return dbusOptions;
    }

    private static async Task<bool> WaitForFileLengthAsync(string path, long minimumLength, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Exists && info.Length >= minimumLength)
                {
                    return true;
                }
            }
            catch
            {
                // Best effort wait loop.
            }
            await Task.Delay(25).ConfigureAwait(false);
        }
        return false;
    }

    private static SKBitmap? DecodeKdeRawBitmap(byte[] rawData, int width, int height, int stride, uint format)
    {
        if (width <= 0 || height <= 0 || stride <= 0)
        {
            return null;
        }
        long requiredBytes = (long)stride * height;
        if (rawData.LongLength < requiredBytes)
        {
            return null;
        }

        const uint qImageFormatRgb32 = 4;
        const uint qImageFormatArgb32 = 5;
        const uint qImageFormatArgb32Premultiplied = 6;
        const uint qImageFormatRgbx8888 = 16;
        const uint qImageFormatRgba8888 = 17;
        const uint qImageFormatRgba8888Premultiplied = 18;

        bool isBgraCompatible = format == qImageFormatRgb32 ||
                                format == qImageFormatArgb32 ||
                                format == qImageFormatArgb32Premultiplied;
        bool requiresRgbToBgrSwap = format == qImageFormatRgbx8888 ||
                                    format == qImageFormatRgba8888 ||
                                    format == qImageFormatRgba8888Premultiplied;

        if (!isBgraCompatible && !requiresRgbToBgrSwap)
        {
            DebugHelper.WriteLine($"LinuxScreenCaptureService: Unsupported KDE raw image format: {format}");
            return null;
        }

        var alphaType = format switch
        {
            qImageFormatRgb32 => SKAlphaType.Opaque,
            qImageFormatRgbx8888 => SKAlphaType.Opaque,
            qImageFormatArgb32Premultiplied => SKAlphaType.Premul,
            qImageFormatRgba8888Premultiplied => SKAlphaType.Premul,
            _ => SKAlphaType.Unpremul
        };

        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, alphaType);
        IntPtr bitmapPixels = bitmap.GetPixels();
        if (bitmapPixels == IntPtr.Zero)
        {
            bitmap.Dispose();
            return null;
        }

        int targetStride = bitmap.RowBytes;
        int bytesPerPixel = 4;
        int copyWidthBytes = width * bytesPerPixel;
        var rowBuffer = requiresRgbToBgrSwap ? new byte[copyWidthBytes] : null;

        for (int y = 0; y < height; y++)
        {
            var sourceOffset = y * stride;
            var destinationRow = IntPtr.Add(bitmapPixels, y * targetStride);

            if (isBgraCompatible)
            {
                Marshal.Copy(rawData, sourceOffset, destinationRow, Math.Min(copyWidthBytes, targetStride));
                continue;
            }

            Buffer.BlockCopy(rawData, sourceOffset, rowBuffer!, 0, copyWidthBytes);
            for (int x = 0; x < width; x++)
            {
                int index = x * bytesPerPixel;
                byte r = rowBuffer![index + 0];
                byte g = rowBuffer[index + 1];
                byte b = rowBuffer[index + 2];
                byte a = rowBuffer[index + 3];
                rowBuffer[index + 0] = b;
                rowBuffer[index + 1] = g;
                rowBuffer[index + 2] = r;
                rowBuffer[index + 3] = format == qImageFormatRgbx8888 ? (byte)255 : a;
            }
            Marshal.Copy(rowBuffer!, 0, destinationRow, Math.Min(copyWidthBytes, targetStride));
        }

        return bitmap;
    }

    private static object UnwrapVariant(object value)
    {
        var current = value;
        while (current != null)
        {
            var type = current.GetType();
            var typeName = type.FullName;
            if (typeName != "Tmds.DBus.Protocol.Variant" &&
                typeName != "Tmds.DBus.Protocol.VariantValue" &&
                typeName != "Tmds.DBus.Variant")
            {
                break;
            }
            var valueProp = type.GetProperty("Value");
            var unwrapped = valueProp?.GetValue(current);
            if (unwrapped == null) break;
            current = unwrapped;
        }
        return current ?? value;
    }

    private static bool TryGetUInt32Result(IDictionary<string, object> results, string key, out uint value)
    {
        value = 0;
        if (!results.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }
        raw = UnwrapVariant(raw);
        switch (raw)
        {
            case uint uintValue:
                value = uintValue;
                return true;
            case int intValue when intValue >= 0:
                value = (uint)intValue;
                return true;
            case long longValue when longValue >= 0 && longValue <= uint.MaxValue:
                value = (uint)longValue;
                return true;
            case string stringValue when uint.TryParse(stringValue, out var parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetStringResult(IDictionary<string, object> results, string key, out string value)
    {
        value = string.Empty;
        if (!results.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }
        raw = UnwrapVariant(raw);
        value = raw.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    [DBusInterface("org.kde.KWin.ScreenShot2")]
    internal interface IKdeScreenShot2 : IDBusObject
    {
        Task<uint> GetVersionAsync();
        Task<IDictionary<string, object>> CaptureInteractiveAsync(uint kind, IDictionary<string, object> options, SafeFileHandle pipe);
        Task<IDictionary<string, object>> CaptureActiveWindowAsync(IDictionary<string, object> options, SafeFileHandle pipe);
        Task<IDictionary<string, object>> CaptureWorkspaceAsync(IDictionary<string, object> options, SafeFileHandle pipe);
    }
}
