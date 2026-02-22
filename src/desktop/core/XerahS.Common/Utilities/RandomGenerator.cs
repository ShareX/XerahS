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

public static class RandomGenerator
{
    public const string Numbers = "0123456789";
    public const string AlphabetCapital = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string Alphabet = "abcdefghijklmnopqrstuvwxyz";
    public const string Alphanumeric = Numbers + AlphabetCapital + Alphabet;
    public const string AlphanumericInverse = Numbers + Alphabet + AlphabetCapital;
    public const string Hexadecimal = Numbers + "ABCDEF";
    public const string Base58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    public const string Base56 = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz";

    private static readonly Random random = new();

    public static char GetRandomChar(string chars)
    {
        if (string.IsNullOrEmpty(chars))
        {
            return ' ';
        }

        return chars[random.Next(chars.Length)];
    }

    public static T? Pick<T>(IList<T> list)
    {
        if (list == null || list.Count == 0)
        {
            return default;
        }

        return list[random.Next(list.Count)];
    }

    public static string GetRandomAlphanumericString(int length)
    {
        if (length <= 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new(length);

        for (int i = 0; i < length; i++)
        {
            sb.Append(Alphanumeric[random.Next(Alphanumeric.Length)]);
        }

        return sb.ToString();
    }

    public static string GetRandomNumber(int length)
    {
        if (length <= 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new(length);

        for (int i = 0; i < length; i++)
        {
            sb.Append(random.Next(10));
        }

        return sb.ToString();
    }
}
