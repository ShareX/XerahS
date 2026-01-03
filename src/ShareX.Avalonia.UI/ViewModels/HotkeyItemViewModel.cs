using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.Ava.Core;
using ShareX.Ava.Common;
using ShareX.Ava.Platform.Abstractions;

namespace ShareX.Ava.UI.ViewModels;

public partial class HotkeyItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private ShareX.Ava.Core.Hotkeys.HotkeySettings _model;

    public string Description => 
        string.IsNullOrEmpty(Model.TaskSettings.Description) 
            ? EnumExtensions.GetDescription(Model.TaskSettings.Job) 
            : Model.TaskSettings.Description;
    
    public string KeyString => Model.HotkeyInfo.ToString();
    
    /// <summary>
    /// Full description using HotkeySettings.ToString() format: "Job: KeyBinding"
    /// </summary>
    public string FullDescription => Model.ToString();
    
    // Expose Status for binding - reads from Model.HotkeyInfo.Status
    public Platform.Abstractions.HotkeyStatus Status => Model.HotkeyInfo.Status;

    public HotkeyItemViewModel(ShareX.Ava.Core.Hotkeys.HotkeySettings model)
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
}
