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

namespace XerahS.UI.Services;

public static class UiViewModelFactoryAccessor
{
    private static IUiViewModelFactory? _factory;

    private static readonly Lock PendingLock = new();
    private static List<Action> _pending = new();

    public static bool IsAvailable => _factory != null;

    public static void Configure(IUiViewModelFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        List<Action> pending;
        lock (PendingLock)
        {
            pending = _pending;
            _pending = new List<Action>();
        }

        foreach (Action callback in pending)
        {
            callback();
        }
    }

    public static void Reset()
    {
        _factory = null;
        lock (PendingLock)
        {
            _pending = new List<Action>();
        }
    }

    /// <summary>
    /// Runs <paramref name="callback"/> now when the factory is configured, otherwise once
    /// <see cref="Configure"/> is called. Avoids startup races where a window opens before
    /// bootstrap finishes composing the UI services.
    /// </summary>
    public static void RunWhenAvailable(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (PendingLock)
        {
            if (_factory == null)
            {
                _pending.Add(callback);
                return;
            }
        }

        callback();
    }

    public static IUiViewModelFactory GetRequired()
    {
        return _factory ?? throw new InvalidOperationException("UI view model factory is not available.");
    }
}
