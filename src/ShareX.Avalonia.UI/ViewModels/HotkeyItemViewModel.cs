using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.Avalonia.Core;

namespace ShareX.Avalonia.UI.ViewModels;

public partial class HotkeyItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private ShareX.Avalonia.Core.Hotkeys.HotkeySettings _model;

    public string Description => Model.TaskSettings.Description ?? Model.TaskSettings.Job.ToString();
    
    public string KeyString => Model.HotkeyInfo.ToString();

    public HotkeyItemViewModel(ShareX.Avalonia.Core.Hotkeys.HotkeySettings model)
    {
        _model = model;
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(KeyString));
    }
}
