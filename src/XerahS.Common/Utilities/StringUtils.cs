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

using System.Text;

namespace XerahS.Common.Utilities;

public static class StringUtils
{
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
        if (hour == 0)
        {
            return "12";
        }

        if (hour > 12)
        {
            return AddZeroes(hour - 12);
        }

        return AddZeroes(hour);
    }

    public static string RepeatGenerator(int count, Func<string> generator)
    {
        StringBuilder sb = new();

        for (int i = 0; i < count; i++)
        {
            sb.Append(generator());
        }

        return sb.ToString();
    }

    public static string RepeatString(string str, int count)
    {
        if (string.IsNullOrEmpty(str) || count <= 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new(str.Length * count);

        for (int i = 0; i < count; i++)
        {
            sb.Append(str);
        }

        return sb.ToString();
    }

    public static string AddZeroWidthSpaces(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        const char zeroWidthSpace = '\u200B';
        StringBuilder sb = new(text.Length * 2);

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

    public static string GetBytesReadable(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] sizes = ["B", "KB", "MB", "GB", "TB", "PB"];
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    public static string BytesToHex(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new(bytes.Length * 2);

        foreach (byte b in bytes)
        {
            sb.AppendFormat("{0:x2}", b);
        }

        return sb.ToString();
    }

    public static string GetProperName(string name, bool keepCase = false)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        StringBuilder result = new();

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
                result.Append(c);
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
