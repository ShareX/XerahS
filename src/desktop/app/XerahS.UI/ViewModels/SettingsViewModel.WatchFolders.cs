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

using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Hotkeys;

namespace XerahS.UI.ViewModels
{
    public partial class SettingsViewModel
    {
        [ObservableProperty]
        private bool _watchFolderEnabled;

        public ObservableCollection<WatchFolderSettingsViewModel> WatchFolders { get; } = new();

        public ObservableCollection<WorkflowOptionViewModel> WatchFolderWorkflows { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(EditWatchFolderCommand))]
        [NotifyCanExecuteChangedFor(nameof(RemoveWatchFolderCommand))]
        private WatchFolderSettingsViewModel? _selectedWatchFolder;

        [ObservableProperty]
        private bool _hasWatchFolders;

        partial void OnWatchFolderEnabledChanged(bool value)
        {
            if (_isLoading)
            {
                return;
            }

            RefreshWatchFolderStatuses();
        }

        [RelayCommand]
        private async Task AddWatchFolder()
        {
            if (EditWatchFolderRequester == null)
            {
                return;
            }

            var editVm = new WatchFolderEditViewModel
            {
                Title = "Add Watch Folder",
                Filter = "*.*"
            };
            PopulateEditViewModel(editVm, null);

            var saved = await EditWatchFolderRequester(editVm);
            if (!saved)
            {
                return;
            }

            var item = new WatchFolderSettingsViewModel
            {
                FolderPath = editVm.FolderPath,
                Filter = string.IsNullOrWhiteSpace(editVm.Filter) ? "*.*" : editVm.Filter,
                IncludeSubdirectories = editVm.IncludeSubdirectories,
                MoveFilesToScreenshotsFolder = editVm.MoveFilesToScreenshotsFolder,
                ConvertMovToMp4BeforeProcessing = editVm.ConvertMovToMp4BeforeProcessing,
                Enabled = editVm.Enabled,
                WorkflowId = editVm.SelectedWorkflowId,
                WorkflowName = editVm.SelectedWorkflow?.Name ?? "Unassigned"
            };

            WatchFolders.Add(item);
            AttachWatchFolder(item);
            RefreshWatchFolderStatuses();
            SaveSettings();
        }

        [RelayCommand(CanExecute = nameof(CanEditWatchFolder))]
        private async Task EditWatchFolder()
        {
            if (SelectedWatchFolder == null || EditWatchFolderRequester == null)
            {
                return;
            }

            var editVm = new WatchFolderEditViewModel
            {
                Title = "Edit Watch Folder",
                FolderPath = SelectedWatchFolder.FolderPath,
                Filter = SelectedWatchFolder.Filter,
                IncludeSubdirectories = SelectedWatchFolder.IncludeSubdirectories,
                MoveFilesToScreenshotsFolder = SelectedWatchFolder.MoveFilesToScreenshotsFolder,
                ConvertMovToMp4BeforeProcessing = SelectedWatchFolder.ConvertMovToMp4BeforeProcessing,
                Enabled = SelectedWatchFolder.Enabled
            };
            PopulateEditViewModel(editVm, SelectedWatchFolder.WorkflowId);

            var saved = await EditWatchFolderRequester(editVm);
            if (!saved)
            {
                return;
            }

            _suspendWatchFolderAutoSave = true;
            try
            {
                SelectedWatchFolder.FolderPath = editVm.FolderPath;
                SelectedWatchFolder.Filter = string.IsNullOrWhiteSpace(editVm.Filter) ? "*.*" : editVm.Filter;
                SelectedWatchFolder.IncludeSubdirectories = editVm.IncludeSubdirectories;
                SelectedWatchFolder.MoveFilesToScreenshotsFolder = editVm.MoveFilesToScreenshotsFolder;
                SelectedWatchFolder.ConvertMovToMp4BeforeProcessing = editVm.ConvertMovToMp4BeforeProcessing;
                SelectedWatchFolder.Enabled = editVm.Enabled;
                SelectedWatchFolder.WorkflowId = editVm.SelectedWorkflowId;
                SelectedWatchFolder.WorkflowName = editVm.SelectedWorkflow?.Name ?? "Unassigned";
            }
            finally
            {
                _suspendWatchFolderAutoSave = false;
            }

            RefreshWatchFolderStatuses();
            SaveSettings();
        }

        [RelayCommand(CanExecute = nameof(CanEditWatchFolder))]
        private void RemoveWatchFolder()
        {
            if (SelectedWatchFolder == null)
            {
                return;
            }

            WatchFolders.Remove(SelectedWatchFolder);
            SelectedWatchFolder = null;
            RefreshWatchFolderStatuses();
            SaveSettings();
        }

        private bool CanEditWatchFolder()
        {
            return SelectedWatchFolder != null;
        }

        private void RefreshWatchFolderStatuses()
        {
            var workflows = SettingsManager.WorkflowsConfig?.Hotkeys;
            foreach (var folder in WatchFolders)
            {
                bool workflowValid = workflows?.Any(w => w.Id == folder.WorkflowId) == true;
                folder.UpdateStatus(WatchFolderEnabled, workflowValid);
            }
        }

        private void AttachWatchFolder(WatchFolderSettingsViewModel folder)
        {
            folder.PropertyChanged += (_, e) =>
            {
                if (_suspendWatchFolderAutoSave)
                {
                    return;
                }

                if (e.PropertyName == nameof(WatchFolderSettingsViewModel.FolderPath) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.Filter) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.IncludeSubdirectories) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.MoveFilesToScreenshotsFolder) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.ConvertMovToMp4BeforeProcessing) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.WorkflowId) ||
                    e.PropertyName == nameof(WatchFolderSettingsViewModel.Enabled))
                {
                    RefreshWatchFolderStatuses();
                    SaveSettings();
                }
            };
        }

        private void LoadWatchFolderWorkflows()
        {
            var workflows = SettingsManager.WorkflowsConfig?.Hotkeys;
            if (workflows == null)
            {
                return;
            }

            foreach (var workflow in workflows.Where(w => w.Job != WorkflowType.None))
            {
                WatchFolderWorkflows.Add(new WorkflowOptionViewModel(workflow.Id, GetWorkflowName(workflow)));
            }
        }

        private void PopulateEditViewModel(WatchFolderEditViewModel editVm, string? workflowId)
        {
            foreach (var workflow in WatchFolderWorkflows)
            {
                editVm.Workflows.Add(workflow);
            }

            string? preferredWorkflowId = workflowId;
            if (string.IsNullOrWhiteSpace(preferredWorkflowId))
            {
                preferredWorkflowId = SettingsManager.WorkflowsConfig?.Hotkeys
                    ?.FirstOrDefault(w => w.Job == WorkflowType.FileUpload)
                    ?.Id;
            }

            editVm.SelectedWorkflow = editVm.Workflows.FirstOrDefault(w => w.Id == preferredWorkflowId)
                                      ?? editVm.Workflows.FirstOrDefault();
        }

        private static string GetWorkflowName(string workflowId)
        {
            var workflow = SettingsManager.GetWorkflowById(workflowId);
            return workflow != null ? GetWorkflowName(workflow) : "Unknown workflow";
        }

        private static string GetWorkflowName(WorkflowSettings workflow)
        {
            if (!string.IsNullOrEmpty(workflow.TaskSettings?.Description))
            {
                return workflow.TaskSettings.Description;
            }

            return EnumExtensions.GetDescription(workflow.Job);
        }
    }
}
