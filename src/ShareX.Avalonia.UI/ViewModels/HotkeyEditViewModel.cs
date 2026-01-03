using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Ava.Core.Hotkeys;
using ShareX.Ava.Core;
using ShareX.Ava.Platform.Abstractions;
using Avalonia.Input;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ShareX.Ava.UI.ViewModels;

public partial class HotkeyEditViewModel : ViewModelBase
{
    [ObservableProperty]
    private ShareX.Ava.Core.Hotkeys.HotkeySettings _model;

    [ObservableProperty]
    private Key _selectedKey;

    [ObservableProperty]
    private KeyModifiers _selectedModifiers;

    [ObservableProperty]
    private HotkeyType _selectedJob;

    public List<HotkeyType> AvailableJobs { get; }

    public string WindowTitle => Model.HotkeyInfo.Id == 0 ? "Add Hotkey" : "Edit Hotkey";

    public HotkeyEditViewModel(ShareX.Ava.Core.Hotkeys.HotkeySettings model)
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

    public string KeyText 
    {
        get
        {
            if (SelectedKey == Key.None && SelectedModifiers == KeyModifiers.None)
                return "None";
                
            var info = new HotkeyInfo { Key = SelectedKey, Modifiers = SelectedModifiers };
            return info.ToString();
        }
    }

    partial void OnSelectedKeyChanged(Key value) => OnPropertyChanged(nameof(KeyText));
    partial void OnSelectedModifiersChanged(KeyModifiers value) => OnPropertyChanged(nameof(KeyText));
}
