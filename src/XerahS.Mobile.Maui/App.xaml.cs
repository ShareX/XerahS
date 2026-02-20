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

namespace XerahS.Mobile.Maui;

public partial class App : Application
{
    /// <summary>
    /// Static callback for platform heads to push shared file paths into the UI.
    /// Set after the framework initialization completes.
    /// </summary>
    public static Action<string[]>? OnFilesReceived { get; set; }

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            return new Window(new Views.LoadingPage());
        }
        catch (System.Exception ex)
        {
#if ANDROID
            global::Android.Util.Log.Error("XerahS", "CreateWindow Crash: " + ex.ToString());
#endif
            throw;
        }
    }

    public async Task InitializeCoreAsync()
    {
        try
        {
#if ANDROID
            global::Android.Util.Log.Info("XerahS", "InitializeCoreAsync Started.");
#endif
            await XerahS.Core.SettingsManager.LoadInitialSettingsAsync();
            await XerahS.Uploaders.PluginSystem.ProviderCatalog.InitializeBuiltInProvidersAsync(typeof(ShareX.AmazonS3.Plugin.AmazonS3Provider).Assembly);
#if ANDROID
            global::Android.Util.Log.Info("XerahS", "Initialization completed. Swapping View to AppShell...");
#endif
        
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Application.Current?.Windows.FirstOrDefault() is Window window)
                {
                    window.Page = new AppShell();
#if ANDROID
                    global::Android.Util.Log.Info("XerahS", "Swapped window.Page to AppShell.");
#endif
                }
                else
                {
#if ANDROID
                    global::Android.Util.Log.Error("XerahS", "Could not find Window to swap to AppShell.");
#endif
                }
            });
        }
        catch (System.Exception ex)
        {
#if ANDROID
            global::Android.Util.Log.Error("XerahS", "Init Crash: " + ex.ToString());
#endif
        }
    }
}
