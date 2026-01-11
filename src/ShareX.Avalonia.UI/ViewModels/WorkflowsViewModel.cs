using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using System.Collections.ObjectModel;

namespace XerahS.UI.ViewModels;

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





    private XerahS.Core.Hotkeys.WorkflowManager? _manager;

    /// <summary>
    /// Delegate to request editing a hotkey. Set by the View.
    /// </summary>
    public Func<WorkflowSettings, Task<bool>>? EditHotkeyRequester { get; set; }

    public WorkflowsViewModel()
    {
        if (global::Avalonia.Application.Current is App app)
        {
            _manager = app.WorkflowManager;
        }

        LoadWorkflows();
    }

    private void LoadWorkflows()
    {
        Workflows.Clear();

        IEnumerable<WorkflowSettings> source;
        if (_manager != null)
        {
            source = _manager.Workflows;
        }
        else
        {
            source = SettingManager.WorkflowsConfig.Hotkeys;
        }

        // No sorting - use the stored order
        int index = 0;
        foreach (var hk in source)
        {
            if (hk.Job == HotkeyType.None)
            {
                continue;
            }

            var vm = new HotkeyItemViewModel(hk);
            // Highlight top 3 (0, 1, 2)
            vm.IsNavWorkflow = index < 3;
            Workflows.Add(vm);
            index++;
        }
    }

    private void SaveHotkeys()
    {
        if (_manager != null)
        {
            SettingManager.WorkflowsConfig.Hotkeys = _manager.Workflows;
        }
        SettingManager.SaveWorkflowsConfig();
    }

    [RelayCommand]
    private async Task AddWorkflow()
    {
        // Create new blank workflow with defaults
        var newSettings = new XerahS.Core.Hotkeys.WorkflowSettings();
        // Maybe default job?
        newSettings.Job = XerahS.Core.HotkeyType.RectangleRegion;
        newSettings.TaskSettings = new TaskSettings();

        // Ensure the new workflow has an ID
        newSettings.EnsureId();

        if (EditHotkeyRequester != null)
        {
            var saved = await EditHotkeyRequester(newSettings);
            if (saved)
            {
                if (newSettings.Job != HotkeyType.None)
                {
                    if (_manager != null)
                    {
                        _manager.Workflows.Add(newSettings);
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
            _manager.Workflows.Remove(SelectedWorkflow.Model);
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
            var clone = new XerahS.Core.Hotkeys.WorkflowSettings(SelectedWorkflow.Model.Job,
                new HotkeyInfo(
                    SelectedWorkflow.Model.HotkeyInfo.Key,
                    SelectedWorkflow.Model.HotkeyInfo.Modifiers));

            // Deep copy TaskSettings using JSON
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(SelectedWorkflow.Model.TaskSettings);
            clone.TaskSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<TaskSettings>(json);

            // Copy additional properties
            clone.Name = SelectedWorkflow.Model.Name;
            clone.Enabled = SelectedWorkflow.Model.Enabled;

            // Note: Clone will already have a new unique ID generated by the constructor
            // since we used the parameterized constructor

            _manager.Workflows.Add(clone);
            LoadWorkflows();
            SaveHotkeys();
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedWorkflow == null) return;

        var index = Workflows.IndexOf(SelectedWorkflow);
        if (index <= 0) return;

        var hotkeys = _manager?.Workflows ?? SettingManager.WorkflowsConfig.Hotkeys;
        var model = SelectedWorkflow.Model;
        var modelIndex = hotkeys.IndexOf(model);

        if (modelIndex > 0)
        {
            if (_manager != null)
            {
                _manager.MoveWorkflow(modelIndex, modelIndex - 1);
            }
            else
            {
                hotkeys.RemoveAt(modelIndex);
                hotkeys.Insert(modelIndex - 1, model);
            }
            
            SaveHotkeys();
            LoadWorkflows();
            SelectedWorkflow = Workflows.FirstOrDefault(w => w.Model == model);
        }
    }

    private bool CanMoveUp()
    {
        if (SelectedWorkflow == null) return false;
        return Workflows.IndexOf(SelectedWorkflow) > 0;
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedWorkflow == null) return;

        var index = Workflows.IndexOf(SelectedWorkflow);
        if (index < 0 || index >= Workflows.Count - 1) return;

        var hotkeys = _manager?.Workflows ?? SettingManager.WorkflowsConfig.Hotkeys;
        var model = SelectedWorkflow.Model;
        var modelIndex = hotkeys.IndexOf(model);

        if (modelIndex >= 0 && modelIndex < hotkeys.Count - 1)
        {
            if (_manager != null)
            {
                _manager.MoveWorkflow(modelIndex, modelIndex + 1);
            }
            else
            {
                hotkeys.RemoveAt(modelIndex);
                hotkeys.Insert(modelIndex + 1, model);
            }

            SaveHotkeys();
            LoadWorkflows();
            SelectedWorkflow = Workflows.FirstOrDefault(w => w.Model == model);
        }
    }

    private bool CanMoveDown()
    {
        if (SelectedWorkflow == null) return false;
        return Workflows.IndexOf(SelectedWorkflow) < Workflows.Count - 1;
    }



    /// <summary>
    /// Delegate to request confirmation from the UI.
    /// Arguments: Title, Message
    /// Returns: True if confirmed, False otherwise
    /// </summary>
    public Func<string, string, Task<bool>>? ConfirmByUi { get; set; }

    [RelayCommand]
    private async Task Reset()
    {
        if (ConfirmByUi != null)
        {
            var confirmed = await ConfirmByUi("Reset Workflows", "Are you sure you want to reset all workflows to default settings? This cannot be undone.");
            if (!confirmed)
            {
                return;
            }
        }

        if (_manager != null)
        {
            var defaults = WorkflowManager.GetDefaultWorkflowList();
            _manager.UpdateHotkeys(defaults);
            LoadWorkflows();
            SaveHotkeys();
        }
    }
}
