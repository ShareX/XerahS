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

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using XerahS.Common;
using XerahS.Common.Converters;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Platform.Abstractions;

namespace XerahS.UI.ViewModels;

public partial class IndexFolderViewModel : ViewModelBase
{
    private readonly TaskSettings _taskSettings;

    [ObservableProperty]
    private string _folderPath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyOutputCommand))]
    private string _outputText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Select a folder to index.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
    private string _generatedFilePath = string.Empty;

    public IndexFolderViewModel()
    {
        var workflow = SettingsManager.GetFirstWorkflow(WorkflowType.IndexFolder);
        _taskSettings = workflow?.TaskSettings ?? new TaskSettings { Job = WorkflowType.IndexFolder };

        FolderPath = _taskSettings.ToolsSettings.IndexerFolderPath;
    }

    partial void OnFolderPathChanged(string value)
    {
        _taskSettings.ToolsSettings.IndexerFolderPath = value;
    }

    public IEnumerable<IndexerOutput> IndexerOutputs => Enum.GetValues(typeof(IndexerOutput)).Cast<IndexerOutput>();

    public IndexerOutput Output
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.Output;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.Output != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.Output = value;
                OnPropertyChanged();
            }
        }
    }

    public bool SkipHiddenFolders
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.SkipHiddenFolders;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.SkipHiddenFolders != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.SkipHiddenFolders = value;
                OnPropertyChanged();
            }
        }
    }

    public bool SkipHiddenFiles
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.SkipHiddenFiles;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.SkipHiddenFiles != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.SkipHiddenFiles = value;
                OnPropertyChanged();
            }
        }
    }

    public bool SkipFiles
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.SkipFiles;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.SkipFiles != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.SkipFiles = value;
                OnPropertyChanged();
            }
        }
    }

    public int MaxDepthLevel
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.MaxDepthLevel;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.MaxDepthLevel != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.MaxDepthLevel = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowSizeInfo
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.ShowSizeInfo;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.ShowSizeInfo != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.ShowSizeInfo = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AddFooter
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.AddFooter;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.AddFooter != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.AddFooter = value;
                OnPropertyChanged();
            }
        }
    }

    public string IndentationText
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.IndentationText;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.IndentationText != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.IndentationText = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AddEmptyLineAfterFolders
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.AddEmptyLineAfterFolders;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.AddEmptyLineAfterFolders != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.AddEmptyLineAfterFolders = value;
                OnPropertyChanged();
            }
        }
    }

    public bool UseCustomCssFile
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.UseCustomCSSFile;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.UseCustomCSSFile != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.UseCustomCSSFile = value;
                OnPropertyChanged();
            }
        }
    }

    public bool DisplayPath
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.DisplayPath;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.DisplayPath != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.DisplayPath = value;
                OnPropertyChanged();
            }
        }
    }

    public bool DisplayPathLimited
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.DisplayPathLimited;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.DisplayPathLimited != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.DisplayPathLimited = value;
                OnPropertyChanged();
            }
        }
    }

    public string CustomCssFilePath
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.CustomCSSFilePath;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.CustomCSSFilePath != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.CustomCSSFilePath = value;
                OnPropertyChanged();
            }
        }
    }

    public bool UseAttribute
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.UseAttribute;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.UseAttribute != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.UseAttribute = value;
                OnPropertyChanged();
            }
        }
    }

    public bool CreateParseableJson
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.CreateParseableJson;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.CreateParseableJson != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.CreateParseableJson = value;
                OnPropertyChanged();
            }
        }
    }

    public bool BinaryUnits
    {
        get => _taskSettings.ToolsSettings.IndexerSettings.BinaryUnits;
        set
        {
            if (_taskSettings.ToolsSettings.IndexerSettings.BinaryUnits != value)
            {
                _taskSettings.ToolsSettings.IndexerSettings.BinaryUnits = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasOutput => !string.IsNullOrWhiteSpace(OutputText);

    public bool CanUpload => HasOutput && !string.IsNullOrEmpty(GeneratedFilePath) && !IsBusy;

    public bool CanSave => HasOutput && !IsBusy;

    public bool IsNotBusy => !IsBusy;

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (mainWindow?.StorageProvider == null)
        {
            return;
        }

        var folders = await mainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder to Index",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            FolderPath = folders[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task BrowseCssAsync()
    {
        var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (mainWindow?.StorageProvider == null)
        {
            return;
        }

        var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Custom CSS File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSS Files") { Patterns = new[] { "*.css" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            CustomCssFilePath = files[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task IndexFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            StatusMessage = "Select a valid folder to index.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Indexing folder...";
        OutputText = string.Empty;
        GeneratedFilePath = string.Empty;
        OnPropertyChanged(nameof(HasOutput));
        OnPropertyChanged(nameof(CanUpload));
        OnPropertyChanged(nameof(CanSave));

        try
        {
            _taskSettings.Job = WorkflowType.IndexFolder;
            _taskSettings.ToolsSettings.IndexerFolderPath = FolderPath;

            var indexerSettings = BuildIndexerSettings(_taskSettings.ToolsSettings.IndexerSettings);
            string output = await Task.Run(() => XerahS.Indexer.Indexer.Index(FolderPath, indexerSettings));

            GeneratedFilePath = WriteIndexOutput(_taskSettings, output);
            OutputText = output;
            StatusMessage = $"Index generated: {GeneratedFilePath}";
            SaveWorkflowSettingsIfAvailable();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Indexing failed: {ex.Message}";
            DebugHelper.WriteException(ex, "IndexFolder");
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasOutput));
            OnPropertyChanged(nameof(CanUpload));
            OnPropertyChanged(nameof(CanSave));
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
        OnPropertyChanged(nameof(CanUpload));
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnOutputTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasOutput));
        OnPropertyChanged(nameof(CanUpload));
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnGeneratedFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(CanUpload));
        OnPropertyChanged(nameof(CanSave));
    }

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task UploadAsync()
    {
        if (!CanUpload)
        {
            return;
        }

        var settings = CloneTaskSettings(_taskSettings);
        settings.Job = WorkflowType.IndexFolder;

        await TaskManager.Instance.StartFileTask(settings, GeneratedFilePath);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsAsync()
    {
        if (!CanSave)
        {
            return;
        }

        var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (mainWindow?.StorageProvider == null)
        {
            return;
        }

        string suggestedName = GetSuggestedFileName();
        var file = await mainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Index Output",
            SuggestedFileName = suggestedName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Index Output") { Patterns = new[] { $"*.{GetOutputExtension(Output)}" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (file?.Path == null)
        {
            return;
        }

        await File.WriteAllTextAsync(file.Path.LocalPath, OutputText);
        StatusMessage = $"Saved to {file.Path.LocalPath}";
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private async Task CopyOutputAsync()
    {
        if (!HasOutput || !PlatformServices.IsInitialized)
        {
            return;
        }

        await PlatformServices.Clipboard.SetTextAsync(OutputText);
        StatusMessage = "Output copied to clipboard.";
    }

    private static XerahS.Indexer.IndexerSettings BuildIndexerSettings(XerahS.Core.IndexerSettings settings)
    {
        var indexerSettings = new XerahS.Indexer.IndexerSettings
        {
            Output = (XerahS.Indexer.IndexerOutput)settings.Output,
            SkipHiddenFolders = settings.SkipHiddenFolders,
            SkipHiddenFiles = settings.SkipHiddenFiles,
            SkipFiles = settings.SkipFiles,
            MaxDepthLevel = settings.MaxDepthLevel,
            ShowSizeInfo = settings.ShowSizeInfo,
            AddFooter = settings.AddFooter,
            IndentationText = settings.IndentationText,
            AddEmptyLineAfterFolders = settings.AddEmptyLineAfterFolders,
            UseCustomCSSFile = settings.UseCustomCSSFile,
            DisplayPath = settings.DisplayPath,
            DisplayPathLimited = settings.DisplayPathLimited,
            CustomCSSFilePath = settings.CustomCSSFilePath,
            UseAttribute = settings.UseAttribute,
            CreateParseableJson = settings.CreateParseableJson
        };

        indexerSettings.BinaryUnits = settings.BinaryUnits;
        return indexerSettings;
    }

    private string WriteIndexOutput(TaskSettings taskSettings, string output)
    {
        string extension = GetOutputExtension(Output);
        string screenshotsFolder = TaskHelpers.GetScreenshotsFolder(taskSettings);
        Directory.CreateDirectory(screenshotsFolder);

        string fileName = TaskHelpers.GetFileName(taskSettings, extension);
        string resolvedPath = TaskHelpers.HandleExistsFile(screenshotsFolder, fileName, taskSettings);
        File.WriteAllText(resolvedPath, output);
        return resolvedPath;
    }

    private string GetOutputExtension(IndexerOutput output)
    {
        return output switch
        {
            IndexerOutput.Html => "html",
            IndexerOutput.Txt => "txt",
            IndexerOutput.Xml => "xml",
            IndexerOutput.Json => "json",
            _ => "txt"
        };
    }

    private string GetSuggestedFileName()
    {
        string extension = GetOutputExtension(Output);
        string baseName = string.IsNullOrWhiteSpace(FolderPath)
            ? "index"
            : Path.GetFileName(FolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = "index";
        }

        return $"{baseName}.{extension}";
    }

    private static TaskSettings CloneTaskSettings(TaskSettings source)
    {
        var jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Converters = new List<JsonConverter>
            {
                new StringEnumConverter(),
                new SkColorJsonConverter()
            }
        };

        string json = JsonConvert.SerializeObject(source, jsonSettings);
        return JsonConvert.DeserializeObject<TaskSettings>(json, jsonSettings) ?? new TaskSettings();
    }

    private static void SaveWorkflowSettingsIfAvailable()
    {
        if (SettingsManager.GetFirstWorkflow(WorkflowType.IndexFolder) != null)
        {
            SettingsManager.SaveWorkflowsConfig();
        }
    }
}
