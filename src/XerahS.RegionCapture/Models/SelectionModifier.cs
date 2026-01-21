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
namespace XerahS.RegionCapture.Models;

/// <summary>
/// Represents keyboard modifiers that affect selection behavior.
/// </summary>
[Flags]
public enum SelectionModifier
{
    None = 0,

    /// <summary>
    /// Shift key - locks aspect ratio during drag.
    /// </summary>
    LockAspectRatio = 1,

    /// <summary>
    /// Ctrl key - enables pixel nudge mode with arrow keys.
    /// </summary>
    PixelNudge = 2,

    /// <summary>
    /// Alt key - expands selection from center.
    /// </summary>
    FromCenter = 4
}
