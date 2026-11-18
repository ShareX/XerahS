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

using System.Globalization;
using Avalonia;
using Avalonia.Controls;

namespace XerahS.UI.Services;

/// <summary>
/// Where the next task-mode editor opens. An external capture front end (process-image relay)
/// knows the monitor the capture came from; the editor opens there, full screen, like ShareX's
/// editor covering the taskbar on Windows. One-shot: consumed by the next task-mode editor.
/// </summary>
public static class EditorPlacement
{
    private static readonly Lock Gate = new();
    private static PixelRect? _nextMonitor;

    public static void SetNextTaskEditorMonitor(string? rect)
    {
        PixelRect? parsed = ParseRect(rect);

        lock (Gate)
        {
            _nextMonitor = parsed;
        }
    }

    /// <summary>Parses "x,y,width,height" (invariant integers, positive size); null otherwise.</summary>
    internal static PixelRect? ParseRect(string? rect)
    {
        if (string.IsNullOrWhiteSpace(rect))
        {
            return null;
        }

        string[] parts = rect.Split(',');
        if (parts.Length == 4 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) &&
            int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) &&
            int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) &&
            w > 0 && h > 0)
        {
            return new PixelRect(x, y, w, h);
        }

        return null;
    }

    /// <summary>
    /// Applies a pending placement to a task-mode editor window before it is shown: moves it onto
    /// the monitor and opens it full screen. Returns false when no placement was pending.
    /// </summary>
    public static bool TryApplyToTaskEditor(Window window)
    {
        PixelRect? monitor;
        lock (Gate)
        {
            monitor = _nextMonitor;
            _nextMonitor = null;
        }

        if (monitor is not PixelRect bounds)
        {
            return false;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = new PixelPoint(bounds.X + 40, bounds.Y + 40);
        window.Width = Math.Max(400, bounds.Width - 80);
        window.Height = Math.Max(300, bounds.Height - 80);
        window.WindowState = WindowState.FullScreen;
        return true;
    }
}
