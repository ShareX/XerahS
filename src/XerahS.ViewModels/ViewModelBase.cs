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

using ReactiveUI;

namespace XerahS.ViewModels;

/// <summary>
/// Base class for all ViewModels
/// </summary>
public abstract class ViewModelBase : ReactiveObject
{
    private bool _isBusy;
    private string? _busyMessage;

    /// <summary>
    /// Indicates if the ViewModel is performing an operation
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    /// <summary>
    /// Message to display when busy
    /// </summary>
    public string? BusyMessage
    {
        get => _busyMessage;
        set => this.RaiseAndSetIfChanged(ref _busyMessage, value);
    }

    /// <summary>
    /// Executes an async operation with busy state management
    /// </summary>
    protected async Task ExecuteAsync(Func<Task> operation, string? busyMessage = null)
    {
        try
        {
            IsBusy = true;
            BusyMessage = busyMessage;
            await operation();
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    /// <summary>
    /// Executes an async operation with result and busy state management
    /// </summary>
    protected async Task<T?> ExecuteAsync<T>(Func<Task<T>> operation, string? busyMessage = null)
    {
        try
        {
            IsBusy = true;
            BusyMessage = busyMessage;
            return await operation();
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }
}
