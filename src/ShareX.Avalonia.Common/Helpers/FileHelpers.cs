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

#nullable enable

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using XerahS.Platform.Abstractions;

namespace XerahS.Common;

public static class FileHelpers
{
    public static readonly string[] ImageFileExtensions = { "jpg", "jpeg", "png", "gif", "bmp", "ico", "tif", "tiff" };
    public static readonly string[] TextFileExtensions = { "txt", "log", "nfo", "c", "cpp", "cc", "cxx", "h", "hpp", "hxx", "cs", "vb",
        "html", "htm", "xhtml", "xht", "xml", "css", "js", "php", "bat", "java", "lua", "py", "pl", "cfg", "ini", "dart", "go", "gohtml" };
    public static readonly string[] VideoFileExtensions = { "mp4", "webm", "mkv", "avi", "vob", "ogv", "ogg", "mov", "qt", "wmv", "m4p",
        "m4v", "mpg", "mp2", "mpeg", "mpe", "mpv", "m2v", "flv", "f4v" };

    public static bool IsFilenameValid(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName) && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

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

    public static string GetFileNameExtension(string filePath, bool includeDot = false, bool checkSecondExtension = true)
    {
        string extension = string.Empty;

        if (!string.IsNullOrEmpty(filePath))
        {
            int pos = filePath.LastIndexOf('.');

            if (pos >= 0)
            {
                extension = filePath.Substring(pos + 1);

                if (checkSecondExtension)
                {
                    filePath = filePath.Remove(pos);
                    string extension2 = GetFileNameExtension(filePath, false, false);

                    if (!string.IsNullOrEmpty(extension2))
                    {
                        foreach (string knownExtension in new[] { "tar" })
                        {
                            if (extension2.Equals(knownExtension, StringComparison.OrdinalIgnoreCase))
                            {
                                extension = extension2 + "." + extension;
                                break;
                            }
                        }
                    }
                }

                if (includeDot)
                {
                    extension = "." + extension;
                }
            }
        }

        return extension;
    }

    public static bool CheckExtension(string filePath, IEnumerable<string> extensions)
    {
        string ext = GetFileNameExtension(filePath);
        return !string.IsNullOrEmpty(ext) && extensions.Any(x => ext.Equals(x, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsImageFile(string filePath) => CheckExtension(filePath, ImageFileExtensions);

    public static bool IsTextFile(string filePath) => CheckExtension(filePath, TextFileExtensions);

    public static bool IsVideoFile(string filePath) => CheckExtension(filePath, VideoFileExtensions);

    public static string GetFileNameSafe(string filePath)
    {
        if (!string.IsNullOrEmpty(filePath))
        {
            int pos = filePath.LastIndexOf('\\');

            if (pos < 0)
            {
                pos = filePath.LastIndexOf('/');
            }

            if (pos >= 0)
            {
                return filePath[(pos + 1)..];
            }
        }

        return filePath;
    }

    public static string ChangeFileNameExtension(string fileName, string extension)
    {
        if (!string.IsNullOrEmpty(fileName))
        {
            int pos = fileName.LastIndexOf('.');

            if (pos >= 0)
            {
                fileName = fileName.Remove(pos);
            }

            if (!string.IsNullOrEmpty(extension))
            {
                pos = extension.LastIndexOf('.');

                if (pos >= 0)
                {
                    extension = extension[(pos + 1)..];
                }

                return $"{fileName}.{extension}";
            }
        }

        return fileName;
    }

    public static string AppendTextToFileName(string filePath, string text)
    {
        if (!string.IsNullOrEmpty(filePath))
        {
            int pos = filePath.LastIndexOf('.');

            if (pos >= 0)
            {
                return filePath[..pos] + text + filePath[pos..];
            }
        }

        return filePath + text;
    }

    public static string AppendExtension(string filePath, string extension)
    {
        return filePath.TrimEnd('.') + '.' + extension.TrimStart('.');
    }

    public static string SanitizeFileName(string fileName, string replaceWith = "")
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        fileName = fileName.Trim();

        foreach (char c in invalidChars)
        {
            fileName = fileName.Replace(c.ToString(), replaceWith);
        }

        return fileName;
    }

    public static string SanitizePath(string path, string replaceWith = "")
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        string? root = Path.GetPathRoot(path);

        if (!string.IsNullOrEmpty(root))
        {
            path = path[root.Length..];
        }

        char[] invalidChars = Path.GetInvalidFileNameChars()
            .Except(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar })
            .ToArray();

        foreach (char c in invalidChars)
        {
            path = path.Replace(c.ToString(), replaceWith);
        }

        return (root ?? string.Empty) + path;
    }

    public static string GetAbsolutePath(string path, bool supportCustomSpecialFolders = false)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        path = ExpandFolderVariables(path, supportCustomSpecialFolders);

        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        }

        return Path.GetFullPath(path);
    }

    public static string GetUniqueFilePath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return filePath;
        }

        string folderPath = Path.GetDirectoryName(filePath) ?? string.Empty;
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string fileExtension = Path.GetExtension(filePath);
        int number = 1;

        Match regex = Regex.Match(fileName, @"^(.+) \((\d+)\)$");

        if (regex.Success)
        {
            fileName = regex.Groups[1].Value;
            number = int.Parse(regex.Groups[2].Value, CultureInfo.InvariantCulture);
        }

        string newFilePath;
        do
        {
            number++;
            string newFileName = $"{fileName} ({number}){fileExtension}";
            newFilePath = Path.Combine(folderPath, newFileName);
        }
        while (File.Exists(newFilePath));

        return newFilePath;
    }

    public static long GetFileSize(string filePath)
    {
        try
        {
            return new FileInfo(filePath).Length;
        }
        catch
        {
        }

        return -1;
    }

    public static string GetFileSizeReadable(string filePath, bool binaryUnits = false)
    {
        long fileSize = GetFileSize(filePath);
        return fileSize >= 0 ? fileSize.ToSizeString(binaryUnits) : string.Empty;
    }

    public static void CreateDirectory(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath))
        {
            return;
        }

        Directory.CreateDirectory(directoryPath);
    }

    public static void CreateDirectoryFromFilePath(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public static string? CopyFile(string filePath, string destinationFolder, bool overwrite = true)
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

    public static string? BackupFileWeekly(string filePath, string destinationFolder)
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

    public static string? BackupFileZip(string filePath, string destinationFolder)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            // Create yyyy-MM subfolder
            string monthFolder = Path.Combine(destinationFolder, DateTime.Now.ToString("yyyy-MM"));
            if (!Directory.Exists(monthFolder))
            {
                Directory.CreateDirectory(monthFolder);
            }

            // Create zip file with date stamp: yyyy-MM-dd format
            string zipFileName = $"backup-{DateTime.Now:yyyy-MM-dd}.zip";
            string zipFilePath = Path.Combine(monthFolder, zipFileName);

            // If a backup for today already exists, delete it (we're updating with latest)
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }

            // Create zip file containing the database file and its associated files
            using (var archive = System.IO.Compression.ZipFile.Open(zipFilePath, System.IO.Compression.ZipArchiveMode.Create))
            {
                string fileName = Path.GetFileName(filePath);
                using (var fileStream = File.OpenRead(filePath))
                {
                    var entry = archive.CreateEntry(fileName);
                    using (var entryStream = entry.Open())
                    {
                        fileStream.CopyTo(entryStream);
                    }
                }

                // For SQLite databases, also backup WAL and SHM files if they exist
                string walFile = filePath + "-wal";
                string shmFile = filePath + "-shm";

                if (File.Exists(walFile))
                {
                    using (var fileStream = File.OpenRead(walFile))
                    {
                        var entry = archive.CreateEntry(Path.GetFileName(walFile));
                        using (var entryStream = entry.Open())
                        {
                            fileStream.CopyTo(entryStream);
                        }
                    }
                }

                if (File.Exists(shmFile))
                {
                    using (var fileStream = File.OpenRead(shmFile))
                    {
                        var entry = archive.CreateEntry(Path.GetFileName(shmFile));
                        using (var entryStream = entry.Open())
                        {
                            fileStream.CopyTo(entryStream);
                        }
                    }
                }
            }

            return zipFilePath;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to create backup: {e}");
            return null;
        }
    }

    private static int WeekOfYear(DateTime dateTime)
    {
        return CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(dateTime, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
    }

    public static bool IsFileLocked(string filePath)
    {
        try
        {
            using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            fs.Close();
        }
        catch (IOException)
        {
            return true;
        }

        return false;
    }

    public static bool DeleteFile(string filePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }

        return false;
    }

    public static bool OpenFolderWithFile(string filePath)
    {
        try
        {
            return PlatformServices.System.ShowFileInExplorer(filePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }
}
