#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

namespace ShareX.Editor.Annotations;

/// <summary>
/// Annotation/editing tool types
/// </summary>
public enum EditorTool
{
    /// <summary>
    /// Select and manipulate existing annotations
    /// </summary>
    Select,

    /// <summary>
    /// Draw rectangles
    /// </summary>
    Rectangle,

    /// <summary>
    /// Draw ellipses/circles
    /// </summary>
    Ellipse,

    /// <summary>
    /// Draw arrows
    /// </summary>
    Arrow,

    /// <summary>
    /// Draw straight lines
    /// </summary>
    Line,

    /// <summary>
    /// Add text annotations
    /// </summary>
    Text,

    /// <summary>
    /// Add numbered markers (auto-incrementing)
    /// </summary>
    Number,

    /// <summary>
    /// Create spotlight effect (darken everything except highlighted area)
    /// </summary>
    Spotlight,

    /// <summary>
    /// Crop the image
    /// </summary>
    Crop,

    /// <summary>
    /// Freehand pen drawing
    /// </summary>
    Pen,

    /// <summary>
    /// Freehand highlighter (translucent)
    /// </summary>
    Highlighter,

    /// <summary>
    /// Blur effect
    /// </summary>
    Blur,

    /// <summary>
    /// Pixelate effect
    /// </summary>
    Pixelate,

    /// <summary>
    /// Smart eraser
    /// </summary>
    SmartEraser,

    /// <summary>
    /// Magnify effect
    /// </summary>
    Magnify,

    /// <summary>
    /// Speech balloon
    /// </summary>
    SpeechBalloon,

    /// <summary>
    /// Insert image/sticker
    /// </summary>
    Image,

    /// <summary>
    /// Step/Sequence marker
    /// </summary>
    Step,

    /// <summary>
    /// Cursor stamp
    /// </summary>
    Cursor
}
