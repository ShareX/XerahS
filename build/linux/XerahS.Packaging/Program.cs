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
using System.Linq;
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
        if (version.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            version = DetectVersionFromProps(publishDir) ?? "1.0.0";
            Console.WriteLine($"Auto-detected version: {version}");
        }

        string arch = args[3]; // e.g. amd64 for deb, linux-x64 for filename
        string debArch = arch switch 
        {
            "linux-x64" => "amd64",
            "linux-arm64" => "arm64",
            _ => arch
        };
        string rpmArch = MapRpmArch(arch);

        Console.WriteLine($"Packaging XerahS {version} for {debArch}...");
        Console.WriteLine($"Input: {publishDir}");
        Console.WriteLine($"Output: {outputDir}");

        Directory.CreateDirectory(outputDir);

        // Check for Windows executable
        string windowsExePath = Path.Combine(publishDir, "XerahS.exe");
        if (File.Exists(windowsExePath))
        {
            Console.WriteLine("Found XerahS.exe, creating Windows zip bundle...");
            string zipName = $"XerahS-{version}-{arch}.zip";
            string zipPath = Path.Combine(outputDir, zipName);
            
            // Delete existing to avoid error
            if (File.Exists(zipPath)) File.Delete(zipPath);

            // Create zip containing all files
            ZipFile.CreateFromDirectory(publishDir, zipPath, CompressionLevel.Optimal, false);
            Console.WriteLine($"Created Windows application archive: {zipName}");

            // Package Plugins if they exist
            PackagePlugins(publishDir, outputDir, version, arch);

            return; // Skip tarball/deb for Windows
        }

        // Check for macOS App Bundle
        string appBundlePath = Path.Combine(publishDir, "XerahS.app");
        if (Directory.Exists(appBundlePath))
        {
            Console.WriteLine("Found XerahS.app, creating macOS zip bundle...");
            string zipName = $"XerahS-{version}-{arch}.zip";
            string zipPath = Path.Combine(outputDir, zipName);
            
            // Delete existing to avoid error
            if (File.Exists(zipPath)) File.Delete(zipPath);

            // Create zip containing the .app folder
            ZipFile.CreateFromDirectory(appBundlePath, zipPath, CompressionLevel.Optimal, true);
            Console.WriteLine($"Created macOS application archive: {zipName}");

            // Package Plugins if they exist
            PackagePlugins(publishDir, outputDir, version, arch);

            return; // Skip tarball/deb for macOS app bundle
        }

        // 1. Create .tar.gz (Portable)
        string tarballName = $"XerahS-{version}-{arch}.tar.gz";
        string tarballPath = Path.Combine(outputDir, tarballName);
        CreateTarball(publishDir, tarballPath);
        Console.WriteLine($"Created portable tarball: {tarballName}");

        // Local deploys only need the tarball; CI leaves this unset and builds every format.
        if (string.Equals(Environment.GetEnvironmentVariable("XERAHS_TARBALL_ONLY"), "1", StringComparison.Ordinal))
        {
            Console.WriteLine("Skipped .deb, .rpm and AppImage (XERAHS_TARBALL_ONLY=1).");
            return;
        }

        // 2. Create .deb
        string debName = $"XerahS-{version}-{arch}.deb";
        string debPath = Path.Combine(outputDir, debName);
        CreateDeb(publishDir, debPath, version, debArch);
        Console.WriteLine($"Created Debian package: {debName}");

        // 3. Create .rpm (Fedora/RHEL)
        string rpmName = $"XerahS-{version}-{arch}.rpm";
        string rpmPath = Path.Combine(outputDir, rpmName);
        if (CreateRpm(publishDir, rpmPath, version, rpmArch))
        {
            Console.WriteLine($"Created RPM package: {rpmName}");
        }
        else
        {
            Console.WriteLine("Skipped RPM package (rpmbuild not available or build failed).");
        }

        // 4. Create AppImage (portable, no install). Does not replace Flatpak.
        if (string.Equals(Environment.GetEnvironmentVariable("XERAHS_SKIP_APPIMAGE"), "1", StringComparison.Ordinal))
        {
            Console.WriteLine("Skipped AppImage (XERAHS_SKIP_APPIMAGE=1).");
        }
        else
        {
            string appImageName = $"XerahS-{version}-{arch}.AppImage";
            string appImagePath = Path.Combine(outputDir, appImageName);
            string? iconSource = FindIconFile(publishDir);
            if (XerahS.Packaging.AppImagePackager.TryCreate(publishDir, appImagePath, version, arch, iconSource, out string? appImageError))
            {
                Console.WriteLine($"Created AppImage: {appImageName}");
            }
            else if (OperatingSystem.IsLinux())
            {
                Console.WriteLine($"Error: AppImage packaging failed: {appImageError}");
                Environment.Exit(1);
            }
            else
            {
                Console.WriteLine($"Skipped AppImage: {appImageError}");
            }
        }
    }

    static string? DetectVersionFromProps(string searchStartPath)
    {
        // Try to find Directory.Build.props by walking up from publishDir or current dir
        // Usually build/linux/XerahS.Packaging is run from repo root, so let's check repo root first
        
        string[] candidates = {
            "Directory.Build.props",
            "../Directory.Build.props",
            "../../Directory.Build.props"
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return ParseVersion(candidate);
            }
        }

        // Search recursively up from searchStartPath
        DirectoryInfo? dir = new DirectoryInfo(searchStartPath);
        while (dir != null)
        {
            string path = Path.Combine(dir.FullName, "Directory.Build.props");
            if (File.Exists(path))
            {
                return ParseVersion(path);
            }
            dir = dir.Parent;
        }

        Console.WriteLine("Warning: Could not locate Directory.Build.props for version detection.");
        return null;
    }

    static string? ParseVersion(string propsPath)
    {
        try 
        {
            string content = File.ReadAllText(propsPath);
            // Simple string parsing to avoid XML dependency overhead if possible, but Regex is safer
            // <Version>0.6.1</Version>
            var match = System.Text.RegularExpressions.Regex.Match(content, @"<Version>(.+?)</Version>");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading version from {propsPath}: {ex.Message}");
        }
        return null;
    }

    static void PackagePlugins(string publishDir, string outputDir, string version, string arch)
    {
        string pluginsDir = Path.Combine(publishDir, "Plugins");
        if (!Directory.Exists(pluginsDir)) return;

        Console.WriteLine("Found Plugins folder, packaging plugins...");
        foreach (var pluginPath in Directory.GetDirectories(pluginsDir))
        {
            try 
            {
                string pluginId = Path.GetFileName(pluginPath); // e.g. amazons3
                string zipName = $"{pluginId}-{version}-{arch}.zip";
                string zipPath = Path.Combine(outputDir, zipName);

                if (File.Exists(zipPath)) File.Delete(zipPath);

                // Zip the plugin folder (e.g. zip content will be amazons3/...)
                ZipFile.CreateFromDirectory(pluginPath, zipPath, CompressionLevel.Optimal, true);
                Console.WriteLine($"Created plugin archive: {zipName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to package plugin {pluginPath}: {ex.Message}");
            }
        }
    }

    static void CreateTarball(string sourceDir, string outputPath)
    {
        using var fileStream = File.Create(outputPath);
        using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
        using var tarWriter = new TarWriter(gzipStream, TarEntryFormat.Ustar);
        // Preserve symlinks for all tarball uses (portable archive and RPM Source0 staging tarball).
        AddDirectoryToTar(tarWriter, sourceDir, sourceDir, prependDotPrefix: false);
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

            // Create /usr/bin/xerahs as a symlink to the real binary.
            // A true symlink is required so that xdg-desktop-portal can resolve the Exec= path
            // back to the real binary via /proc/PID/exe for app-ID detection. A wrapper shell
            // script would not resolve to the same path and breaks portal app identification.
            string binPath = Path.Combine(dataRoot, "usr", "bin");
            Directory.CreateDirectory(binPath);
            string symlinkPath = Path.Combine(binPath, "xerahs");
            // Relative symlink: resolves to /usr/lib/xerahs/XerahS from /usr/bin/. A relative
// target is used so rpmbuild can correctly traverse the symlink inside BUILDROOT.
File.CreateSymbolicLink(symlinkPath, "../lib/xerahs/XerahS");
            File.CreateSymbolicLink(Path.Combine(binPath, "omaxerahs"), "../lib/xerahs/omaxerahs");

            // Create desktop entry for application menu
            string applicationsPath = Path.Combine(dataRoot, "usr", "share", "applications");
            Directory.CreateDirectory(applicationsPath);
            string desktopEntry = $"""
                [Desktop Entry]
                Name=XerahS
                Comment=Cross-platform screen capture and sharing tool
                GenericName=Screen Capture
                Exec=/usr/bin/xerahs %U
                Icon=xerahs
                Terminal=false
                Type=Application
                MimeType=x-scheme-handler/xerahs;
                Categories=Utility;Graphics;GTK;
                Keywords=screenshot;screen;capture;share;upload;
                StartupWMClass=xerahs
                X-GNOME-UsesNotifications=true
                X-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2
                """;
            File.WriteAllText(Path.Combine(applicationsPath, "xerahs.desktop"), desktopEntry);

            // Copy icon to pixmaps for application menu
            string pixmapsPath = Path.Combine(dataRoot, "usr", "share", "pixmaps");
            Directory.CreateDirectory(pixmapsPath);
            string? iconSource = FindIconFile(sourceDir);
            if (iconSource != null)
            {
                File.Copy(iconSource, Path.Combine(pixmapsPath, "xerahs.png"));
                Console.WriteLine("Added application icon to package.");
            }
            else
            {
                Console.WriteLine("Warning: Could not find Logo.png for desktop icon.");
            }

            // Global hotkey (evdev) support: udev rule + optional polkit policy.
            StageInputAccessAssets(dataRoot);
            // We will handle permissions when writing the tar

            // 2. Prepare control directory
            string controlRoot = Path.Combine(workDir, "control");
            Directory.CreateDirectory(controlRoot);

            // Maintainer scripts to enable evdev global hotkeys after install/remove.
            string postInstPath = Path.Combine(controlRoot, "postinst");
            File.WriteAllText(postInstPath, DebPostInst.Replace("\r\n", "\n"));
            string postRmPath = Path.Combine(controlRoot, "postrm");
            File.WriteAllText(postRmPath, DebPostRm.Replace("\r\n", "\n"));
            
            long installedSizeKb = GetDirectorySize(installPath) / 1024;
            
            var sb = new StringBuilder();
            sb.AppendLine("Package: xerahs");
            sb.AppendLine($"Version: {version}");
            sb.AppendLine($"Architecture: {arch}");
            sb.AppendLine("Maintainer: ShareX Team <info@getsharex.com>");
            sb.AppendLine($"Installed-Size: {installedSizeKb}");
            sb.AppendLine("Section: utils");
            sb.AppendLine("Priority: optional");
            // gnome-shell-extension-appindicator provides org.kde.StatusNotifierWatcher on GNOME,
            // which is required for the system tray icon to appear. Listed as Suggests (not Recommends)
            // so it does not auto-install on non-GNOME desktops (KDE, XFCE, etc.) where tray works
            // natively without this package.
            sb.AppendLine("Suggests: gnome-shell-extension-appindicator");
            // wl-clipboard (Wayland) and xclip (X11) back the CLI clipboard fallback used by
            // non-UI contexts (CLI tool, watch-folder daemon, pre-window startup). Neither is
            // installed on stock Ubuntu GNOME, and LinuxClipboardService degrades silently
            // without them. apt installs Recommends by default, so this fixes out-of-box
            // clipboard for background workflows while staying removable for minimal installs.
            sb.AppendLine("Recommends: wl-clipboard, xclip");
            sb.AppendLine("Description: XerahS - Cross-platform screen capture tool");
            sb.AppendLine(" A modern, cross-platform successor to ShareX.");
            sb.AppendLine(" .");
            sb.AppendLine(" On GNOME, install the gnome-shell-extension-appindicator package");
            sb.AppendLine(" to enable the system tray icon.");
            
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
                using (var tar = new TarWriter(gz, TarEntryFormat.Ustar))
                {
                    // Add control file
                    WriteTarFileEntry(tar, Path.Combine(controlRoot, "control"), "control", (UnixFileMode)Convert.ToInt32("644", 8));
                    // Maintainer scripts must be executable (755).
                    WriteTarFileEntry(tar, postInstPath, "postinst", (UnixFileMode)Convert.ToInt32("755", 8));
                    WriteTarFileEntry(tar, postRmPath, "postrm", (UnixFileMode)Convert.ToInt32("755", 8));
                }
                controlTarGz = ms.ToArray();
            }
            WriteArEntry(debStream, "control.tar.gz", controlTarGz);

            // data.tar.gz
            byte[] dataTarGz;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal))
                using (var tar = new TarWriter(gz, TarEntryFormat.Ustar))
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

    static bool CreateRpm(string sourceDir, string outputPath, string version, string arch)
    {
        if (!TryGetRpmBuildInvoker(out string rpmBuildFileName, out string rpmBuildPrefixArgs, out string rpmBuildDisplayName))
        {
            Console.WriteLine("rpmbuild not found in PATH or via flatpak-spawn --host.");
            return false;
        }

        Console.WriteLine($"Using {rpmBuildDisplayName} for RPM build.");
        bool rpmBuildRunsOnHost = rpmBuildFileName == "flatpak-spawn";
        string workRoot = rpmBuildRunsOnHost
            ? Path.Combine(Path.GetDirectoryName(outputPath) ?? sourceDir, ".tmp")
            : Path.GetTempPath();
        Directory.CreateDirectory(workRoot);
        string workDir = Path.Combine(workRoot, "xerahs_rpm_" + Guid.NewGuid());
        string rpmRoot = Path.Combine(workDir, "rpmbuild");
        string buildRoot = Path.Combine(rpmRoot, "BUILDROOT");
        string sourcesRoot = Path.Combine(rpmRoot, "SOURCES");
        string specsRoot = Path.Combine(rpmRoot, "SPECS");

        Directory.CreateDirectory(buildRoot);
        Directory.CreateDirectory(Path.Combine(rpmRoot, "BUILD"));
        Directory.CreateDirectory(Path.Combine(rpmRoot, "RPMS"));
        Directory.CreateDirectory(Path.Combine(rpmRoot, "SRPMS"));
        Directory.CreateDirectory(sourcesRoot);
        Directory.CreateDirectory(specsRoot);

        try
        {
            // Stage install tree under xerahs-{version}/usr/...
            string stageRoot = Path.Combine(workDir, "stage");
            string stagePackageRoot = Path.Combine(stageRoot, $"xerahs-{version}");
            string installPath = Path.Combine(stagePackageRoot, "usr", "lib", "xerahs");
            Directory.CreateDirectory(installPath);

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, file);
                string destFile = Path.Combine(installPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                File.Copy(file, destFile);
            }

            // /usr/bin/xerahs as a symlink (see DEB section for rationale)
            string binPath = Path.Combine(stagePackageRoot, "usr", "bin");
            Directory.CreateDirectory(binPath);
            string symlinkPath = Path.Combine(binPath, "xerahs");
            // Relative symlink: resolves to /usr/lib/xerahs/XerahS from /usr/bin/. A relative
// target is used so rpmbuild can correctly traverse the symlink inside BUILDROOT.
File.CreateSymbolicLink(symlinkPath, "../lib/xerahs/XerahS");
            File.CreateSymbolicLink(Path.Combine(binPath, "omaxerahs"), "../lib/xerahs/omaxerahs");

            // Desktop entry
            string applicationsPath = Path.Combine(stagePackageRoot, "usr", "share", "applications");
            Directory.CreateDirectory(applicationsPath);
            string desktopEntry = $"""
                [Desktop Entry]
                Name=XerahS
                Comment=Cross-platform screen capture and sharing tool
                GenericName=Screen Capture
                Exec=/usr/bin/xerahs %U
                Icon=xerahs
                Terminal=false
                Type=Application
                MimeType=x-scheme-handler/xerahs;
                Categories=Utility;Graphics;GTK;
                Keywords=screenshot;screen;capture;share;upload;
                StartupWMClass=xerahs
                X-GNOME-UsesNotifications=true
                X-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2
                """;
            File.WriteAllText(Path.Combine(applicationsPath, "xerahs.desktop"), desktopEntry);

            // Icon
            string pixmapsPath = Path.Combine(stagePackageRoot, "usr", "share", "pixmaps");
            Directory.CreateDirectory(pixmapsPath);
            string? iconSource = FindIconFile(sourceDir);
            if (iconSource != null)
            {
                File.Copy(iconSource, Path.Combine(pixmapsPath, "xerahs.png"));
            }
            else
            {
                Console.WriteLine("Warning: Could not find Logo.png for RPM icon.");
            }

            // Global hotkey (evdev) support: udev rule + optional polkit policy.
            StageInputAccessAssets(stagePackageRoot);

            // Create source tarball (contains xerahs-{version}/...)
            string sourceTarGz = Path.Combine(sourcesRoot, $"xerahs-{version}.tar.gz");
            if (Directory.Exists(stageRoot))
            {
                CreateTarball(stageRoot, sourceTarGz);
            }

            // Write spec
            string specPath = Path.Combine(specsRoot, "xerahs.spec");
            File.WriteAllText(specPath, BuildRpmSpec(version, arch));

            // Build
            bool requireDeps = IsRpmNativeHost();
            string depFlag = requireDeps ? string.Empty : " --nodeps";
            if (!requireDeps)
            {
                Console.WriteLine("Non-RPM host detected; disabling rpmbuild dependency checks.");
            }

            string rpmBuildArgs = string.IsNullOrWhiteSpace(rpmBuildPrefixArgs)
                ? $"-bb{depFlag} --target {arch} --define \"_topdir {rpmRoot}\" \"{specPath}\""
                : $"{rpmBuildPrefixArgs} -bb{depFlag} --target {arch} --define \"_topdir {rpmRoot}\" \"{specPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = rpmBuildFileName,
                Arguments = rpmBuildArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                Console.WriteLine($"Failed to start RPM build command: {rpmBuildDisplayName}");
                return false;
            }

            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                Console.WriteLine("rpmbuild failed:");
                Console.WriteLine(stdout);
                Console.WriteLine(stderr);
                return false;
            }

            // Copy resulting RPM to output path
            string rpmDir = Path.Combine(rpmRoot, "RPMS", arch);
            if (!Directory.Exists(rpmDir))
            {
                rpmDir = Path.Combine(rpmRoot, "RPMS");
            }

            string? builtRpm = Directory.Exists(rpmDir)
                ? Directory.GetFiles(rpmDir, "*.rpm", SearchOption.AllDirectories).FirstOrDefault()
                : null;

            if (builtRpm == null)
            {
                Console.WriteLine("RPM build succeeded but no RPM file found.");
                return false;
            }

            File.Copy(builtRpm, outputPath, true);
            return true;
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { }
            if (rpmBuildRunsOnHost)
            {
                try
                {
                    if (Directory.Exists(workRoot) && !Directory.EnumerateFileSystemEntries(workRoot).Any())
                    {
                        Directory.Delete(workRoot);
                    }
                }
                catch { }
            }
        }
    }

    static string BuildRpmSpec(string version, string arch)
    {
        var sb = new StringBuilder();
        sb.AppendLine("%global debug_package %{nil}");
        sb.AppendLine("%global _debuginfo_subpackages 0");
        sb.AppendLine("%global __brp_strip %{nil}");
        sb.AppendLine("%global __brp_strip_comment_note %{nil}");
        sb.AppendLine("%global __brp_strip_lto %{nil}");
        sb.AppendLine("Name: xerahs");
        sb.AppendLine($"Version: {version}");
        sb.AppendLine("Release: 1%{?dist}");
        sb.AppendLine("Summary: XerahS - Cross-platform screen capture tool");
        sb.AppendLine("License: GPL-3.0-or-later");
        sb.AppendLine("URL: https://github.com/ShareX/XerahS");
        sb.AppendLine("Source0: %{name}-%{version}.tar.gz");
        sb.AppendLine($"BuildArch: {arch}");
        sb.AppendLine("BuildRequires: desktop-file-utils");
        // gnome-shell-extension-appindicator provides org.kde.StatusNotifierWatcher on GNOME,
        // required for the system tray icon. Suggests means it is displayed as optional by dnf
        // and not pulled in automatically, so KDE/XFCE users are unaffected.
        sb.AppendLine("Suggests: gnome-shell-extension-appindicator");
        // wl-clipboard / xclip back CLI clipboard for daemon and pre-window workflows.
        sb.AppendLine("Recommends: wl-clipboard, xclip");
        sb.AppendLine();
        sb.AppendLine("%description");
        sb.AppendLine("XerahS is a modern, cross-platform screen capture and sharing tool.");
        sb.AppendLine();
        sb.AppendLine("On GNOME, install gnome-shell-extension-appindicator to enable the");
        sb.AppendLine("system tray icon (Settings > Show tray icon).");
        sb.AppendLine();
        sb.AppendLine("%prep");
        sb.AppendLine("%setup -q -n %{name}-%{version}");
        sb.AppendLine();
        sb.AppendLine("%install");
        sb.AppendLine("rm -rf %{buildroot}");
        sb.AppendLine("mkdir -p %{buildroot}/usr");
        // The staged source already mirrors the install layout (usr/lib/xerahs, usr/bin, usr/share, usr/lib/udev).
        sb.AppendLine("cp -a usr/. %{buildroot}/usr/");
        sb.AppendLine("rm -f %{buildroot}/usr/bin/xerahs");
        sb.AppendLine("rm -f %{buildroot}/usr/bin/omaxerahs");
        sb.AppendLine("ln -s /usr/lib/xerahs/XerahS %{buildroot}/usr/bin/xerahs");
        sb.AppendLine("ln -s /usr/lib/xerahs/omaxerahs %{buildroot}/usr/bin/omaxerahs");
        sb.AppendLine("chmod 755 %{buildroot}/usr/lib/xerahs/XerahS");
        sb.AppendLine("if [ -f %{buildroot}/usr/lib/xerahs/xerahs-watchfolder-daemon ]; then chmod 755 %{buildroot}/usr/lib/xerahs/xerahs-watchfolder-daemon; fi");
        sb.AppendLine("if [ -f %{buildroot}/usr/lib/xerahs/xerahs-watchfolder-daemon.exe ]; then chmod 755 %{buildroot}/usr/lib/xerahs/xerahs-watchfolder-daemon.exe; fi");
        sb.AppendLine("if [ -f %{buildroot}/usr/lib/xerahs/omaxerahs ]; then chmod 755 %{buildroot}/usr/lib/xerahs/omaxerahs; fi");
        sb.AppendLine("if [ -f %{buildroot}/usr/lib/xerahs/omaxerahs.exe ]; then chmod 755 %{buildroot}/usr/lib/xerahs/omaxerahs.exe; fi");
        sb.AppendLine("desktop-file-validate %{buildroot}/usr/share/applications/xerahs.desktop");
        sb.AppendLine();
        sb.AppendLine("%post");
        sb.AppendLine("# Enable evdev global hotkeys (XIP0080): ensure input group + reload udev.");
        sb.AppendLine("if ! getent group input >/dev/null 2>&1; then groupadd --system input || true; fi");
        sb.AppendLine("if command -v udevadm >/dev/null 2>&1; then");
        sb.AppendLine("  udevadm control --reload-rules || true");
        sb.AppendLine("  udevadm trigger --subsystem-match=input || true");
        sb.AppendLine("fi");
        sb.AppendLine();
        sb.AppendLine("%postun");
        sb.AppendLine("if command -v udevadm >/dev/null 2>&1; then");
        sb.AppendLine("  udevadm control --reload-rules || true");
        sb.AppendLine("  udevadm trigger --subsystem-match=input || true");
        sb.AppendLine("fi");
        sb.AppendLine();
        sb.AppendLine("%files");
        sb.AppendLine("%{_bindir}/xerahs");
        sb.AppendLine("%{_bindir}/omaxerahs");
        sb.AppendLine("/usr/lib/xerahs/**");
        sb.AppendLine("/usr/lib/udev/rules.d/99-xerahs-input.rules");
        sb.AppendLine("%{_datadir}/applications/xerahs.desktop");
        sb.AppendLine("%{_datadir}/pixmaps/xerahs.png");
        sb.AppendLine("%{_datadir}/polkit-1/actions/com.xerahs.input.policy");
        sb.AppendLine();
        sb.AppendLine("%changelog");
        sb.AppendLine($"* {DateTime.UtcNow:ddd MMM dd yyyy} ShareX Team <info@getsharex.com> - {version}-1");
        sb.AppendLine("- Automated build");
        return sb.ToString();
    }

    static string MapRpmArch(string arch)
    {
        return arch switch
        {
            "linux-x64" => "x86_64",
            "linux-arm64" => "aarch64",
            "amd64" => "x86_64",
            "x64" => "x86_64",
            "arm64" => "aarch64",
            "aarch64" => "aarch64",
            "armhf" => "armhfp",
            "arm" => "armhfp",
            _ => arch
        };
    }

    static bool IsToolAvailable(string toolName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = toolName,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(3000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    static bool TryGetRpmBuildInvoker(out string fileName, out string prefixArgs, out string displayName)
    {
        if (IsToolAvailable("rpmbuild"))
        {
            fileName = "rpmbuild";
            prefixArgs = string.Empty;
            displayName = "rpmbuild";
            return true;
        }

        if (IsHostToolAvailableViaFlatpak("rpmbuild"))
        {
            fileName = "flatpak-spawn";
            prefixArgs = "--host rpmbuild";
            displayName = "flatpak-spawn --host rpmbuild";
            return true;
        }

        fileName = string.Empty;
        prefixArgs = string.Empty;
        displayName = string.Empty;
        return false;
    }

    static bool IsHostToolAvailableViaFlatpak(string toolName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "flatpak-spawn",
                Arguments = $"--host which {toolName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);
            return proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout);
        }
        catch
        {
            return false;
        }
    }

    static bool IsRpmNativeHost()
    {
        try
        {
            const string osRelease = "/etc/os-release";
            if (!File.Exists(osRelease))
            {
                return false;
            }

            string id = string.Empty;
            string idLike = string.Empty;
            foreach (var line in File.ReadLines(osRelease))
            {
                if (line.StartsWith("ID="))
                {
                    id = line.Substring(3).Trim().Trim('"');
                }
                else if (line.StartsWith("ID_LIKE="))
                {
                    idLike = line.Substring(8).Trim().Trim('"');
                }
            }

            string combined = $"{id} {idLike}".ToLowerInvariant();
            return combined.Contains("fedora") ||
                   combined.Contains("rhel") ||
                   combined.Contains("centos") ||
                   combined.Contains("suse") ||
                   combined.Contains("opensuse") ||
                   combined.Contains("rocky") ||
                   combined.Contains("almalinux");
        }
        catch
        {
            return false;
        }
    }

    static void AddDirectoryToTar(TarWriter tar, string rootDir, string currentDir, bool prependDotPrefix = true)
    {
        string relativeDirPath = Path.GetRelativePath(rootDir, currentDir).Replace('\\', '/');
        if (!string.IsNullOrEmpty(relativeDirPath) && relativeDirPath != ".")
        {
            if (relativeDirPath.StartsWith("/")) relativeDirPath = relativeDirPath.Substring(1);
            relativeDirPath = (prependDotPrefix ? "./" : string.Empty) + relativeDirPath.TrimEnd('/') + "/";
            WriteTarDirectoryEntry(tar, relativeDirPath);
        }

        foreach (var file in Directory.GetFiles(currentDir))
        {
            string relativePath = Path.GetRelativePath(rootDir, file).Replace('\\', '/');
            // Make sure relative path doesn't start with /
            if (relativePath.StartsWith("/")) relativePath = relativePath.Substring(1);
            if (prependDotPrefix)
            {
                // Standard debian data.tar.gz is usually ./usr/..., so we simulate that.
                relativePath = "./" + relativePath;
            }

            // Detect and preserve symlinks: write a tar symlink entry instead of copying content.
            string? linkTarget = new FileInfo(file).LinkTarget;
            if (linkTarget != null)
            {
                WriteTarSymlinkEntry(tar, relativePath, linkTarget);
                continue;
            }

            // Permission handling
            UnixFileMode mode;
            if (IsExecutablePayloadPath(relativePath))
            {
                mode = (UnixFileMode)Convert.ToInt32("755", 8);
            }
            else
            {
                mode = (UnixFileMode)Convert.ToInt32("644", 8);
            }

            WriteTarFileEntry(tar, file, relativePath, mode);
        }

        foreach (var dir in Directory.GetDirectories(currentDir))
        {
            AddDirectoryToTar(tar, rootDir, dir, prependDotPrefix);
        }
    }

    static void WriteTarDirectoryEntry(TarWriter tar, string entryPath)
    {
        var entry = new UstarTarEntry(TarEntryType.Directory, entryPath);
        entry.Mode = (UnixFileMode)Convert.ToInt32("755", 8);
        tar.WriteEntry(entry);
    }

    static void WriteTarFileEntry(TarWriter tar, string filePath, string entryPath, UnixFileMode? modeOverride = null)
    {
        var entry = new UstarTarEntry(TarEntryType.RegularFile, entryPath);
        using var fs = File.OpenRead(filePath);
        entry.DataStream = fs;

        if (modeOverride.HasValue)
        {
            entry.Mode = modeOverride.Value;
        }
        else if (IsExecutablePayloadPath(entryPath))
        {
            entry.Mode = (UnixFileMode)Convert.ToInt32("755", 8);
        }
        else
        {
            entry.Mode = (UnixFileMode)Convert.ToInt32("644", 8);
        }

        tar.WriteEntry(entry);
    }

    static void WriteTarSymlinkEntry(TarWriter tar, string entryPath, string linkTarget)
    {
        var entry = new UstarTarEntry(TarEntryType.SymbolicLink, entryPath);
        entry.LinkName = linkTarget;
        entry.Mode = (UnixFileMode)Convert.ToInt32("777", 8);
        tar.WriteEntry(entry);
    }

    static bool IsExecutablePayloadPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        string fileName = Path.GetFileName(normalized);
        return fileName.Equals("xerahs", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("xerahs-watchfolder-daemon", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("xerahs-watchfolder-daemon.exe", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("omaxerahs", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("omaxerahs.exe", StringComparison.OrdinalIgnoreCase);
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

    // Maintainer script run after the .deb is unpacked: ensure the input group exists
    // and reload udev so the XerahS input rule (for evdev global hotkeys, XIP0080) applies.
    const string DebPostInst = """
        #!/bin/sh
        set -e
        if ! getent group input >/dev/null 2>&1; then
            groupadd --system input || true
        fi
        if command -v udevadm >/dev/null 2>&1; then
            udevadm control --reload-rules || true
            udevadm trigger --subsystem-match=input || true
        fi
        echo "XerahS: For global hotkeys on Wayland/X11, add your user to the 'input' group:"
        echo "  sudo usermod -aG input \$USER   (then log out and back in)"
        echo "Verify with: xerahs doctor --linux-input"
        exit 0

        """;

    const string DebPostRm = """
        #!/bin/sh
        set -e
        if command -v udevadm >/dev/null 2>&1; then
            udevadm control --reload-rules || true
            udevadm trigger --subsystem-match=input || true
        fi
        exit 0

        """;

    static string? FindPackagingFile(string fileName)
    {
        string[] searchPaths =
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "packaging", fileName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", fileName),
            Path.Combine(Environment.CurrentDirectory, "build", "linux", "packaging", fileName),
            Path.Combine(Environment.CurrentDirectory, "..", "build", "linux", "packaging", fileName),
        };

        foreach (var path in searchPaths)
        {
            string fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "build", "linux", "packaging", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        return null;
    }

    static void StageInputAccessAssets(string dataRoot)
    {
        string udevDir = Path.Combine(dataRoot, "usr", "lib", "udev", "rules.d");
        Directory.CreateDirectory(udevDir);
        string? udevSrc = FindPackagingFile("99-xerahs-input.rules");
        if (udevSrc != null)
        {
            File.Copy(udevSrc, Path.Combine(udevDir, "99-xerahs-input.rules"), true);
            Console.WriteLine("Added udev rule for evdev global hotkeys (99-xerahs-input.rules).");
        }
        else
        {
            Console.WriteLine("Warning: 99-xerahs-input.rules not found; global hotkey udev rule will be missing.");
        }

        string polkitDir = Path.Combine(dataRoot, "usr", "share", "polkit-1", "actions");
        Directory.CreateDirectory(polkitDir);
        string? polkitSrc = FindPackagingFile("com.xerahs.input.policy");
        if (polkitSrc != null)
        {
            File.Copy(polkitSrc, Path.Combine(polkitDir, "com.xerahs.input.policy"), true);
            Console.WriteLine("Added polkit policy for optional input-access helper (com.xerahs.input.policy).");
        }
    }

    static string? FindIconFile(string publishDir)
    {
        // Prefer the 512px app icon for AppImage / desktop pixmaps, then Logo.png.
        string inPublish512 = Path.Combine(publishDir, "ShareX.iconset", "icon_512x512.png");
        if (File.Exists(inPublish512)) return inPublish512;

        // Look for Logo.png in various locations
        // 1. Check if it's in the publish directory
        string inPublish = Path.Combine(publishDir, "Logo.png");
        if (File.Exists(inPublish)) return inPublish;

        // 2. Try to find it relative to the packaging tool location (repo structure)
        // The packaging tool is in build/linux/XerahS.Packaging, icon is in src/desktop/app/XerahS.UI/Assets/Logo.png
        string[] searchPaths =
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "src", "desktop", "app", "XerahS.UI", "Assets", "ShareX.iconset", "icon_512x512.png"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "src", "desktop", "app", "XerahS.UI", "Assets", "ShareX.iconset", "icon_512x512.png"),
            Path.Combine(Environment.CurrentDirectory, "src", "desktop", "app", "XerahS.UI", "Assets", "ShareX.iconset", "icon_512x512.png"),
            Path.Combine(Environment.CurrentDirectory, "..", "src", "desktop", "app", "XerahS.UI", "Assets", "ShareX.iconset", "icon_512x512.png"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "src", "desktop", "app", "XerahS.UI", "Assets", "Logo.png"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "src", "desktop", "app", "XerahS.UI", "Assets", "Logo.png"),
            Path.Combine(Environment.CurrentDirectory, "src", "desktop", "app", "XerahS.UI", "Assets", "Logo.png"),
            Path.Combine(Environment.CurrentDirectory, "..", "src", "desktop", "app", "XerahS.UI", "Assets", "Logo.png"),
        };

        foreach (var path in searchPaths)
        {
            string fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // 3. Walk up from publishDir to find the repo root
        DirectoryInfo? dir = new DirectoryInfo(publishDir);
        while (dir != null)
        {
            string candidate512 = Path.Combine(dir.FullName, "src", "desktop", "app", "XerahS.UI", "Assets", "ShareX.iconset", "icon_512x512.png");
            if (File.Exists(candidate512))
            {
                return candidate512;
            }

            string candidate = Path.Combine(dir.FullName, "src", "desktop", "app", "XerahS.UI", "Assets", "Logo.png");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            string legacyCandidate = Path.Combine(dir.FullName, "src", "XerahS.UI", "Assets", "Logo.png");
            if (File.Exists(legacyCandidate))
            {
                return legacyCandidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
