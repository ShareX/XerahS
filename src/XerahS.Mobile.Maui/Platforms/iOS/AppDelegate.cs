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

using Foundation;
using UIKit;
using XerahS.Common;
using XerahS.Core.Services;

namespace XerahS.Mobile.Maui;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
    {
        // Handle custom URL scheme for share extension communication
        if (url.Scheme == "xerahs" && url.Host == "share")
        {
            ProcessSharedFiles();
            return true;
        }

        return base.OpenUrl(application, url, options);
    }

    private void ProcessSharedFiles()
    {
        // 1) Preferred path: shared pasteboard payload produced by share extension.
        // 2) Fallback path: App Group shared folder for backwards compatibility.
        var localPaths = ProcessPasteboardSharedFiles();
        if (localPaths.Count == 0)
        {
            localPaths = ProcessAppGroupSharedFiles();
        }

        if (localPaths.Count > 0)
        {
            App.OnFilesReceived?.Invoke(localPaths.ToArray());
        }
    }

    private static List<string> ProcessPasteboardSharedFiles()
    {
        var sharedPasteboardName = $"{NSBundle.MainBundle.BundleIdentifier ?? "com.sharexteam.xerahs"}.shared";
        var pasteboard = UIPasteboard.FromName(sharedPasteboardName, create: false) ?? UIPasteboard.General;
        var parsedItems = SharedPayloadService.Parse(pasteboard.String);

        if (parsedItems.Count == 0)
        {
            return [];
        }

        pasteboard.String = string.Empty;

        var cachePath = GetCacheFolderPath();
        var localPaths = new List<string>();

        foreach (var item in parsedItems)
        {
            try
            {
                var fileName = string.IsNullOrWhiteSpace(item.FileName)
                    ? $"share_{Guid.NewGuid():N}.bin"
                    : Path.GetFileName(item.FileName);
                var destPath = Path.Combine(cachePath, fileName);
                EnsureUniqueFileName(ref destPath);
                File.WriteAllBytes(destPath, item.Content);
                localPaths.Add(destPath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to process shared file");
            }
        }

        return localPaths;
    }

    private static List<string> ProcessAppGroupSharedFiles()
    {
        var sharedContainer = NSFileManager.DefaultManager.GetContainerUrl("group.com.sharexteam.xerahs");
        if (sharedContainer == null)
        {
            return [];
        }

        var sharedFolder = Path.Combine(sharedContainer.Path!, "SharedFiles");
        if (!Directory.Exists(sharedFolder))
        {
            return [];
        }

        var files = Directory.GetFiles(sharedFolder);
        if (files.Length == 0)
        {
            return [];
        }

        var cachePath = GetCacheFolderPath();
        var localPaths = new List<string>();

        foreach (var file in files)
        {
            try
            {
                var destPath = Path.Combine(cachePath, Path.GetFileName(file));
                EnsureUniqueFileName(ref destPath);
                File.Copy(file, destPath, overwrite: true);
                File.Delete(file);
                localPaths.Add(destPath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to process shared file");
            }
        }

        return localPaths;
    }

    private static string GetCacheFolderPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "..", "Library", "Caches");
    }

    private static void EnsureUniqueFileName(ref string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var dir = Path.GetDirectoryName(filePath)!;
        var name = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);
        var counter = 1;

        while (File.Exists(filePath))
        {
            filePath = Path.Combine(dir, $"{name}_{counter}{ext}");
            counter++;
        }
    }
}
