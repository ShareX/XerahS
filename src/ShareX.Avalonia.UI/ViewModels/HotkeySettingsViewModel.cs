using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Core;
using System.Collections.ObjectModel;

namespace XerahS.UI.ViewModels;

public partial class HotkeySettingsViewModel : ViewModelBase
{
    public ObservableCollection<HotkeyItemViewModel> Hotkeys { get; } = new();

    public Func<XerahS.Core.Hotkeys.WorkflowSettings, Task<bool>>? EditHotkeyRequester { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    private HotkeyItemViewModel? _selectedHotkey;

    private Core.Hotkeys.WorkflowManager? _manager;

    public HotkeySettingsViewModel()
    {
        if (global::Avalonia.Application.Current is App app)
        {
            _manager = app.WorkflowManager;
        }

        LoadHotkeys();
    }

    private void LoadHotkeys()
    {
        System.Diagnostics.Debug.WriteLine($"[HotkeySettings] LoadHotkeys called, _manager={_manager != null}");
        Hotkeys.Clear();
        if (_manager != null)
        {
            System.Diagnostics.Debug.WriteLine($"[HotkeySettings] Manager has {_manager.Workflows.Count} hotkeys");
            foreach (var hk in _manager.Workflows)
            {
                System.Diagnostics.Debug.WriteLine($"[HotkeySettings] Adding hotkey: {hk.Job} - {hk.HotkeyInfo}");
                Hotkeys.Add(new HotkeyItemViewModel(hk));
            }
            System.Diagnostics.Debug.WriteLine($"[HotkeySettings] Hotkeys collection now has {Hotkeys.Count} items");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[HotkeySettings] WARNING: Manager is NULL");
        }
    }

    /// <summary>
    /// Sync manager's hotkeys to config and save to disk
    /// </summary>
    private void SaveHotkeys()
    {
        if (_manager != null)
        {
            SettingManager.WorkflowsConfig.Hotkeys = _manager.Workflows;
            // Save to disk
            SettingManager.SaveWorkflowsConfigAsync();
        }
    }

    [RelayCommand]
    private void Add()
    {
        if (_manager == null) return;

        // Create new hotkey with default settings
        var newHotkey = new XerahS.Core.Hotkeys.WorkflowSettings();

        // Add to list (user will configure inline via HotkeySelectionControl)
        _manager.Workflows.Add(newHotkey);

        LoadHotkeys();
        SaveHotkeys();
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private void Remove()
    {
        if (SelectedHotkey != null && _manager != null)
        {
            _manager.UnregisterHotkey(SelectedHotkey.Model);
            // Also remove from manager's list if separate
            _manager.Workflows.Remove(SelectedHotkey.Model);
            LoadHotkeys();
            SaveHotkeys();
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
                SaveHotkeys();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private void Duplicate()
    {
        if (SelectedHotkey != null && _manager != null)
        {
            // Shallow copy for now, deep would be better
            var clone = new XerahS.Core.Hotkeys.WorkflowSettings(SelectedHotkey.Model.Job,
                new Platform.Abstractions.HotkeyInfo(
                    SelectedHotkey.Model.HotkeyInfo.Key,
                    SelectedHotkey.Model.HotkeyInfo.Modifiers));

            // Just add to list, user needs to change key
            _manager.Workflows.Add(clone);
            LoadHotkeys();
            SaveHotkeys();
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private void MoveUp()
    {
        // Not strictly necessary for functionality unless order matters for priority
        if (_manager == null || SelectedHotkey == null) return;
        int index = _manager.Workflows.IndexOf(SelectedHotkey.Model);
        if (index > 0)
        {
            _manager.Workflows.RemoveAt(index);
            _manager.Workflows.Insert(index - 1, SelectedHotkey.Model);
            LoadHotkeys();
            SaveHotkeys();
            SelectedHotkey = Hotkeys[index - 1];
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyHotkey))]
    private void MoveDown()
    {
        if (_manager == null || SelectedHotkey == null) return;
        int index = _manager.Workflows.IndexOf(SelectedHotkey.Model);
        if (index < _manager.Workflows.Count - 1)
        {
            _manager.Workflows.RemoveAt(index);
            _manager.Workflows.Insert(index + 1, SelectedHotkey.Model);
            LoadHotkeys();
            SaveHotkeys();
            SelectedHotkey = Hotkeys[index + 1];
        }
    }

    [RelayCommand]
    private void Reset()
    {
        if (_manager != null)
        {
            var defaults = Core.Hotkeys.WorkflowManager.GetDefaultWorkflowList();
            _manager.UpdateHotkeys(defaults);
            LoadHotkeys();
            SaveHotkeys();
        }
    }

    private bool CanModifyHotkey() => SelectedHotkey != null;
}
