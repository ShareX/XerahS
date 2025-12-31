using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.Avalonia.Core;
using ShareX.Avalonia.Common;
using ShareX.Avalonia.Platform.Abstractions;

namespace ShareX.Avalonia.UI.ViewModels;

public partial class HotkeyItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private ShareX.Avalonia.Core.Hotkeys.HotkeySettings _model;

    public string Description => 
        string.IsNullOrEmpty(Model.TaskSettings.Description) 
            ? EnumExtensions.GetDescription(Model.TaskSettings.Job) 
            : Model.TaskSettings.Description;
    
    public string KeyString => Model.HotkeyInfo.ToString();
    
    // Expose Status for binding - reads from Model.HotkeyInfo.Status
    public Platform.Abstractions.HotkeyStatus Status => Model.HotkeyInfo.Status;

    public HotkeyItemViewModel(ShareX.Avalonia.Core.Hotkeys.HotkeySettings model)
    {
        _model = model;
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(KeyString));
        OnPropertyChanged(nameof(Status));
    }
}
