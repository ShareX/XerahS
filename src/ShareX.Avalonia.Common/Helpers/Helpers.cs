#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace ShareX.Avalonia.Common
{
    public static class Helpers
    {
        public const string Numbers = "0123456789"; // 48 ... 57
        public const string AlphabetCapital = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"; // 65 ... 90
        public const string Alphabet = "abcdefghijklmnopqrstuvwxyz"; // 97 ... 122
        public const string Alphanumeric = Numbers + AlphabetCapital + Alphabet;
        public const string AlphanumericInverse = Numbers + Alphabet + AlphabetCapital;
        public const string Hexadecimal = Numbers + "ABCDEF";
        public const string Base58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"; // https://en.wikipedia.org/wiki/Base58
        public const string Base56 = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz"; // A variant, Base56, excludes 1 (one) and o (lowercase o) compared to Base 58.

        public static string AddZeroes(string input, int digits = 2)
        {
            return input.PadLeft(digits, '0');
        }

        public static string AddZeroes(int number, int digits = 2)
        {
            return AddZeroes(number.ToString(), digits);
        }

        public static char GetRandomChar(string chars)
        {
            return chars[RandomCrypto.Next(chars.Length - 1)];
        }

        public static string GetRandomString(string chars, int length)
        {
            StringBuilder sb = new StringBuilder();

            while (length-- > 0)
            {
                sb.Append(GetRandomChar(chars));
            }

            return sb.ToString();
        }

        public static string GetRandomNumber(int length)
        {
            return GetRandomString(Numbers, length);
        }

        public static string GetRandomAlphanumeric(int length)
        {
            return GetRandomString(Alphanumeric, length);
        }

        public static string GetRandomKey(int length = 5, int count = 3, char separator = '-')
        {
            return Enumerable.Range(1, ((length + 1) * count) - 1).Aggregate("", (x, index) => x += index % (length + 1) == 0 ? separator : GetRandomChar(Alphanumeric));
        }

        public static string GetUniqueID()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string SafeStringFormat(string format, params object[] args)
        {
            return SafeStringFormat(null, format, args);
        }

        public static string SafeStringFormat(IFormatProvider provider, string format, params object[] args)
        {
            try
            {
                if (provider != null)
                {
                    return string.Format(provider, format, args);
                }

                return string.Format(format, args);
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
            }

            return format;
        }
        
        public static string BytesToHex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte x in bytes)
            {
                sb.Append(string.Format("{0:x2}", x));
            }
            return sb.ToString();
        }

        public static byte[] ComputeSHA256(byte[] data)
        {
            using (HashAlgorithm hashAlgorithm = SHA256.Create())
            {
                return hashAlgorithm.ComputeHash(data);
            }
        }

        public static byte[] ComputeSHA256(Stream stream, int bufferSize = 1024 * 32)
        {
            BufferedStream bufferedStream = new BufferedStream(stream, bufferSize);

            using (HashAlgorithm hashAlgorithm = SHA256.Create())
            {
                return hashAlgorithm.ComputeHash(bufferedStream);
            }
        }

        public static byte[] ComputeSHA256(string data)
        {
            return ComputeSHA256(Encoding.UTF8.GetBytes(data));
        }

        public static byte[] ComputeHMACSHA256(byte[] data, byte[] key)
        {
            using (HMACSHA256 hashAlgorithm = new HMACSHA256(key))
            {
                return hashAlgorithm.ComputeHash(data);
            }
        }

        public static byte[] ComputeHMACSHA256(string data, string key)
        {
            return ComputeHMACSHA256(Encoding.UTF8.GetBytes(data), Encoding.UTF8.GetBytes(key));
        }

        public static byte[] ComputeHMACSHA256(byte[] data, string key)
        {
            return ComputeHMACSHA256(data, Encoding.UTF8.GetBytes(key));
        }

        public static byte[] ComputeHMACSHA256(string data, byte[] key)
        {
            return ComputeHMACSHA256(Encoding.UTF8.GetBytes(data), key);
        }

        public static T[] GetEnums<T>()
        {
            return (T[])Enum.GetValues(typeof(T));
        }
        
        public static string GetProperName(string name, bool keepCase = false)
        {
             StringBuilder sb = new StringBuilder();

             bool number = false;

             for (int i = 0; i < name.Length; i++)
             {
                 char c = name[i];

                 if (i > 0 && (char.IsUpper(c) || (!number && char.IsNumber(c))))
                 {
                     sb.Append(' ');

                     if (keepCase)
                     {
                         sb.Append(c);
                     }
                     else
                     {
                         sb.Append(char.ToLowerInvariant(c));
                     }
                 }
                 else
                 {
                     sb.Append(c);
                 }

                 number = char.IsNumber(c);
             }

             return sb.ToString();
        }

        public static int CompareVersion(Version version1, Version version2, bool ignoreRevision = false)
        {
            if (ignoreRevision)
            {
                version1 = new Version(Math.Max(version1.Major, 0), Math.Max(version1.Minor, 0), Math.Max(version1.Build, 0));
                version2 = new Version(Math.Max(version2.Major, 0), Math.Max(version2.Minor, 0), Math.Max(version2.Build, 0));
            }

            return version1.CompareTo(version2);
        }
    }
}
