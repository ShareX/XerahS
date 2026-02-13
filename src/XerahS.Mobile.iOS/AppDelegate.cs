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

        // Initialize plugin system
        XerahS.Core.Uploaders.ProviderContextManager.EnsureProviderContext();
        ProviderCatalog.InitializeBuiltInProviders();

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
        // Read files from App Group shared container
        var sharedContainer = NSFileManager.DefaultManager.GetContainerUrl("group.com.sharexteam.xerahs");
        if (sharedContainer == null) return;

        var sharedFolder = Path.Combine(sharedContainer.Path!, "SharedFiles");
        if (!Directory.Exists(sharedFolder)) return;

        var files = Directory.GetFiles(sharedFolder);
        if (files.Length == 0) return;

        // Copy to app's cache and clean up shared folder
        var localPaths = new List<string>();
        var cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "..", "Library", "Caches");

        foreach (var file in files)
        {
            try
            {
                var destPath = Path.Combine(cachePath, Path.GetFileName(file));
                File.Copy(file, destPath, overwrite: true);
                File.Delete(file);
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
}
