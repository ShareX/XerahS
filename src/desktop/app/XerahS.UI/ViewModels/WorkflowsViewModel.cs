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
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;
using XerahS.UI.Services;

namespace XerahS.UI.ViewModels;

public partial class WorkflowsViewModel : ViewModelBase, IDisposable
{
    public ObservableCollection<HotkeyItemViewModel> Workflows { get; } = new();
    private readonly IUiViewModelFactory _uiViewModelFactory;
    private readonly IDialogService _coreDialogService;
    private readonly WorkflowManager? _manager;
    private readonly Func<bool> _persist;
    private readonly DispatcherTimer _saveTimer;
    private bool _suppressManagerChanges;
    private bool _reloading;
    private bool _pendingChanges;
    private bool _disposed;
    private bool _active = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveWorkflowCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditWorkflowCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(TogglePinCommand))]
    [NotifyPropertyChangedFor(nameof(PinButtonText))]
    private HotkeyItemViewModel? _selectedWorkflow;

    [ObservableProperty]
    private WorkflowEditorViewModel? _selectedEditor;

    [ObservableProperty]
    private string _saveStatus = "Changes save automatically";

    [ObservableProperty]
    private bool _hasSaveError;

    public WorkflowsViewModel(IUiViewModelFactory factory)
        : this(factory, (Avalonia.Application.Current as App)?.WorkflowManager, PersistConfiguration) { }

    internal WorkflowsViewModel(IUiViewModelFactory factory, WorkflowManager? manager, Func<bool> persist)
    {
        _uiViewModelFactory = factory;
        _coreDialogService = factory.CoreDialogService;
        _manager = manager;
        _persist = persist;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _saveTimer.Tick += OnSaveTimer;
        if (_manager != null) _manager.WorkflowsChanged += OnManagerWorkflowsChanged;
        LoadWorkflows();
    }

    private List<WorkflowSettings> Models => _manager?.Workflows ?? SettingsManager.WorkflowsConfig.Hotkeys;

    private static bool PersistConfiguration()
    {
        bool saved = SettingsManager.WorkflowsConfig.Save(SettingsManager.WorkflowsConfigFilePath);
        if (saved) SettingsManager.RaiseSettingsChanged();
        return saved;
    }

    private void OnManagerWorkflowsChanged(object? sender, EventArgs e)
    {
        if (_suppressManagerChanges || _disposed || !_active) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || !_active) return;
            var current = Models.Where(w => w.Job != WorkflowType.None).ToList();
            if (current.SequenceEqual(Workflows.Select(w => w.Model)))
            {
                foreach (var workflow in Workflows) workflow.Refresh();
            }
            else LoadWorkflows();
        });
    }

    private void LoadWorkflows(string? selectId = null)
    {
        FlushAutoSave();
        selectId ??= SelectedWorkflow?.Model.Id;
        UnsubscribeEditor();
        _reloading = true;
        try
        {
            Workflows.Clear();
            foreach (var model in Models.Where(w => w.Job != WorkflowType.None))
                Workflows.Add(new HotkeyItemViewModel(model) { IsNavWorkflow = Workflows.Count < 3 });
            SelectedWorkflow = Workflows.FirstOrDefault(w => w.Model.Id == selectId) ?? Workflows.FirstOrDefault();
        }
        finally { _reloading = false; }
        SetEditor(SelectedWorkflow);
    }

    partial void OnSelectedWorkflowChanging(HotkeyItemViewModel? value)
    {
        if (!_reloading) FlushAutoSave();
    }

    partial void OnSelectedWorkflowChanged(HotkeyItemViewModel? value)
    {
        if (!_reloading) SetEditor(value);
    }

    private void SetEditor(HotkeyItemViewModel? workflow)
    {
        UnsubscribeEditor();
        var editor = workflow == null ? null : _uiViewModelFactory.CreateWorkflowEditorViewModel(workflow.Model);
        if (editor != null) editor.IsInline = true;
        SelectedEditor = editor;
        if (SelectedEditor == null) return;
        SubscribeEditor();
    }

    private void SubscribeEditor()
    {
        if (SelectedEditor == null || !_active) return;
        SelectedEditor.PropertyChanged += OnEditorChanged;
        SelectedEditor.TaskSettings.PropertyChanged += OnEditorChanged;
        SelectedEditor.IndexFolderConfig.PropertyChanged += OnEditorChanged;
        SelectedEditor.TaskSettings.ImageEffects.SettingsChanged += OnEffectSettingsChanged;
    }

    private void UnsubscribeEditor()
    {
        if (SelectedEditor == null) return;
        SelectedEditor.PropertyChanged -= OnEditorChanged;
        SelectedEditor.TaskSettings.PropertyChanged -= OnEditorChanged;
        SelectedEditor.IndexFolderConfig.PropertyChanged -= OnEditorChanged;
        SelectedEditor.TaskSettings.ImageEffects.SettingsChanged -= OnEffectSettingsChanged;
    }

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e) => ScheduleAutoSave();
    private void OnEffectSettingsChanged(object? sender, EventArgs e) => ScheduleAutoSave();

    private void ScheduleAutoSave()
    {
        if (_disposed || _reloading || SelectedEditor == null) return;
        _pendingChanges = true;
        HasSaveError = false;
        SaveStatus = "Saving changes…";
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnSaveTimer(object? sender, EventArgs e) => FlushAutoSave();

    public void FlushAutoSave()
    {
        _saveTimer.Stop();
        if (!_pendingChanges || SelectedEditor == null) return;
        try
        {
            _suppressManagerChanges = true;
            var model = SelectedWorkflow!.Model;
            var runtimeHotkey = model.HotkeyInfo;
            bool registrationChanged = model.Job != SelectedEditor.SelectedJob || model.Enabled != SelectedEditor.Model.Enabled ||
                runtimeHotkey.Key != SelectedEditor.SelectedKey || runtimeHotkey.Modifiers != SelectedEditor.SelectedModifiers;
            SelectedEditor.Save();
            if (registrationChanged) _manager?.RegisterHotkey(model);
            else model.HotkeyInfo = runtimeHotkey;
            SelectedEditor.Model.HotkeyInfo.Id = model.HotkeyInfo.Id;
            SelectedEditor.Model.HotkeyInfo.Status = model.HotkeyInfo.Status;
            SelectedEditor.Model.HotkeyInfo.NativeTriggerDescription = model.HotkeyInfo.NativeTriggerDescription;
            foreach (var workflow in Workflows) workflow.Refresh();
            SaveHotkeys();
            _pendingChanges = HasSaveError;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Could not save workflow changes");
            HasSaveError = true;
            SaveStatus = "Changes could not be saved. Retry.";
        }
        finally { _suppressManagerChanges = false; }
    }

    private void SaveHotkeys()
    {
        if (_manager != null) SettingsManager.WorkflowsConfig.Hotkeys = _manager.Workflows;
        try
        {
            HasSaveError = !_persist();
            SaveStatus = HasSaveError ? "Changes could not be saved. Retry." : "All changes saved";
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Could not save workflow configuration");
            HasSaveError = true;
            SaveStatus = "Changes could not be saved. Retry.";
        }
        bool wasSuppressing = _suppressManagerChanges;
        _suppressManagerChanges = true;
        try { _manager?.NotifyWorkflowsChanged(); }
        finally { _suppressManagerChanges = wasSuppressing; }
    }

    [RelayCommand]
    private void RetrySave()
    {
        if (_pendingChanges) FlushAutoSave();
        else SaveHotkeys();
    }

    [RelayCommand]
    private void AddWorkflow()
    {
        FlushAutoSave();
        var model = new WorkflowSettings(WorkflowType.RectangleRegion, new HotkeyInfo());
        model.TaskSettings.Description = "New workflow";
        model.EnsureId();
        Models.Add(model);
        SaveHotkeys();
        LoadWorkflows(model.Id);
    }

    [RelayCommand(CanExecute = nameof(CanEditWorkflow))]
    private void EditWorkflow() { }
    private bool CanEditWorkflow() => SelectedWorkflow != null;

    [RelayCommand(CanExecute = nameof(CanEditWorkflow))]
    private void RemoveWorkflow()
    {
        if (SelectedWorkflow == null) return;
        FlushAutoSave();
        var model = SelectedWorkflow.Model;
        int index = Workflows.IndexOf(SelectedWorkflow);
        _suppressManagerChanges = true;
        try
        {
            _manager?.UnregisterHotkey(model);
            Models.Remove(model);
            SaveHotkeys();
            LoadWorkflows(Workflows.ElementAtOrDefault(index + 1)?.Model.Id ?? Workflows.ElementAtOrDefault(index - 1)?.Model.Id);
        }
        finally { _suppressManagerChanges = false; }
    }

    [RelayCommand(CanExecute = nameof(CanEditWorkflow))]
    private void Duplicate()
    {
        if (SelectedWorkflow == null) return;
        FlushAutoSave();
        var clone = CreateWorkflowDuplicate(SelectedWorkflow.Model);
        Models.Add(clone);
        _suppressManagerChanges = true;
        try { _manager?.RegisterHotkey(clone); }
        finally { _suppressManagerChanges = false; }
        SaveHotkeys();
        LoadWorkflows(clone.Id);
    }

    internal static WorkflowSettings CreateWorkflowDuplicate(WorkflowSettings source)
    {
        var cloneJob = source.Job == WorkflowType.None
            ? WorkflowType.RectangleRegion
            : source.Job;
        var clone = new XerahS.Core.Hotkeys.WorkflowSettings(cloneJob,
            new HotkeyInfo(
                source.HotkeyInfo.Key,
                source.HotkeyInfo.Modifiers));

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
        var effectCount = source.TaskSettings?.ImageSettings?.ImageEffectsPreset?.Effects?.Count ?? 0;
        var presetName = source.TaskSettings?.ImageSettings?.ImageEffectsPreset?.Name ?? "(null)";
        Console.WriteLine($"[Workflows] Duplicate workflow. Preset='{presetName}', Effects={effectCount}");
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(source.TaskSettings, jsonSettings);
        clone.TaskSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<TaskSettings>(json, jsonSettings) ?? new TaskSettings();

        // Copy additional properties
        clone.Name = source.Name;
        clone.Enabled = source.Enabled;
        clone.PinnedToTray = source.PinnedToTray;

        // The deep-copied task settings still carry the source workflow ID.
        // Re-sync the cloned task settings to the new workflow identity.
        clone.EnsureId();

        return clone;
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => MoveSelected(-1);
    private bool CanMoveUp() => SelectedWorkflow != null && Workflows.IndexOf(SelectedWorkflow) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => MoveSelected(1);
    private bool CanMoveDown() => SelectedWorkflow != null && Workflows.IndexOf(SelectedWorkflow) < Workflows.Count - 1;

    private void MoveSelected(int offset)
    {
        if (SelectedWorkflow == null) return;
        FlushAutoSave();
        var model = SelectedWorkflow.Model;
        int index = Models.IndexOf(model);
        int target = index + offset;
        if (target < 0 || target >= Models.Count) return;
        _suppressManagerChanges = true;
        try
        {
            if (_manager != null) _manager.MoveWorkflow(index, target);
            else { Models.RemoveAt(index); Models.Insert(target, model); }
            SaveHotkeys();
            LoadWorkflows(model.Id);
        }
        finally { _suppressManagerChanges = false; }
    }

    [RelayCommand]
    private async Task Reset()
    {
        if (!await _coreDialogService.ShowConfirmationAsync("Reset Workflows", "Reset all workflows to default settings? This cannot be undone.")) return;
        FlushAutoSave();
        _suppressManagerChanges = true;
        try
        {
            var defaults = WorkflowManager.GetDefaultWorkflowList();
            if (_manager != null) _manager.UpdateHotkeys(defaults);
            else SettingsManager.WorkflowsConfig.Hotkeys = defaults;
            SaveHotkeys();
            LoadWorkflows();
        }
        finally { _suppressManagerChanges = false; }
    }

    [RelayCommand(CanExecute = nameof(CanTogglePin))]
    private void TogglePin()
    {
        if (SelectedWorkflow == null || SelectedWorkflow.IsNavWorkflow) return;
        FlushAutoSave();
        SelectedWorkflow.PinnedToTray = !SelectedWorkflow.PinnedToTray;
        if (SelectedEditor != null) SelectedEditor.Model.PinnedToTray = SelectedWorkflow.PinnedToTray;
        SaveHotkeys();
        OnPropertyChanged(nameof(PinButtonText));
    }

    private bool CanTogglePin() => SelectedWorkflow != null && !SelectedWorkflow.IsNavWorkflow;
    public string PinButtonText => SelectedWorkflow?.PinnedToTray == true ? "Unpin from Tray" : "Pin to Tray";

    public void Dispose()
    {
        if (_disposed) return;
        Deactivate();
        _disposed = true;
        _saveTimer.Tick -= OnSaveTimer;
    }

    public void Activate()
    {
        if (_disposed || _active) return;
        _active = true;
        if (_manager != null) _manager.WorkflowsChanged += OnManagerWorkflowsChanged;
        if (!Models.Where(w => w.Job != WorkflowType.None).SequenceEqual(Workflows.Select(w => w.Model))) LoadWorkflows();
        else SubscribeEditor();
    }

    public void Deactivate()
    {
        if (!_active) return;
        FlushAutoSave();
        _active = false;
        if (_manager != null) _manager.WorkflowsChanged -= OnManagerWorkflowsChanged;
        UnsubscribeEditor();
    }
}
