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
    private bool _isNavWorkflow;

    public bool IsHighlighted => IsNavWorkflow;
    
    public bool NavLabelVisible => IsNavWorkflow;
}
