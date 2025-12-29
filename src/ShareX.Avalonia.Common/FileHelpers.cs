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
using System.Diagnostics;
using System.IO;

namespace ShareX.Avalonia.Common
{
    public static class FileHelpers
    {
        public static string ExpandFolderVariables(string path, bool supportCustomSpecialFolders = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                if (supportCustomSpecialFolders)
                {
                    foreach (var specialFolder in HelpersOptions.ShareXSpecialFolders)
                    {
                        string token = $"%{specialFolder.Key}%";
                        if (!string.IsNullOrEmpty(specialFolder.Value))
                        {
                            path = path.Replace(token, specialFolder.Value, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }

                foreach (Environment.SpecialFolder specialFolder in Enum.GetValues(typeof(Environment.SpecialFolder)))
                {
                    string token = $"%{specialFolder}%";
                    string folderPath = Environment.GetFolderPath(specialFolder);
                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        path = path.Replace(token, folderPath, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
            }

            return Environment.ExpandEnvironmentVariables(path);
        }

        public static string GetVariableFolderPath(string path, bool supportCustomSpecialFolders = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                if (supportCustomSpecialFolders)
                {
                    foreach (var specialFolder in HelpersOptions.ShareXSpecialFolders)
                    {
                        if (!string.IsNullOrEmpty(specialFolder.Value))
                        {
                            path = path.Replace(specialFolder.Value, $"%{specialFolder.Key}%", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }

                foreach (Environment.SpecialFolder specialFolder in Enum.GetValues(typeof(Environment.SpecialFolder)))
                {
                    string folderPath = Environment.GetFolderPath(specialFolder);
                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        path = path.Replace(folderPath, $"%{specialFolder}%", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
            }

            return path;
        }

        public static void CreateDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                return;
            }

            Directory.CreateDirectory(directoryPath);
        }

        public static bool OpenFolderWithFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"")
                {
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
