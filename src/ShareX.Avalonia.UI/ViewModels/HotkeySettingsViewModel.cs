using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Avalonia.Core;

using System.Threading.Tasks;

namespace ShareX.Avalonia.UI.ViewModels;

public partial class HotkeySettingsViewModel : ViewModelBase
{
    public ObservableCollection<HotkeyItemViewModel> Hotkeys { get; } = new();

    public Func<ShareX.Avalonia.Core.Hotkeys.HotkeySettings, Task<bool>>? EditHotkeyRequester { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    private HotkeyItemViewModel? _selectedHotkey;

    private Core.Hotkeys.HotkeyManager? _manager;

    public HotkeySettingsViewModel()
    {
        if (global::Avalonia.Application.Current is App app)
        {
            _manager = app.HotkeyManager;
        }

        LoadHotkeys();
    }

    private void LoadHotkeys()
    {
        Hotkeys.Clear();
        if (_manager != null)
        {
            foreach (var hk in _manager.Hotkeys)
            {
                Hotkeys.Add(new HotkeyItemViewModel(hk));
            }
        }
    }

    [RelayCommand]
    private void Add()
    {
        if (_manager == null) return;
        
        // Create new hotkey with default settings
        var newHotkey = new ShareX.Avalonia.Core.Hotkeys.HotkeySettings();
        
        // Add to list (user will configure inline via HotkeySelectionControl)
        _manager.Hotkeys.Add(newHotkey);
        
        LoadHotkeys();
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private void Remove()
    {
        if (SelectedHotkey != null && _manager != null)
        {
            _manager.UnregisterHotkey(SelectedHotkey.Model);
            // Also remove from manager's list if separate
            _manager.Hotkeys.Remove(SelectedHotkey.Model);
            LoadHotkeys();
            SelectedHotkey = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private async Task Edit()
    {
        if (SelectedHotkey != null && EditHotkeyRequester != null)
        {
            // Unregister before editing to avoid conflicts if key changes
            bool wasEnabled = SelectedHotkey.Model.Enabled;
            // logic to edit
            // For now passing model directly
            if (await EditHotkeyRequester(SelectedHotkey.Model))
            {
                // Re-register
                if (_manager != null)
                {
                    _manager.RegisterHotkey(SelectedHotkey.Model);
                }
                SelectedHotkey.Refresh();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private void Duplicate()
    {
        if (SelectedHotkey != null && _manager != null)
        {
            // Shallow copy for now, deep would be better
            var clone = new ShareX.Avalonia.Core.Hotkeys.HotkeySettings(SelectedHotkey.Model.Job, 
                new Platform.Abstractions.HotkeyInfo(
                    SelectedHotkey.Model.HotkeyInfo.Key, 
                    SelectedHotkey.Model.HotkeyInfo.Modifiers));
            
            // Just add to list, user needs to change key
            _manager.Hotkeys.Add(clone);
            LoadHotkeys();
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private void MoveUp()
    {
        // Not strictly necessary for functionality unless order matters for priority
         if (_manager == null || SelectedHotkey == null) return;
         int index = _manager.Hotkeys.IndexOf(SelectedHotkey.Model);
         if (index > 0)
         {
             _manager.Hotkeys.RemoveAt(index);
             _manager.Hotkeys.Insert(index - 1, SelectedHotkey.Model);
             LoadHotkeys();
             SelectedHotkey = Hotkeys[index - 1];
         }
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private void MoveDown()
    {
         if (_manager == null || SelectedHotkey == null) return;
         int index = _manager.Hotkeys.IndexOf(SelectedHotkey.Model);
         if (index < _manager.Hotkeys.Count - 1)
         {
             _manager.Hotkeys.RemoveAt(index);
             _manager.Hotkeys.Insert(index + 1, SelectedHotkey.Model);
             LoadHotkeys();
             SelectedHotkey = Hotkeys[index + 1];
         }
    }
    
    [RelayCommand]
    private void Reset()
    {
        if (_manager != null)
        {
            var defaults = Core.Hotkeys.HotkeyManager.GetDefaultHotkeyList();
            _manager.UpdateHotkeys(defaults);
            LoadHotkeys();
        }
    }

    private bool CanModifyHotkey() => SelectedHotkey != null;
}
