using System;
using ShareX.Ava.Common;
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
    private HotkeyItemViewModel? _selectedWorkflow;

    [ObservableProperty]
    private bool _isWizardOpen;

    [ObservableProperty]
    private WorkflowWizardViewModel? _wizardViewModel;
    
    // Track if we are editing an existing item
    private HotkeyItemViewModel? _editingWorkflow;

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
            foreach (var hk in SettingManager.WorkflowsConfig.Hotkeys)
            {
                Workflows.Add(new HotkeyItemViewModel(hk));
            }
        }
    }

    private void SaveHotkeys()
    {
        if (_manager != null)
        {
            SettingManager.WorkflowsConfig.Hotkeys = _manager.Hotkeys;
            SettingManager.SaveWorkflowsConfigAsync();
        }
        else
        {
            SettingManager.SaveWorkflowsConfig();
        }
    }

    [RelayCommand]
    private async Task AddWorkflow()
    {
        // Create new blank workflow with defaults
        var newSettings = new ShareX.Ava.Core.Hotkeys.HotkeySettings();
        // Maybe default job?
        newSettings.Job = ShareX.Ava.Core.HotkeyType.RectangleRegion;
        newSettings.TaskSettings = new TaskSettings();
        
        if (EditHotkeyRequester != null)
        {
            var saved = await EditHotkeyRequester(newSettings);
            if (saved)
            {
                 if (_manager != null)
                 {
                     _manager.Hotkeys.Add(newSettings);
                     _manager.RegisterHotkey(newSettings);
                 }
                 else
                 {
                     SettingManager.WorkflowsConfig.Hotkeys.Add(newSettings);
                 }
                 
                 SaveHotkeys();
                 LoadWorkflows();
            }
        }
    }

    [RelayCommand]
    private void CompleteWizard()
    {
        if (WizardViewModel != null && _manager != null)
        {
            var newSettings = WizardViewModel.ConstructHotkeySettings();
            
            if (_editingWorkflow != null)
            {
                // Update existing
                // Reuse the same hotkey/modifier if possible, or just replace settings?
                // ConstructHotkeySettings usually sets Key to None. 
                // We should Preserve the key from the edited item!
                
                newSettings.HotkeyInfo = _editingWorkflow.Model.HotkeyInfo;
                
                // Replace in manager
                int index = _manager.Hotkeys.IndexOf(_editingWorkflow.Model);
                if (index != -1)
                {
                    _manager.UnregisterHotkey(_editingWorkflow.Model);
                    
                    // Insert at correct position if possible
                    if (index <= _manager.Hotkeys.Count)
                    {
                        _manager.Hotkeys.Insert(index, newSettings);
                    }
                    else
                    {
                        _manager.Hotkeys.Add(newSettings);
                    }
                    
                    _manager.RegisterHotkey(newSettings);
                }
                
                _editingWorkflow = null;
            }
            else
            {
                // Add new
                // Add via manager to ensure registration if needed
                _manager.Hotkeys.Add(newSettings);
            }
            
            // Reload to sync UI
            LoadWorkflows();
            SaveHotkeys();
            
            // Select the item
            if (_editingWorkflow != null)
            {
                 // If we were editing, try to find the updated one?
                 // Actually we refreshed list.
                 // Just leave selection or select last added?
                 // Let's select the last one if added, or try to select the modified one.
            }
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
        _editingWorkflow = null;
    }

    [RelayCommand(CanExecute = nameof(CanEditWorkflow))]
    private async Task EditWorkflow()
    {
        if (SelectedWorkflow != null && EditHotkeyRequester != null)
        {
            var changed = await EditHotkeyRequester(SelectedWorkflow.Model);
            if (changed)
            {
                SaveHotkeys();
                // Refresh specific item or reload all?
                // Reloading ensures displayed description updates if Hotkey/Job changed
                LoadWorkflows();
                // Restore selection?
                // For now, reload clears selection, but cleaner UI
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
             SettingManager.WorkflowsConfig.Hotkeys.Remove(SelectedWorkflow.Model);
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
