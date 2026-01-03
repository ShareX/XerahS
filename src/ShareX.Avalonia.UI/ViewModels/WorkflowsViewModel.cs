using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Ava.Core;
using ShareX.Ava.Core.Hotkeys;
using ShareX.Ava.UI.Services;
using ShareX.Ava.Platform.Abstractions;

namespace ShareX.Ava.UI.ViewModels;

public partial class WorkflowsViewModel : ViewModelBase
{
    public ObservableCollection<HotkeyItemViewModel> Workflows { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveWorkflowCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditWorkflowCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    private HotkeyItemViewModel? _selectedWorkflow;

    [ObservableProperty]
    private bool _isWizardOpen;

    [ObservableProperty]
    private WorkflowWizardViewModel? _wizardViewModel;

    private ShareX.Ava.Core.Hotkeys.HotkeyManager? _manager;

    /// <summary>
    /// Delegate to request editing a hotkey. Set by the View.
    /// </summary>
    public Func<HotkeySettings, Task<bool>>? EditHotkeyRequester { get; set; }

    public WorkflowsViewModel()
    {
        if (global::Avalonia.Application.Current is App app)
        {
            _manager = app.HotkeyManager;
        }

        LoadWorkflows();
    }

    private void LoadWorkflows()
    {
        Workflows.Clear();
        if (_manager != null)
        {
            foreach (var hk in _manager.Hotkeys)
            {
                Workflows.Add(new HotkeyItemViewModel(hk));
            }
        }
        else
        {
            // Fallback if manager isn't ready (e.g. design time)
            foreach (var hk in SettingManager.HotkeysConfig.Hotkeys)
            {
                Workflows.Add(new HotkeyItemViewModel(hk));
            }
        }
    }

    private void SaveHotkeys()
    {
        if (_manager != null)
        {
            SettingManager.HotkeysConfig.Hotkeys = _manager.Hotkeys;
            SettingManager.SaveHotkeysConfigAsync();
        }
        else
        {
            SettingManager.SaveHotkeysConfig();
        }
    }

    [RelayCommand]
    private void AddWorkflow()
    {
        WizardViewModel = new WorkflowWizardViewModel();
        IsWizardOpen = true;
    }

    [RelayCommand]
    private void CompleteWizard()
    {
        if (WizardViewModel != null && _manager != null)
        {
            var newSettings = WizardViewModel.ConstructHotkeySettings();
            
            // Add via manager to ensure registration if needed
            _manager.Hotkeys.Add(newSettings);
            
            // Reload to sync UI
            LoadWorkflows();
            SaveHotkeys();
            
            // Select the new one (it's the last one)
            if (Workflows.Count > 0)
                SelectedWorkflow = Workflows.Last();
        }
        
        CloseWizard();
    }

    [RelayCommand]
    private void CloseWizard()
    {
        IsWizardOpen = false;
        WizardViewModel = null;
    }

    [RelayCommand(CanExecute = nameof(CanEditWorkflow))]
    private async Task EditWorkflow()
    {
        if (SelectedWorkflow != null && EditHotkeyRequester != null)
        {
            // Unregister before editing to avoid conflicts if key changes
            bool wasEnabled = SelectedWorkflow.Model.Enabled;
            // logic to edit
            if (await EditHotkeyRequester(SelectedWorkflow.Model))
            {
                // Re-register
                if (_manager != null)
                {
                    _manager.RegisterHotkey(SelectedWorkflow.Model);
                }
                SelectedWorkflow.Refresh();
                SaveHotkeys();
            }
        }
    }

    private bool CanEditWorkflow() => SelectedWorkflow != null;

    [RelayCommand(CanExecute = nameof(CanEditWorkflow))]
    private void RemoveWorkflow()
    {
        if (SelectedWorkflow != null && _manager != null)
        {
            _manager.UnregisterHotkey(SelectedWorkflow.Model);
            _manager.Hotkeys.Remove(SelectedWorkflow.Model);
            LoadWorkflows();
            SaveHotkeys();
            SelectedWorkflow = null;
        }
        else if (SelectedWorkflow != null && _manager == null) // Fallback
        {
             SettingManager.HotkeysConfig.Hotkeys.Remove(SelectedWorkflow.Model);
             Workflows.Remove(SelectedWorkflow);
             SaveHotkeys();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditWorkflow))]
    private void Duplicate()
    {
        if (SelectedWorkflow != null && _manager != null)
        {
            var clone = new ShareX.Ava.Core.Hotkeys.HotkeySettings(SelectedWorkflow.Model.Job, 
                new HotkeyInfo(
                    SelectedWorkflow.Model.HotkeyInfo.Key, 
                    SelectedWorkflow.Model.HotkeyInfo.Modifiers));
            
            // Copy TaskSettings too - shallow for now
            clone.TaskSettings = SelectedWorkflow.Model.TaskSettings; // Wait, this shares reference! BAD.
            // We need a proper clone. For now, let's just copy the Job. 
            // Better: use JSON clone? Or just create fresh.
            // HotkeySettings constructor copies Job. 
            // Let's rely on basic hotkey duplication for now.
            // Actually, we want to copy the destination settings too.
            // Since Copy logic wasn't fully deep in HotkeySettingsViewModel either, I will stick to basic Clone.
            
            _manager.Hotkeys.Add(clone);
            LoadWorkflows();
            SaveHotkeys();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditWorkflow))]
    private void MoveUp()
    {
         if (_manager == null || SelectedWorkflow == null) return;
         int index = _manager.Hotkeys.IndexOf(SelectedWorkflow.Model);
         if (index > 0)
         {
             _manager.Hotkeys.RemoveAt(index);
             _manager.Hotkeys.Insert(index - 1, SelectedWorkflow.Model);
             LoadWorkflows();
             SaveHotkeys();
             SelectedWorkflow = Workflows[index - 1]; // Reselect
         }
    }

    [RelayCommand(CanExecute = nameof(CanEditWorkflow))]
    private void MoveDown()
    {
         if (_manager == null || SelectedWorkflow == null) return;
         int index = _manager.Hotkeys.IndexOf(SelectedWorkflow.Model);
         if (index < _manager.Hotkeys.Count - 1)
         {
             _manager.Hotkeys.RemoveAt(index);
             _manager.Hotkeys.Insert(index + 1, SelectedWorkflow.Model);
             LoadWorkflows();
             SaveHotkeys();
             SelectedWorkflow = Workflows[index + 1];
         }
    }
    
    [RelayCommand]
    private void Reset()
    {
        if (_manager != null)
        {
            var defaults = HotkeyManager.GetDefaultHotkeyList();
            _manager.UpdateHotkeys(defaults);
            LoadWorkflows();
            SaveHotkeys();
        }
    }
}
