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
using Avalonia.Media;
using Avalonia.Platform;
using System.Drawing;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.Views;

/// <summary>
/// Transparent window that shows a colored border around the recording area
/// Border color indicates recording technology:
/// - Red: GDI/FFmpeg fallback
/// - Green: Modern Capture (Windows.Graphics.Capture) - shown on top of Windows yellow border
/// </summary>
public partial class RecordingBorderWindow : Window
{
    private Border? _borderElement;

    public RecordingBorderWindow()
    {
        InitializeComponent();
        _borderElement = this.FindControl<Border>("BorderElement");
    }

    /// <summary>
    /// Set the border color to indicate recording technology
    /// </summary>
    /// <param name="color">Border color (e.g., "Red" for GDI, "Green" for Modern Capture)</param>
    public void SetBorderColor(string color)
    {
        if (_borderElement != null)
        {
            _borderElement.BorderBrush = new SolidColorBrush(Avalonia.Media.Color.Parse(color));
        }
    }

    /// <summary>
    /// Position and size the border window to match the recording area
    /// </summary>
    /// <param name="bounds">Recording area bounds in screen coordinates</param>
    public void SetBounds(Rectangle bounds)
    {
        Position = new PixelPoint(bounds.X, bounds.Y);
        Width = bounds.Width;
        Height = bounds.Height;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Make window click-through after it's opened (uses platform service abstraction)
        if (TryGetPlatformHandle()?.Handle is IntPtr hwnd && hwnd != IntPtr.Zero)
        {
            try
            {
                PlatformServices.Window.SetWindowClickThrough(hwnd);
            }
            catch (InvalidOperationException)
            {
                // Platform services not initialized - click-through not available
            }
        }
    }
}
