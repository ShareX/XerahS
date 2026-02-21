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
using Android.Content.Res;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls.ApplicationLifetimes;
using ShareX.AmazonS3.Plugin;
using XerahS.Common;
using Ava;
using XerahS.Platform.Abstractions;
using XerahS.Platform.Mobile;
using XerahS.Uploaders.PluginSystem;

namespace Ava.Platforms.Android;

[Activity(
    Label = "XerahS",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/logo",
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
    /// <summary>Current activity instance for use by MobileToastService (Android Toast must run with a context).</summary>
    public static Activity? CurrentActivity { get; private set; }

    private static readonly global::Android.Graphics.Color LightSystemBarColor = global::Android.Graphics.Color.ParseColor("#FFF5F5F5");
    private static readonly global::Android.Graphics.Color DarkSystemBarColor = global::Android.Graphics.Color.ParseColor("#FF121212");

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        MobilePlatform.Initialize(PlatformType.Android);
        PlatformServices.Clipboard = new AndroidClipboardService(this);
        PathsManager.PersonalFolder = FilesDir!.AbsolutePath;
        MobileApp.RegisterBundledProvider(new AmazonS3Provider());

        return builder
            .UseAndroid()
            .LogToTrace();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // When activity is re-created (e.g. Share intent), detach the navigation root from the
        // previous activity's visual tree so Avalonia can attach it to this activity's host.
        if (Avalonia.Application.Current is MobileApp mobileApp)
        {
            mobileApp.DetachNavigationRootFromVisualTree();
        }

        base.OnCreate(savedInstanceState);
        CurrentActivity = this;
        ApplyNativeSystemBars();
        HandleShareIntent(Intent);
        StartHeartbeat();
    }

    protected override void OnDestroy()
    {
        CurrentActivity = null;
        base.OnDestroy();
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent != null)
            HandleShareIntent(intent);
    }

    private void StartHeartbeat()
    {
        var timer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += (s, e) => global::Android.Util.Log.Debug("XerahS", $"[Heartbeat] UI Thread is alive at {DateTime.Now:HH:mm:ss}");
        timer.Start();
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ApplyNativeSystemBars();
    }

    private void ApplyNativeSystemBars()
    {
        var window = Window;
        if (window == null) return;

        var isDarkTheme = (Resources?.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes;
        var targetColor = isDarkTheme ? DarkSystemBarColor : LightSystemBarColor;

        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            window.SetStatusBarColor(targetColor);
            window.SetNavigationBarColor(targetColor);
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var controller = window.InsetsController;
            if (controller == null) return;
            var lightBars = !isDarkTheme;
            const int mask = (int)WindowInsetsControllerAppearance.LightStatusBars | (int)WindowInsetsControllerAppearance.LightNavigationBars;
            controller.SetSystemBarsAppearance(lightBars ? mask : 0, mask);
            return;
        }

        var decorView = window.DecorView;
        if (decorView == null) return;
        var uiVisibility = decorView.SystemUiFlags;
        if (isDarkTheme)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(23)) uiVisibility &= ~SystemUiFlags.LightStatusBar;
            if (OperatingSystem.IsAndroidVersionAtLeast(26)) uiVisibility &= ~SystemUiFlags.LightNavigationBar;
        }
        else
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(23)) uiVisibility |= SystemUiFlags.LightStatusBar;
            if (OperatingSystem.IsAndroidVersionAtLeast(26)) uiVisibility |= SystemUiFlags.LightNavigationBar;
        }
        decorView.SystemUiFlags = uiVisibility;
    }

#pragma warning disable CA1422
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
            MobileApp.EnqueueSharedPaths(localPaths.ToArray());
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

#pragma warning disable CS0618
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
                    fileName = cursor.GetString(nameIndex);
            }
        }
        return fileName ?? Path.GetFileName(uri.Path);
    }
#pragma warning restore CS0618
}
