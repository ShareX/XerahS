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

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using XerahS.Common;
using XerahS.Mobile.UI;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Mobile;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Mobile.Android;

[Activity(
    Label = "XerahS",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
[IntentFilter(
    new[] { Intent.ActionSend, Intent.ActionSendMultiple },
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "image/*")]
[IntentFilter(
    new[] { Intent.ActionSend },
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "application/*")]
[IntentFilter(
    new[] { Intent.ActionSend },
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "text/*")]
public class MainActivity : AvaloniaMainActivity<MobileApp>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Initialize platform services for mobile
        MobilePlatform.Initialize(PlatformType.Android);

        // Set personal folder to app's internal storage
        PathsManager.PersonalFolder = FilesDir!.AbsolutePath;

        // Load settings (UploadersConfig, WorkflowsConfig, etc.)
        XerahS.Core.SettingsManager.LoadInitialSettings();

        // Initialize plugin system
        XerahS.Core.Uploaders.ProviderContextManager.EnsureProviderContext();
        ProviderCatalog.InitializeBuiltInProviders();

        return builder
            .UseAndroid()
            .LogToTrace();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HandleShareIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent != null)
        {
            HandleShareIntent(intent);
        }
    }

#pragma warning disable CA1422 // Validate platform compatibility - these APIs work on all supported Android versions
    private void HandleShareIntent(Intent? intent)
    {
        if (intent == null) return;

        var action = intent.Action;
        if (action != Intent.ActionSend && action != Intent.ActionSendMultiple) return;

        if (string.IsNullOrEmpty(intent.Type)) return;

        var localPaths = new List<string>();

        if (action == Intent.ActionSend)
        {
            var uri = intent.GetParcelableExtra(Intent.ExtraStream) as global::Android.Net.Uri;
            if (uri != null)
            {
                var path = CopyUriToCache(uri);
                if (path != null) localPaths.Add(path);
            }
        }
        else if (action == Intent.ActionSendMultiple)
        {
            var uris = intent.GetParcelableArrayListExtra(Intent.ExtraStream);
            if (uris != null)
            {
                foreach (var item in uris)
                {
                    if (item is global::Android.Net.Uri uri)
                    {
                        var path = CopyUriToCache(uri);
                        if (path != null) localPaths.Add(path);
                    }
                }
            }
        }

        if (localPaths.Count > 0)
        {
            MobileApp.OnFilesReceived?.Invoke(localPaths.ToArray());
        }
    }
#pragma warning restore CA1422

    private string? CopyUriToCache(global::Android.Net.Uri uri)
    {
        try
        {
            var fileName = GetFileNameFromUri(uri) ?? $"share_{Guid.NewGuid():N}";
            var cachePath = Path.Combine(CacheDir!.AbsolutePath, fileName);

            using var input = ContentResolver!.OpenInputStream(uri);
            if (input == null) return null;

            using var output = File.Create(cachePath);
            input.CopyTo(output);

            return cachePath;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to copy URI to cache");
            return null;
        }
    }

#pragma warning disable CS0618 // Type or member is obsolete - using IOpenableColumns.DisplayName for backward compatibility
    private string? GetFileNameFromUri(global::Android.Net.Uri uri)
    {
        string? fileName = null;

        if (uri.Scheme == "content")
        {
            using var cursor = ContentResolver!.Query(uri, null, null, null, null);
            if (cursor != null && cursor.MoveToFirst())
            {
                var nameIndex = cursor.GetColumnIndex(global::Android.Provider.IOpenableColumns.DisplayName);
                if (nameIndex >= 0)
                {
                    fileName = cursor.GetString(nameIndex);
                }
            }
        }

        return fileName ?? Path.GetFileName(uri.Path);
    }
#pragma warning restore CS0618
}
