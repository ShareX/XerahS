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
    private bool _isNavWorkflow;

    public bool IsHighlighted => IsNavWorkflow;
    
    public bool NavLabelVisible => IsNavWorkflow;
}
