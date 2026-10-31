// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Finds keyboard event devices without opening them, so Quick Setup grants
// access to keyboards only and never to mice, touchpads, cameras or joysticks.
using System.Globalization;
using System.Runtime.InteropServices;
using XerahS.Platform.Linux.Input.Evdev;

namespace XerahS.Platform.Linux.Services.QuickSetup;

internal static class KeyboardDeviceLocator
{
    private const string SysClassInput = "/sys/class/input";

    /// <summary>
    /// Physical keyboards this process cannot read yet, as <c>/dev/input/eventN</c> paths.
    /// Capabilities come from sysfs, which every user can read on every distro.
    /// </summary>
    public static IReadOnlyList<string> FindKeyboardsNeedingAccess()
    {
        var readable = new HashSet<string>(
            InputDeviceEnumerator.Enumerate().Where(device => device.CanRead).Select(device => device.Path),
            StringComparer.Ordinal);

        return FindKeyboards()
            .Where(path => !readable.Contains(path))
            .ToList();
    }

    /// <summary>All physical keyboard event devices reported by sysfs.</summary>
    public static IReadOnlyList<string> FindKeyboards()
    {
        var keyboards = new List<string>();
        int wordBits = KernelWordBits();

        try
        {
            if (!Directory.Exists(SysClassInput))
            {
                return keyboards;
            }

            foreach (string eventDirectory in Directory.EnumerateDirectories(SysClassInput, "event*"))
            {
                string eventName = Path.GetFileName(eventDirectory);
                string devicePath = $"/dev/input/{eventName}";
                string capabilities = Path.Combine(eventDirectory, "device", "capabilities");

                try
                {
                    if (!File.Exists(devicePath))
                    {
                        continue;
                    }

                    string ev = File.ReadAllText(Path.Combine(capabilities, "ev"));
                    string key = File.ReadAllText(Path.Combine(capabilities, "key"));
                    string name = ReadOptional(Path.Combine(eventDirectory, "device", "name"));

                    if (IsKeyboard(ev, key, wordBits) && !InputDeviceEnumerator.IsVirtualDevice(devicePath, name))
                    {
                        keyboards.Add(devicePath);
                    }
                }
                catch (IOException)
                {
                    // Device vanished while it was inspected.
                }
                catch (UnauthorizedAccessException)
                {
                    // sysfs is normally world-readable; skip anything hardened.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            XerahS.Common.DebugHelper.WriteLine($"KeyboardDeviceLocator: cannot list {SysClassInput}. {ex.Message}");
        }

        keyboards.Sort(StringComparer.Ordinal);
        return keyboards;
    }

    /// <summary>
    /// Same test as <c>InputDeviceEnumerator.CheckIsKeyboard</c>, from sysfs bitmaps instead of ioctls:
    /// key events, Esc or Enter, and at least one QWERTY-row key (codes 30 to 44).
    /// </summary>
    /// <param name="evBitmap">Contents of <c>capabilities/ev</c>.</param>
    /// <param name="keyBitmap">Contents of <c>capabilities/key</c>.</param>
    /// <param name="wordBits">Width of the kernel's <c>long</c>, which sysfs prints each word in.</param>
    internal static bool IsKeyboard(string evBitmap, string keyBitmap, int wordBits)
    {
        IReadOnlyList<ulong> ev = ParseBitmap(evBitmap);
        IReadOnlyList<ulong> key = ParseBitmap(keyBitmap);

        if (!HasBit(ev, InputEventCodes.EV_KEY, wordBits))
        {
            return false;
        }

        if (!HasBit(key, InputEventCodes.KEY_ESC, wordBits) && !HasBit(key, InputEventCodes.KEY_ENTER, wordBits))
        {
            return false;
        }

        for (int code = 30; code <= 44; code++)
        {
            if (HasBit(key, code, wordBits))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Parses a sysfs bitmap: space-separated hex words, most significant first, leading zero words
    /// omitted. Returns the words least significant first.
    /// </summary>
    internal static IReadOnlyList<ulong> ParseBitmap(string text)
    {
        string[] words = (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<ulong>(words.Length);
        for (int i = words.Length - 1; i >= 0; i--)
        {
            result.Add(ulong.TryParse(words[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value) ? value : 0UL);
        }

        return result;
    }

    private static bool HasBit(IReadOnlyList<ulong> words, int bit, int wordBits)
    {
        int index = bit / wordBits;
        return index < words.Count && (words[index] & (1UL << (bit % wordBits))) != 0;
    }

    private static int KernelWordBits() => RuntimeInformation.OSArchitecture switch
    {
        Architecture.X86 or Architecture.Arm or Architecture.Armv6 => 32,
        _ => 64,
    };

    private static string ReadOptional(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
