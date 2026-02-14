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
using Avalonia;
using Avalonia.iOS;
using ShareX.AmazonS3.Plugin;
using XerahS.Common;
using XerahS.Mobile.UI;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Mobile;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Mobile.iOS;

[Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public partial class AppDelegate : AvaloniaAppDelegate<MobileApp>
#pragma warning restore CA1711
{
    private const string SharedPayloadPrefix = "xerahs_shared_payload:";

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Initialize platform services for mobile
        MobilePlatform.Initialize(PlatformType.iOS);

        // Replace in-memory clipboard with native iOS clipboard
        PlatformServices.Clipboard = new iOSClipboardService();

        // Set personal folder to app's Documents directory
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        PathsManager.PersonalFolder = documentsPath;

        // Load settings (UploadersConfig, WorkflowsConfig, etc.)
        XerahS.Core.SettingsManager.LoadInitialSettings();

        // Initialize plugin system - on mobile, plugins are bundled with the app
        XerahS.Core.Uploaders.ProviderContextManager.EnsureProviderContext();
        ProviderCatalog.InitializeBuiltInProviders(typeof(AmazonS3Provider).Assembly);

        return builder
            .UseiOS()
            .LogToTrace();
    }

    [Export("application:openURL:options:")]
    public new bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
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
        var payload = ReadSharedPayload();
        if (payload == null || payload.Count == 0) return;

        // Copy shared payload files to app cache.
        var localPaths = new List<string>();
        var cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "..", "Library", "Caches");

        foreach (var item in payload)
        {
            try
            {
                var fileName = string.IsNullOrWhiteSpace(item.FileName)
                    ? $"share_{Guid.NewGuid():N}.bin"
                    : Path.GetFileName(item.FileName);
                var destPath = Path.Combine(cachePath, fileName);
                EnsureUniqueFileName(ref destPath);
                File.WriteAllBytes(destPath, Convert.FromBase64String(item.Base64Content));
                localPaths.Add(destPath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to process shared file");
            }
        }

        if (localPaths.Count > 0)
        {
            MobileApp.OnFilesReceived?.Invoke(localPaths.ToArray());
        }
    }

    private static List<SharedFilePayload>? ReadSharedPayload()
    {
        var sharedPasteboardName = $"{NSBundle.MainBundle.BundleIdentifier ?? "com.sharexteam.xerahs"}.shared";
        var pasteboard = UIPasteboard.FromName(sharedPasteboardName, create: false) ?? UIPasteboard.General;
        var text = pasteboard.String;
        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith(SharedPayloadPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var encoded = text.Substring(SharedPayloadPrefix.Length);
            pasteboard.String = string.Empty;

            var list = new List<SharedFilePayload>();
            var lines = encoded.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var separator = line.IndexOf('|');
                if (separator <= 0 || separator == line.Length - 1) continue;

                var encodedFileName = line.Substring(0, separator);
                var base64 = line.Substring(separator + 1);
                list.Add(new SharedFilePayload
                {
                    FileName = Uri.UnescapeDataString(encodedFileName),
                    Base64Content = base64
                });
            }

            return list;
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureUniqueFileName(ref string filePath)
    {
        if (!File.Exists(filePath)) return;

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

internal class SharedFilePayload
{
    public string FileName { get; set; } = string.Empty;
    public string Base64Content { get; set; } = string.Empty;
}
