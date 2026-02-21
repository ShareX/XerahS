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

using Avalonia.Threading;
using XerahS.Platform.Abstractions;

namespace Ava.Services;

/// <summary>
/// IToastService for mobile: shows Android native Toast on Android, no-op on iOS.
/// Prevents "Toast service not initialized" when upload completion tries to show a toast.
/// </summary>
public sealed class MobileToastService : IToastService
{
    public void ShowToast(ToastConfig config)
    {
        if (config == null) return;

        var title = config.Title ?? "";
        var text = config.Text ?? "";
        var message = string.IsNullOrEmpty(text) ? title : (string.IsNullOrEmpty(title) ? text : $"{title}: {text}");
        if (string.IsNullOrEmpty(message)) message = "Upload completed";

#if __ANDROID__
        var activity = Ava.Platforms.Android.MainActivity.CurrentActivity;
        if (activity != null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var toast = global::Android.Widget.Toast.MakeText(activity, message, global::Android.Widget.ToastLength.Short);
                    toast?.Show();
                }
                catch
                {
                    // Ignore if activity is no longer valid
                }
            });
        }
#endif
    }

    public void CloseActiveToast()
    {
        // No persistent toast on mobile
    }
}
