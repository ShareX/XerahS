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

using System;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 4)
        {
            Console.WriteLine("Usage: XerahS.Packaging <publish_dir> <output_dir> <version> <arch>");
            return;
        }

        string publishDir = Path.GetFullPath(args[0]);
        string outputDir = Path.GetFullPath(args[1]);
        string version = args[2];
        string arch = args[3]; // e.g. amd64 for deb, linux-x64 for filename
        string debArch = arch == "linux-x64" ? "amd64" : arch; // map linux-x64 to debian amd64

        Console.WriteLine($"Packaging XerahS {version} for {debArch}...");
        Console.WriteLine($"Input: {publishDir}");
        Console.WriteLine($"Output: {outputDir}");

        Directory.CreateDirectory(outputDir);

        // 1. Create .tar.gz (Portable)
        string tarballName = $"XerahS-{version}-{arch}.tar.gz";
        string tarballPath = Path.Combine(outputDir, tarballName);
        CreateTarball(publishDir, tarballPath);
        Console.WriteLine($"Created portable tarball: {tarballName}");

        // 2. Create .deb
        string debName = $"XerahS-{version}-{arch}.deb";
        string debPath = Path.Combine(outputDir, debName);
        CreateDeb(publishDir, debPath, version, debArch);
        Console.WriteLine($"Created Debian package: {debName}");
    }

    static void CreateTarball(string sourceDir, string outputPath)
    {
        using var fileStream = File.Create(outputPath);
        using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
        using var tarWriter = new TarWriter(gzipStream);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDir, file);
            // In tarball, just put them in root
            tarWriter.WriteEntry(file, relativePath);
        }
    }

    static void CreateDeb(string sourceDir, string outputPath, string version, string arch)
    {
        string workDir = Path.Combine(Path.GetTempPath(), "xerahs_deb_" + Guid.NewGuid());
        Directory.CreateDirectory(workDir);

        try
        {
            // 1. Prepare data directory (simulating install structure)
            // Install to /opt/xerahs/
            string dataRoot = Path.Combine(workDir, "data");
            string installPath = Path.Combine(dataRoot, "usr", "lib", "xerahs"); // Or /opt/xerahs, let's stick to /usr/lib/xerahs for standard linux layout
            Directory.CreateDirectory(installPath);
            
            // Copy files
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, file);
                string destFile = Path.Combine(installPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                File.Copy(file, destFile);
            }

            // Create symlink binary in /usr/bin/xerahs
            string binPath = Path.Combine(dataRoot, "usr", "bin");
            Directory.CreateDirectory(binPath);
            // We can't easily create a true symlink on Windows for the tar archive without specific library calls or storing it as a special entry. 
            // A safer bet for cross-platform packaging is a wrapper script.
            string wrapperScript = Path.Combine(binPath, "xerahs");
            File.WriteAllText(wrapperScript, "#!/bin/sh\nexec /usr/lib/xerahs/XerahS \"$@\"\n");
            // We will handle permissions when writing the tar

            // 2. Prepare control directory
            string controlRoot = Path.Combine(workDir, "control");
            Directory.CreateDirectory(controlRoot);
            
            long installedSizeKb = GetDirectorySize(installPath) / 1024;
            
            var sb = new StringBuilder();
            sb.AppendLine("Package: xerahs");
            sb.AppendLine($"Version: {version}");
            sb.AppendLine($"Architecture: {arch}");
            sb.AppendLine("Maintainer: ShareX Team <info@sharex.com>");
            sb.AppendLine($"Installed-Size: {installedSizeKb}");
            sb.AppendLine("Section: utils");
            sb.AppendLine("Priority: optional");
            sb.AppendLine("Description: XerahS - Cross-platform screen capture tool");
            sb.AppendLine(" A modern, cross-platform successor to ShareX.");
            
            File.WriteAllText(Path.Combine(controlRoot, "control"), sb.ToString());

            // 3. Create .deb AR archive
            using var debStream = File.Create(outputPath);
            // AR format is very simple. https://en.wikipedia.org/wiki/Ar_(Unix)
            // Magic string
            byte[] magic = Encoding.ASCII.GetBytes("!<arch>\n");
            debStream.Write(magic);

            // 2.0 debian-binary
            WriteArEntry(debStream, "debian-binary", Encoding.ASCII.GetBytes("2.0\n"));

            // control.tar.gz
            byte[] controlTarGz;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal))
                using (var tar = new TarWriter(gz))
                {
                    // Add control file
                    tar.WriteEntry(Path.Combine(controlRoot, "control"), "control");
                }
                controlTarGz = ms.ToArray();
            }
            WriteArEntry(debStream, "control.tar.gz", controlTarGz);

            // data.tar.gz
            byte[] dataTarGz;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal))
                using (var tar = new TarWriter(gz))
                {
                    // Add app files recursively
                    // Note: Permissions must be set correctly for Linux!
                    // XerahS executable needs 755
                    // Wrapper script needs 755
                    // Others can be 644
                    
                    AddDirectoryToTar(tar, dataRoot, dataRoot);
                }
                dataTarGz = ms.ToArray();
            }
            WriteArEntry(debStream, "data.tar.gz", dataTarGz);
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { }
        }
    }

    static void AddDirectoryToTar(TarWriter tar, string rootDir, string currentDir)
    {
        foreach (var file in Directory.GetFiles(currentDir))
        {
            string relativePath = Path.GetRelativePath(rootDir, file).Replace('\\', '/');
            // Make sure relative path doesn't start with /
            if (relativePath.StartsWith("/")) relativePath = relativePath.Substring(1);
            // Standard debian data.tar.gz is usually ./usr/..., so we simulate that
            relativePath = "./" + relativePath;

            var entry = new PaxTarEntry(TarEntryType.RegularFile, relativePath);
            using var fs = File.OpenRead(file);
            entry.DataStream = fs;
            
            // Permission handling
            if (relativePath.EndsWith("/xerahs") || relativePath.EndsWith("/XerahS")) // Wrapper script or main executable
            {
                entry.Mode = (UnixFileMode)Convert.ToInt32("755", 8);
            }
            else
            {
                entry.Mode = (UnixFileMode)Convert.ToInt32("644", 8);
            }
            
            tar.WriteEntry(entry);
        }

        foreach (var dir in Directory.GetDirectories(currentDir))
        {
            AddDirectoryToTar(tar, rootDir, dir);
        }
    }

    static void WriteArEntry(Stream stream, string name, byte[] content)
    {
        // AR Header 60 bytes
        // File identifier    16 bytes
        // File modification timestamp 12 bytes
        // Owner ID           6 bytes
        // Group ID           6 bytes
        // File mode          8 bytes
        // File size in bytes 10 bytes
        // Ending characters  2 bytes (`\``\n`)
        
        // Pad name with spaces to 16
        // name + "/" is common SysV variant, but deb uses plain names usually
        string nameField = (name + "/").PadRight(16);
        string dateField = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString().PadRight(12);
        string ownerField = "0".PadRight(6);
        string groupField = "0".PadRight(6);
        string modeField = "100644".PadRight(8); // File mode
        string sizeField = content.Length.ToString().PadRight(10);
        string magic = "`\n";

        byte[] header = Encoding.ASCII.GetBytes(nameField + dateField + ownerField + groupField + modeField + sizeField + magic);
        stream.Write(header);
        stream.Write(content);
        
        // Output must be 2-byte aligned
        if (content.Length % 2 != 0)
        {
            stream.WriteByte((byte)'\n');
        }
    }
    
    static long GetDirectorySize(string p)
    {
        long size = 0;
        foreach (string file in Directory.GetFiles(p, "*", SearchOption.AllDirectories))
        {
            size += new FileInfo(file).Length;
        }
        return size;
    }
}
