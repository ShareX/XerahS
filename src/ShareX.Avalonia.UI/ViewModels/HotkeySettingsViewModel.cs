using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Avalonia.Core;

namespace ShareX.Avalonia.UI.ViewModels;

public partial class HotkeySettingsViewModel : ViewModelBase
{
    public ObservableCollection<HotkeyItemViewModel> Hotkeys { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    private HotkeyItemViewModel? _selectedHotkey;

    public HotkeySettingsViewModel()
    {
        LoadHotkeys();
    }

    private void LoadHotkeys()
    {
        Hotkeys.Clear();
        // TODO: Load from actual HotkeysConfig.Instance.Hotkeys when fully integrated
        // For now, using the default list generator from HotkeysConfig
        var defaultHotkeys = HotkeysConfig.GetDefaultHotkeyList();
        foreach (var hk in defaultHotkeys)
        {
            Hotkeys.Add(new HotkeyItemViewModel(hk));
        }
    }

    [RelayCommand]
    private void Add()
    {
        var newHotkey = new HotkeySettings();
        var vm = new HotkeyItemViewModel(newHotkey);
        Hotkeys.Add(vm);
        SelectedHotkey = vm;
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private void Remove()
    {
        if (SelectedHotkey != null)
        {
            Hotkeys.Remove(SelectedHotkey);
            SelectedHotkey = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private void Edit()
    {
        // TODO: Open TaskSettings dialog/view for the selected hotkey
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private void Duplicate()
    {
        if (SelectedHotkey != null)
        {
            // TODO: deep copy logic
            // var clone = SelectedHotkey.Model.Clone(); 
            // Hotkeys.Add(new HotkeyItemViewModel(clone));
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private void MoveUp()
    {
        if (SelectedHotkey == null) return;
        int index = Hotkeys.IndexOf(SelectedHotkey);
        if (index > 0)
        {
            Hotkeys.Move(index, index - 1);
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private void MoveDown()
    {
        if (SelectedHotkey == null) return;
        int index = Hotkeys.IndexOf(SelectedHotkey);
        if (index < Hotkeys.Count - 1)
        {
            Hotkeys.Move(index, index + 1);
        }
    }
    
    [RelayCommand]
    private void Reset()
    {
        LoadHotkeys();
    }

    private bool CanModifyHotkey() => SelectedHotkey != null;
}
