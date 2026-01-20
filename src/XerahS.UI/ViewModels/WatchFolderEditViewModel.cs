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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace XerahS.UI.ViewModels;

public partial class WatchFolderEditViewModel : ViewModelBase, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private string _title = "Add Watch Folder";

    [ObservableProperty]
    private string _folderPath = string.Empty;

    [ObservableProperty]
    private string _filter = "*.*";

    [ObservableProperty]
    private bool _includeSubdirectories;

    [ObservableProperty]
    private bool _moveFilesToScreenshotsFolder;

    [ObservableProperty]
    private string _folderPathError = string.Empty;

    [ObservableProperty]
    private string _filterError = string.Empty;

    [ObservableProperty]
    private string _workflowError = string.Empty;

    [ObservableProperty]
    private WorkflowOptionViewModel? _selectedWorkflow;

    public ObservableCollection<WorkflowOptionViewModel> Workflows { get; } = new();

    public string SelectedWorkflowId => SelectedWorkflow?.Id ?? string.Empty;

    public bool HasFolderPathError => !string.IsNullOrWhiteSpace(FolderPathError);

    public bool HasFilterError => !string.IsNullOrWhiteSpace(FilterError);

    public bool HasWorkflowError => !string.IsNullOrWhiteSpace(WorkflowError);

    public bool HasErrors => _errors.Count > 0;

    public bool CanSave => !HasErrors;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public Func<Task<string?>>? BrowseFolderRequester { get; set; }

    public Action<bool>? CloseRequested { get; set; }

    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return Array.Empty<string>();
        }

        return _errors.TryGetValue(propertyName, out var errors) ? errors : Array.Empty<string>();
    }

    partial void OnFolderPathChanged(string value)
    {
        ValidateFolderPath();
    }

    partial void OnFilterChanged(string value)
    {
        ValidateFilter();
    }

    partial void OnSelectedWorkflowChanged(WorkflowOptionViewModel? value)
    {
        ValidateWorkflow();
    }

    [RelayCommand]
    private async Task BrowseFolder()
    {
        if (BrowseFolderRequester == null)
        {
            return;
        }

        var path = await BrowseFolderRequester();
        if (!string.IsNullOrWhiteSpace(path))
        {
            FolderPath = path;
        }
    }

    [RelayCommand]
    private void Save()
    {
        ValidateAll();
        if (HasErrors)
        {
            return;
        }

        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }

    private void ValidateAll()
    {
        ValidateFolderPath();
        ValidateFilter();
        ValidateWorkflow();
    }

    private void ValidateFolderPath()
    {
        ClearErrors(nameof(FolderPath));
        FolderPathError = string.Empty;

        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            AddError(nameof(FolderPath), "Folder path is required.");
        }
        else if (!Directory.Exists(FolderPath))
        {
            AddError(nameof(FolderPath), "Folder does not exist.");
        }

        FolderPathError = GetFirstError(nameof(FolderPath));
        OnPropertyChanged(nameof(HasFolderPathError));
        OnPropertyChanged(nameof(CanSave));
    }

    private void ValidateFilter()
    {
        ClearErrors(nameof(Filter));
        FilterError = string.Empty;

        if (string.IsNullOrWhiteSpace(Filter))
        {
            AddError(nameof(Filter), "Filter is required.");
        }
        else
        {
            var invalidChars = Path.GetInvalidFileNameChars()
                .Where(c => c != '*' && c != '?')
                .ToArray();

            if (Filter.IndexOfAny(invalidChars) >= 0)
            {
                AddError(nameof(Filter), "Filter contains invalid characters.");
            }
        }

        FilterError = GetFirstError(nameof(Filter));
        OnPropertyChanged(nameof(HasFilterError));
        OnPropertyChanged(nameof(CanSave));
    }

    private void ValidateWorkflow()
    {
        ClearErrors(nameof(SelectedWorkflow));
        WorkflowError = string.Empty;

        if (SelectedWorkflow == null)
        {
            AddError(nameof(SelectedWorkflow), "Select a workflow.");
        }

        WorkflowError = GetFirstError(nameof(SelectedWorkflow));
        OnPropertyChanged(nameof(HasWorkflowError));
        OnPropertyChanged(nameof(CanSave));
    }

    private void AddError(string propertyName, string error)
    {
        if (!_errors.TryGetValue(propertyName, out var errors))
        {
            errors = new List<string>();
            _errors[propertyName] = errors;
        }

        if (!errors.Contains(error))
        {
            errors.Add(error);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }

    private void ClearErrors(string propertyName)
    {
        if (_errors.Remove(propertyName))
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }

    private string GetFirstError(string propertyName)
    {
        return _errors.TryGetValue(propertyName, out var errors) && errors.Count > 0
            ? errors[0]
            : string.Empty;
    }
}
