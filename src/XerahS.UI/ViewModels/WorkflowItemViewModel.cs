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
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;

namespace XerahS.UI.ViewModels;

public partial class WorkflowItemViewModel : ObservableObject
{
    private readonly WorkflowSettings _hotkeySettings;

    public WorkflowItemViewModel(WorkflowSettings hotkeySettings)
    {
        _hotkeySettings = hotkeySettings;
    }

    public WorkflowSettings Model => _hotkeySettings;

    public string Description
    {
        get => !string.IsNullOrEmpty(_hotkeySettings.TaskSettings.Description)
               ? _hotkeySettings.TaskSettings.Description
               : EnumExtensions.GetDescription(_hotkeySettings.Job);
        set
        {
            if (_hotkeySettings.TaskSettings.Description != value)
            {
                _hotkeySettings.TaskSettings.Description = value;
                OnPropertyChanged();
            }
        }
    }

    public HotkeyType Job => _hotkeySettings.Job;

    public string HotkeyText => _hotkeySettings.HotkeyInfo.ToString();

    public void Refresh()
    {
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Job));
        OnPropertyChanged(nameof(HotkeyText));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHighlighted))]
    [NotifyPropertyChangedFor(nameof(NavLabelVisible))]
    [NotifyPropertyChangedFor(nameof(TrayLabelVisible))]
    private bool _isNavWorkflow;

    public bool IsHighlighted => IsNavWorkflow;
    
    public bool NavLabelVisible => IsNavWorkflow;

    public bool PinnedToTray
    {
        get => _hotkeySettings.PinnedToTray;
        set
        {
            if (_hotkeySettings.PinnedToTray != value)
            {
                _hotkeySettings.PinnedToTray = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TrayLabelVisible));
                OnPropertyChanged(nameof(CanPinToTray));
            }
        }
    }

    public bool TrayLabelVisible => IsNavWorkflow || PinnedToTray;

    public bool CanPinToTray => !IsNavWorkflow && !PinnedToTray;
}

