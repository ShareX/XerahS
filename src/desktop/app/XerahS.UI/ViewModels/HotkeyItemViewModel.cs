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

namespace XerahS.UI.ViewModels;

public partial class HotkeyItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private XerahS.Core.Hotkeys.WorkflowSettings _model;

    public string Description =>
        string.IsNullOrEmpty(Model.TaskSettings.Description)
            ? EnumExtensions.GetDescription(Model.TaskSettings.Job)
            : Model.TaskSettings.Description;

    public string KeyString => Model.HotkeyInfo.ToString();

    /// <summary>
    /// Full description using WorkflowSettings.ToString() format: "Job: KeyBinding"
    /// </summary>
    public string FullDescription => Model.ToString();

    // Expose Status for binding - reads from Model.HotkeyInfo.Status
    public Platform.Abstractions.HotkeyStatus Status => Model.HotkeyInfo.Status;

    public HotkeyItemViewModel(XerahS.Core.Hotkeys.WorkflowSettings model)
    {
        _model = model;
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(KeyString));
        OnPropertyChanged(nameof(FullDescription));
        OnPropertyChanged(nameof(Status));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHighlighted))]
    [NotifyPropertyChangedFor(nameof(NavLabelVisible))]
    [NotifyPropertyChangedFor(nameof(TrayLabelVisible))]
    [NotifyPropertyChangedFor(nameof(CanPinToTray))]
    private bool _isNavWorkflow;

    public bool IsHighlighted => IsNavWorkflow;
    
    public bool NavLabelVisible => IsNavWorkflow;

    public bool PinnedToTray
    {
        get => Model.PinnedToTray;
        set
        {
            if (Model.PinnedToTray != value)
            {
                Model.PinnedToTray = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TrayLabelVisible));
                OnPropertyChanged(nameof(CanPinToTray));
            }
        }
    }

    public bool TrayLabelVisible => IsNavWorkflow || PinnedToTray;

    public bool CanPinToTray => !IsNavWorkflow && !PinnedToTray;
}
