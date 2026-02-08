#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using System;
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
    [NotifyCanExecuteChangedFor(nameof(TogglePinCommand))]
    [NotifyPropertyChangedFor(nameof(PinButtonText))]
    private HotkeyItemViewModel? _selectedWorkflow;





    private XerahS.Core.Hotkeys.WorkflowManager? _manager;

    /// <summary>
    /// Delegate to request editing a hotkey. Set by the View.
    /// </summary>
    public Func<WorkflowSettings, Task<bool>>? EditHotkeyRequester { get; set; }

    public WorkflowsViewModel()
    {
        DebugHelper.WriteLine("[WorkflowsVM] ctor start");
        if (global::Avalonia.Application.Current is App app)
        {
            _manager = app.WorkflowManager;
        }

        LoadWorkflows();
        DebugHelper.WriteLine($"[WorkflowsVM] ctor end. Workflows={Workflows.Count}");
    }

    private void LoadWorkflows()
    {
        DebugHelper.WriteLine("[WorkflowsVM] LoadWorkflows start");
        Workflows.Clear();

        IEnumerable<WorkflowSettings> source;
        if (_manager != null)
        {
            source = _manager.Workflows;
        }
        else
        {
            source = SettingsManager.WorkflowsConfig.Hotkeys;
        }

        // No sorting - use the stored order
        int index = 0;
        foreach (var hk in source)
        {
            if (hk.Job == WorkflowType.None)
            {
                continue;
            }

            var vm = new HotkeyItemViewModel(hk);
            // Highlight top 3 (0, 1, 2)
            vm.IsNavWorkflow = index < 3;
            Workflows.Add(vm);
            index++;
        }

        DebugHelper.WriteLine($"[WorkflowsVM] LoadWorkflows end. Count={Workflows.Count}");
    }

    private void SaveHotkeys()
    {
        if (_manager != null)
        {
            SettingsManager.WorkflowsConfig.Hotkeys = _manager.Workflows;
        }
        SettingsManager.SaveWorkflowsConfig();
    }

    [RelayCommand]
    private async Task AddWorkflow()
    {
        // Create new blank workflow with defaults
        var newSettings = new XerahS.Core.Hotkeys.WorkflowSettings();
        // Maybe default job?
        newSettings.Job = XerahS.Core.WorkflowType.RectangleRegion;
        newSettings.TaskSettings = new TaskSettings();

        // Ensure the new workflow has an ID
        newSettings.EnsureId();

        if (EditHotkeyRequester != null)
        {
            var saved = await EditHotkeyRequester(newSettings);
            if (saved)
            {
                if (newSettings.Job != WorkflowType.None)
                {
                    if (_manager != null)
                    {
                        _manager.Workflows.Add(newSettings);
                        _manager.RegisterHotkey(newSettings);
                    }
                    else
                    {
                        SettingsManager.WorkflowsConfig.Hotkeys.Add(newSettings);
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
        DebugHelper.WriteLine($"[WorkflowsVM] EditWorkflow invoked. Selected={(SelectedWorkflow != null ? SelectedWorkflow.Model.Id : "null")}, CanEdit={CanEditWorkflow()}, HasRequester={EditHotkeyRequester != null}");
        if (SelectedWorkflow != null && EditHotkeyRequester != null)
        {
            var changed = await EditHotkeyRequester(SelectedWorkflow.Model);
            if (changed)
            {
                SaveHotkeys();
                // Refresh specific item or reload all?
                // Reloading ensures displayed description updates if Hotkey/Job changed
                LoadWorkflows();
                _manager?.NotifyWorkflowsChanged();
                // Restore selection?
                // For now, reload clears selection, but cleaner UI
            }
        }
    }

    private bool CanEditWorkflow() => SelectedWorkflow != null;

    partial void OnSelectedWorkflowChanged(HotkeyItemViewModel? value)
    {
        DebugHelper.WriteLine($"[WorkflowsVM] SelectedWorkflow changed: {(value != null ? value.Model.Id : "null")}");
    }

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
            SettingsManager.WorkflowsConfig.Hotkeys.Remove(SelectedWorkflow.Model);
            Workflows.Remove(SelectedWorkflow);
            SaveHotkeys();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditWorkflow))]
    private void Duplicate()
    {
        if (SelectedWorkflow != null && _manager != null)
        {
            var cloneJob = SelectedWorkflow.Model.Job == WorkflowType.None
                ? WorkflowType.RectangleRegion
                : SelectedWorkflow.Model.Job;
            var clone = new XerahS.Core.Hotkeys.WorkflowSettings(cloneJob,
                new HotkeyInfo(
                    SelectedWorkflow.Model.HotkeyInfo.Key,
                    SelectedWorkflow.Model.HotkeyInfo.Modifiers));

            // Deep copy TaskSettings using JSON
            var jsonSettings = new Newtonsoft.Json.JsonSerializerSettings
            {
                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto,
                ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace,
                Converters = new List<Newtonsoft.Json.JsonConverter>
                {
                    new Newtonsoft.Json.Converters.StringEnumConverter(),
                    new XerahS.Common.Converters.SkColorJsonConverter()
                }
            };
            var effectCount = SelectedWorkflow.Model.TaskSettings?.ImageSettings?.ImageEffectsPreset?.Effects?.Count ?? 0;
            var presetName = SelectedWorkflow.Model.TaskSettings?.ImageSettings?.ImageEffectsPreset?.Name ?? "(null)";
            Console.WriteLine($"[Workflows] Duplicate workflow. Preset='{presetName}', Effects={effectCount}");
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(SelectedWorkflow.Model.TaskSettings, jsonSettings);
            clone.TaskSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<TaskSettings>(json, jsonSettings) ?? new TaskSettings();

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

        var hotkeys = _manager?.Workflows ?? SettingsManager.WorkflowsConfig.Hotkeys;
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

        var hotkeys = _manager?.Workflows ?? SettingsManager.WorkflowsConfig.Hotkeys;
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

    [RelayCommand(CanExecute = nameof(CanTogglePin))]
    private void TogglePin()
    {
        if (SelectedWorkflow != null && !SelectedWorkflow.IsNavWorkflow)
        {
            SelectedWorkflow.PinnedToTray = !SelectedWorkflow.PinnedToTray;
            SaveHotkeys();
            TrayIconHelper.Instance.RefreshFromSettings();
            OnPropertyChanged(nameof(PinButtonText));
        }
    }

    private bool CanTogglePin()
    {
        return SelectedWorkflow != null && !SelectedWorkflow.IsNavWorkflow;
    }

    public string PinButtonText => SelectedWorkflow?.PinnedToTray == true ? "Unpin from Tray" : "Pin to Tray";
}
