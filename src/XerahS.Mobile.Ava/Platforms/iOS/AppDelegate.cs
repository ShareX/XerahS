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
using XerahS.Core.Services;
using Ava;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Mobile;
using XerahS.Uploaders.PluginSystem;

namespace Ava.Platforms.iOS;

[Register("AppDelegate")]
#pragma warning disable CA1711
public partial class AppDelegate : AvaloniaAppDelegate<MobileApp>
#pragma warning restore CA1711
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        ApplyNativeAppearanceDefaults();

        MobilePlatform.Initialize(PlatformType.iOS);
        PlatformServices.Clipboard = new iOSClipboardService();

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        PathsManager.PersonalFolder = documentsPath;

        XerahS.Core.SettingsManager.LoadInitialSettings();
        XerahS.Core.Uploaders.ProviderContextManager.EnsureProviderContext();
        ProviderCatalog.InitializeBuiltInProviders(typeof(AmazonS3Provider).Assembly);

        return builder
            .UseiOS()
            .LogToTrace();
    }

    private static void ApplyNativeAppearanceDefaults()
    {
        UIView.Appearance.TintColor = UIColor.FromRGB(0, 122, 255);
    }

    [Export("application:openURL:options:")]
    public new bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
    {
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
        if (payload.Count == 0) return;

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
                File.WriteAllBytes(destPath, item.Content);
                localPaths.Add(destPath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to process shared file");
            }
        }

        if (localPaths.Count > 0)
            MobileApp.OnFilesReceived?.Invoke(localPaths.ToArray());
    }

    private static IReadOnlyList<SharedPayloadFile> ReadSharedPayload()
    {
        var sharedPasteboardName = $"{NSBundle.MainBundle.BundleIdentifier ?? "com.sharexteam.xerahs"}.shared";
        var pasteboard = UIPasteboard.FromName(sharedPasteboardName, create: false) ?? UIPasteboard.General;
        var parsedItems = SharedPayloadService.Parse(pasteboard.String);
        if (parsedItems.Count > 0)
            pasteboard.String = string.Empty;
        return parsedItems;
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
