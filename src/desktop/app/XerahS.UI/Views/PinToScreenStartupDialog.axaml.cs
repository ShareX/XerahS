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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SkiaSharp;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using XerahS.UI.Services;

namespace XerahS.UI.Views;

public class PinToScreenStartupResult
{
    public required SKBitmap Image { get; init; }
    public PixelPoint? Location { get; init; }
}

public partial class PinToScreenStartupDialog : Window
{
    public PinToScreenStartupResult? Result { get; private set; }

    public Func<Task<(SKBitmap? Bitmap, PixelPoint? Location)>>? SelectRegionRequested { get; set; }
    public Func<Task<string?>>? BrowseFileRequested { get; set; }

    public PinToScreenStartupDialog()
    {
        InitializeComponent();

        FromScreenButton.Click += OnFromScreenClick;
        FromClipboardButton.Click += OnFromClipboardClick;
        FromFileButton.Click += OnFromFileClick;
        CancelButton.Click += OnCancelClick;
    }

    private async void OnFromScreenClick(object? sender, RoutedEventArgs e)
    {
        if (SelectRegionRequested == null) return;

        Hide();

        try
        {
            var (bitmap, location) = await SelectRegionRequested();

            if (bitmap != null)
            {
                Result = new PinToScreenStartupResult { Image = bitmap, Location = location };
                Close();
            }
            else
            {
                Show();
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PinToScreen from screen");
            Show();
        }
    }

    private void OnFromClipboardClick(object? sender, RoutedEventArgs e)
    {
        if (!PlatformServices.IsInitialized) return;

        var bitmap = PlatformServices.Clipboard.GetImage();

        if (bitmap == null)
        {
            ShowToast("Clipboard does not contain an image.");
            return;
        }

        Result = new PinToScreenStartupResult { Image = bitmap };
        Close();
    }

    private async void OnFromFileClick(object? sender, RoutedEventArgs e)
    {
        if (BrowseFileRequested == null) return;

        var path = await BrowseFileRequested();
        if (string.IsNullOrEmpty(path)) return;

        var bitmap = SKBitmap.Decode(path);
        if (bitmap == null)
        {
            ShowToast("Failed to load image file.");
            return;
        }

        Result = new PinToScreenStartupResult { Image = bitmap };
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }

    private static void ShowToast(string text)
    {
        try
        {
            if (PlatformServices.IsToastServiceInitialized)
            {
                PlatformServices.Toast.ShowToast(new ToastConfig
                {
                    Title = "Pin to Screen",
                    Text = text,
                    Duration = 4f,
                    Size = new SizeI(420, 120),
                    AutoHide = true,
                    LeftClickAction = ToastClickAction.CloseNotification
                });
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "PinToScreen startup toast");
        }
    }
}
