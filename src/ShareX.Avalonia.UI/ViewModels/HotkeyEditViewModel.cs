using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Avalonia.Core.Hotkeys;
using ShareX.Avalonia.Core;
using ShareX.Avalonia.Platform.Abstractions;
using Avalonia.Input;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ShareX.Avalonia.UI.ViewModels;

public partial class HotkeyEditViewModel : ViewModelBase
{
    [ObservableProperty]
    private ShareX.Avalonia.Core.Hotkeys.HotkeySettings _model;

    [ObservableProperty]
    private Key _selectedKey;

    [ObservableProperty]
    private KeyModifiers _selectedModifiers;

    [ObservableProperty]
    private HotkeyType _selectedJob;

    public List<HotkeyType> AvailableJobs { get; }

    public string WindowTitle => Model.HotkeyInfo.Id == 0 ? "Add Hotkey" : "Edit Hotkey";

    public HotkeyEditViewModel(ShareX.Avalonia.Core.Hotkeys.HotkeySettings model)
    {
        _model = model;
        _selectedKey = model.HotkeyInfo.Key;
        _selectedModifiers = model.HotkeyInfo.Modifiers;
        _selectedJob = model.Job;

        AvailableJobs = Enum.GetValues(typeof(HotkeyType)).Cast<HotkeyType>().ToList();
    }

    public void Save()
    {
        Model.HotkeyInfo.Key = SelectedKey;
        Model.HotkeyInfo.Modifiers = SelectedModifiers;
        Model.Job = SelectedJob;
        // Model.TaskSettings.Job = SelectedJob; // Sync task settings job
    }

    [RelayCommand]
    private void Clear()
    {
        SelectedKey = Key.None;
        SelectedModifiers = KeyModifiers.None;
    }
}
