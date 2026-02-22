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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Platform.Abstractions;
using System.Collections.ObjectModel;

namespace XerahS.UI.ViewModels;

public partial class WindowSelectorViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<WindowInfo> _windows = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private WindowInfo? _selectedWindow;

    public Action<WindowInfo>? OnWindowSelected { get; set; }
    public Action? OnCancelled { get; set; }

    public WindowSelectorViewModel()
    {
        RefreshWindows();
    }

    [RelayCommand]
    public void RefreshWindows()
    {
        Windows.Clear();
        if (PlatformServices.IsInitialized && PlatformServices.Window != null)
        {
            var myHandle = PlatformServices.Window.GetForegroundWindow(); // Assuming this is mostly correct context or handled elsewhere

            try
            {
                var allWindows = PlatformServices.Window.GetAllWindows();
                // Filter visible windows with titles
                // ShareX also filters "Progman", "Button" but usually Title check covers emptiness.
                var filtered = allWindows
                    .Where(w => w.IsVisible && !string.IsNullOrWhiteSpace(w.Title))
                    .OrderBy(w => w.Title);

                foreach (var w in filtered)
                {
                    Windows.Add(w);
                }
            }
            catch (Exception)
            {
                // Log?
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (SelectedWindow != null)
        {
            OnWindowSelected?.Invoke(SelectedWindow);
        }
    }

    private bool CanConfirm() => SelectedWindow != null;

    [RelayCommand]
    private void Cancel()
    {
        OnCancelled?.Invoke();
    }
}
