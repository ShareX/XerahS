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

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace XerahS.Common;

/// <summary>
/// General helper methods
/// </summary>
public static class GeneralHelpers
{
    public const string Numbers = "0123456789";
    public const string AlphabetCapital = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string Alphabet = "abcdefghijklmnopqrstuvwxyz";
    public const string Alphanumeric = Numbers + AlphabetCapital + Alphabet;
    public const string AlphanumericInverse = Numbers + Alphabet + AlphabetCapital;
    public const string Hexadecimal = Numbers + "ABCDEF";
    public const string Base58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    public const string Base56 = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz";

    public static string AddZeroes(string input, int digits = 2)
    {
        return input.PadLeft(digits, '0');
    }

    public static string AddZeroes(int number, int digits = 2)
    {
        return AddZeroes(number.ToString(), digits);
    }

    public static string HourTo12(int hour)
    {
        if (hour == 0) return "12";
        if (hour > 12) return AddZeroes(hour - 12);
        return AddZeroes(hour);
    }

    public static char GetRandomChar(string chars)
    {
        if (string.IsNullOrEmpty(chars)) return ' ';
        return chars[random.Next(chars.Length)];
    }

    public static string RepeatGenerator(int count, Func<string> generator)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            sb.Append(generator());
        }
        return sb.ToString();
    }

    public static T Pick<T>(IList<T> list)
    {
        if (list == null || list.Count == 0) return default;
        return list[random.Next(list.Count)];
    }

    private static readonly Random random = new Random();

    /// <summary>
    /// Generates a random alphanumeric string
    /// </summary>
    public static string GetRandomAlphanumericString(int length)
    {
        if (length <= 0) return string.Empty;

        StringBuilder sb = new StringBuilder(length);

        for (int i = 0; i < length; i++)
        {
            sb.Append(Alphanumeric[random.Next(Alphanumeric.Length)]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a random number string
    /// </summary>
    public static string GetRandomNumber(int length)
    {
        if (length <= 0) return string.Empty;

        StringBuilder sb = new StringBuilder(length);

        for (int i = 0; i < length; i++)
        {
            sb.Append(random.Next(10));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Opens a URL in the default browser
    /// </summary>
    public static void OpenURL(string url)
    {
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open URL: {url}, Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a string to an enum value
    /// </summary>
    public static T ParseEnum<T>(string value, T defaultValue = default) where T : struct, Enum
    {
        if (Enum.TryParse<T>(value, out T result))
        {
            return result;
        }

        return defaultValue;
    }

    /// <summary>
    /// Converts bytes to a human-readable string
    /// </summary>
    public static string GetBytesReadable(long bytes)
    {
        if (bytes <= 0) return "0 B";

        string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Repeats a string n times
    /// </summary>
    public static string RepeatString(string str, int count)
    {
        if (string.IsNullOrEmpty(str) || count <= 0) return string.Empty;

        StringBuilder sb = new StringBuilder(str.Length * count);

        for (int i = 0; i < count; i++)
        {
            sb.Append(str);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Adds zero-width space characters between each character
    /// </summary>
    public static string AddZerWidthSpaces(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        const char zeroWidthSpace = '\u200B';
        StringBuilder sb = new StringBuilder(text.Length * 2);

        for (int i = 0; i < text.Length; i++)
        {
            sb.Append(text[i]);
            if (i < text.Length - 1)
            {
                sb.Append(zeroWidthSpace);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Checks if a string is a valid URL
    /// </summary>
    public static bool IsValidURL(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Gets a timestamp string
    /// </summary>
    public static string GetTimestamp(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets current timestamp
    /// </summary>
    public static string GetTimestamp()
    {
        return GetTimestamp(DateTime.Now);
    }

    /// <summary>
    /// Checks if running on Windows 10 or greater
    /// </summary>
    public static bool IsWindows10OrGreater()
    {
        return OperatingSystem.IsWindowsVersionAtLeast(10, 0);
    }

    /// <summary>
    /// Converts byte array to hexadecimal string
    /// </summary>
    public static string BytesToHex(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return string.Empty;

        StringBuilder sb = new StringBuilder(bytes.Length * 2);

        foreach (byte b in bytes)
        {
            sb.AppendFormat("{0:x2}", b);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets all instances of a type using reflection
    /// </summary>
    public static IEnumerable<T> GetInstances<T>() where T : class
    {
        var type = typeof(T);
        var assembly = type.Assembly;

        return assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && type.IsAssignableFrom(t))
            .Select(t => Activator.CreateInstance(t) as T)
            .Where(instance => instance != null)!;
    }

    /// <summary>
    /// Compares two version objects
    /// </summary>
    public static int CompareVersion(Version version1, Version version2, bool ignoreRevision = false)
    {
        if (version1 == null && version2 == null) return 0;
        if (version1 == null) return -1;
        if (version2 == null) return 1;

        if (ignoreRevision)
        {
            var v1 = new Version(version1.Major, version1.Minor, version1.Build);
            var v2 = new Version(version2.Major, version2.Minor, version2.Build);
            return v1.CompareTo(v2);
        }

        return version1.CompareTo(version2);
    }

    /// <summary>
    /// Converts a string to proper name format (e.g., "myVariable" -> "My Variable")
    /// </summary>
    public static string GetProperName(string name, bool keepCase = false)
    {
        if (string.IsNullOrEmpty(name)) return name;

        StringBuilder result = new StringBuilder();

        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];

            if (i == 0)
            {
                result.Append(keepCase ? c : char.ToUpper(c));
            }
            else if (char.IsUpper(c))
            {
                result.Append(' ');
                result.Append(keepCase ? c : c);
            }
            else
            {
                result.Append(keepCase ? c : c);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Gets the application version
    /// </summary>
    public static string GetApplicationVersion(bool includeRevision = false)
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly() ?? System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;

        if (version == null)
        {
            return "1.0.0.0";
        }

        if (includeRevision)
        {
            return version.ToString();
        }
        else
        {
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    /// <summary>
    /// Compares two version strings
    /// </summary>
    public static int CompareVersion(string version1, string version2, bool ignoreRevision = false)
    {
        if (Version.TryParse(version1, out Version? v1) && Version.TryParse(version2, out Version? v2))
        {
            return CompareVersion(v1, v2, ignoreRevision);
        }

        return string.Compare(version1, version2, StringComparison.Ordinal);
    }
}
