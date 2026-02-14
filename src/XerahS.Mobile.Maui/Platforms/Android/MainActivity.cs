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
using ShareX.AmazonS3.Plugin;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Uploaders;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Mobile;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Mobile.Maui;

[Activity(
    Label = "XerahS",
    Theme = "@style/Maui.SplashTheme",
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
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Full initialization sequence (needs Activity context for FilesDir and ClipboardService)
        MobilePlatform.Initialize(PlatformType.Android);
        PlatformServices.Clipboard = new AndroidClipboardService(this);
        PathsManager.PersonalFolder = FilesDir!.AbsolutePath;
        SettingsManager.LoadInitialSettings();
        ProviderContextManager.EnsureProviderContext();
        ProviderCatalog.InitializeBuiltInProviders(typeof(AmazonS3Provider).Assembly);

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

#pragma warning disable CA1422 // Validate platform compatibility
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
            App.OnFilesReceived?.Invoke(localPaths.ToArray());
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

#pragma warning disable CS0618 // Type or member is obsolete
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
