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

using XerahS.Core.Services.Privacy;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.Services;

/// <summary>
/// Runs the Redact sensitive information tool on an image file from a menu and tells the user where the copy went.
/// The flow itself lives in <see cref="PrivacyRedactionService"/>.
/// </summary>
public static class RedactImageToolService
{
    private static int _running;

    public static async Task RunAsync(string? filePath)
    {
        if (!PrivacyRedactionService.IsSupportedFile(filePath)) return;
        // One review at a time: the dialogs are modal and a second scan would ask for consent again on top.
        if (Interlocked.Exchange(ref _running, 1) == 1) return;
        try
        {
            PrivacyRedactionService? service = PrivacyRedactionService.CreateDefault();
            if (service == null)
            {
                ShowToast("Redact sensitive information", "Not available in this mode.", null);
                return;
            }

            // Off the UI thread: local OCR and the AI request can take a while; the dialogs marshal themselves.
            PrivacyRedactionResult result = await Task.Run(() => service.RedactFileAsync(filePath!, CancellationToken.None));
            switch (result.Outcome)
            {
                case PrivacyRedactionOutcome.Saved:
                    ShowToast("Redacted copy saved", Path.GetFileName(result.OutputPath), result.OutputPath);
                    break;
                case PrivacyRedactionOutcome.UnsupportedImage:
                    ShowToast("Redact sensitive information", "This image cannot be redacted: it is animated, too large or unreadable.", null);
                    break;
                case PrivacyRedactionOutcome.Failed:
                    ShowToast("Redact sensitive information failed", "Nothing was saved. The original is unchanged.", null);
                    break;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private static void ShowToast(string title, string? text, string? outputPath)
    {
        try
        {
            PlatformServices.Toast?.ShowToast(new ToastConfig
            {
                Title = title,
                Text = text,
                FilePath = outputPath,
                ImagePath = outputPath,
                Duration = 6f,
                AutoHide = true,
                LeftClickAction = outputPath == null ? ToastClickAction.CloseNotification : ToastClickAction.OpenFolder
            });
        }
        catch
        {
            // Headless and early-startup hosts may not have a toast surface.
        }
    }
}
