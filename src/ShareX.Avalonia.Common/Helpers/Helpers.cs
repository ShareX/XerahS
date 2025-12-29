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
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace ShareX.Avalonia.Common.Helpers
{
    public static class Helpers
    {
        private const string Alphanumeric = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        public static IEnumerable<T> GetInstances<T>()
        {
            Type targetType = typeof(T);
            List<T> instances = new List<T>();

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in asm.GetTypes().Where(t => targetType.IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null))
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is T instance)
                        {
                            instances.Add(instance);
                        }
                    }
                    catch
                    {
                        // Ignore types that cannot be instantiated
                    }
                }
            }

            return instances;
        }

        public static string GetRandomAlphanumeric(int length)
        {
            if (length <= 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder(length);

            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            byte[] buffer = new byte[4];

            for (int i = 0; i < length; i++)
            {
                rng.GetBytes(buffer);
                int value = BitConverter.ToInt32(buffer, 0) & int.MaxValue;
                sb.Append(Alphanumeric[value % Alphanumeric.Length]);
            }

            return sb.ToString();
        }

        public static string BytesToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        public static int CompareVersion(string? v1, string? v2)
        {
            if (!Version.TryParse(v1, out Version? version1))
            {
                version1 = new Version(0, 0, 0, 0);
            }

            if (!Version.TryParse(v2, out Version? version2))
            {
                version2 = new Version(0, 0, 0, 0);
            }

            return version1.CompareTo(version2);
        }

        public static string GetApplicationVersion()
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString() ?? "0.0.0.0";
        }

        public static Size MeasureText(string text, Font font)
        {
            if (string.IsNullOrEmpty(text) || font == null)
            {
                return Size.Empty;
            }

            using Bitmap bmp = new Bitmap(1, 1);
            using Graphics g = Graphics.FromImage(bmp);
            SizeF size = g.MeasureString(text, font);
            return Size.Ceiling(size);
        }
    }
}
