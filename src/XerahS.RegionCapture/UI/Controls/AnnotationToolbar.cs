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

using Avalonia.Controls;
using Avalonia.Media;

namespace XerahS.RegionCapture.UI.Controls;

/// <summary>
/// Minimal toolbar surface used by RegionCapture overlay.
/// Upstream ShareX.ImageEditor currently does not provide AnnotationToolbar.
/// </summary>
public class AnnotationToolbar : UserControl
{
    public event EventHandler<IBrush>? ColorChanged;
    public event EventHandler<IBrush>? FillColorChanged;
    public event EventHandler<int>? WidthChanged;
    public event EventHandler<float>? FontSizeChanged;
    public event EventHandler<float>? StrengthChanged;
    public event EventHandler? ShadowButtonClick;

    // Compatibility helpers for future UI wiring.
    public void RaiseColorChanged(IBrush brush) => ColorChanged?.Invoke(this, brush);
    public void RaiseFillColorChanged(IBrush brush) => FillColorChanged?.Invoke(this, brush);
    public void RaiseWidthChanged(int width) => WidthChanged?.Invoke(this, width);
    public void RaiseFontSizeChanged(float fontSize) => FontSizeChanged?.Invoke(this, fontSize);
    public void RaiseStrengthChanged(float strength) => StrengthChanged?.Invoke(this, strength);
    public void RaiseShadowButtonClick() => ShadowButtonClick?.Invoke(this, EventArgs.Empty);
}
