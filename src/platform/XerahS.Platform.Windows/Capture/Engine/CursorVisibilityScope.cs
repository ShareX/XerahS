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

using XerahS.Common;

namespace XerahS.Platform.Windows.Capture.Engine;

/// <summary>
/// Hides every system cursor for the lifetime of the scope, for capture APIs that bake the cursor into the frame.
/// </summary>
internal sealed class CursorVisibilityScope : IDisposable
{
    private static readonly CursorVisibilityScope Inactive = new(hidden: false);

    private bool _hidden;

    private CursorVisibilityScope(bool hidden)
    {
        _hidden = hidden;
    }

    /// <param name="hide">Whether the cursor must be hidden at all.</param>
    /// <param name="settleMilliseconds">Time DWM needs to compose a frame without the cursor.</param>
    public static CursorVisibilityScope HideIf(bool hide, int settleMilliseconds)
    {
        if (!hide)
        {
            return Inactive;
        }

        bool hidden = false;
        try
        {
            hidden = SystemCursorGuard.TryHide();
            if (hidden && settleMilliseconds > 0)
            {
                Thread.Sleep(settleMilliseconds);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"CursorVisibilityScope: failed to hide cursor. {ex.Message}");
        }

        return hidden ? new CursorVisibilityScope(hidden: true) : Inactive;
    }

    public void Dispose()
    {
        if (_hidden)
        {
            _hidden = false;
            SystemCursorGuard.Restore();
        }
    }
}
