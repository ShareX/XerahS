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
using System.Globalization;
using System.IO;

namespace ShareX.Avalonia.Common.Helpers
{
    public static class FileHelpersLite
    {
        public static void CreateDirectoryFromFilePath(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public static string CopyFile(string filePath, string destinationFolder, bool overwrite = true)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) || string.IsNullOrEmpty(destinationFolder))
            {
                return null;
            }

            string fileName = Path.GetFileName(filePath);
            string destinationFilePath = Path.Combine(destinationFolder, fileName);
            Directory.CreateDirectory(destinationFolder);
            File.Copy(filePath, destinationFilePath, overwrite);
            return destinationFilePath;
        }

        public static string BackupFileWeekly(string filePath, string destinationFolder)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            DateTime dateTime = DateTime.Now;
            string extension = Path.GetExtension(filePath);
            string newFileName = $"{fileName}-{dateTime:yyyy-MM}-W{WeekOfYear(dateTime):00}{extension}";
            string newFilePath = Path.Combine(destinationFolder, newFileName);

            if (!File.Exists(newFilePath))
            {
                Directory.CreateDirectory(destinationFolder);
                File.Copy(filePath, newFilePath, false);
                return newFilePath;
            }

            return null;
        }

        public static string GetFileNameExtension(string filePath, bool includeDot = false)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            string extension = Path.GetExtension(filePath);

            if (string.IsNullOrEmpty(extension))
            {
                return string.Empty;
            }

            return includeDot ? extension : extension.TrimStart('.');
        }

        private static int WeekOfYear(DateTime dateTime)
        {
            return CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(dateTime, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
        }
    }
}
