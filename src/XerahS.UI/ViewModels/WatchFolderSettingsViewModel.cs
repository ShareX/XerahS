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
using System.IO;
using XerahS.Core;

namespace XerahS.UI.ViewModels;

public partial class WatchFolderSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _folderPath = string.Empty;

    [ObservableProperty]
    private string _filter = "*.*";

    [ObservableProperty]
    private bool _includeSubdirectories;

    [ObservableProperty]
    private bool _moveFilesToScreenshotsFolder;

    [ObservableProperty]
    private bool _convertMovToMp4BeforeProcessing;

    [ObservableProperty]
    private string _statusText = "Disabled";

    [ObservableProperty]
    private string _statusDetail = "Watch folders are disabled.";

    [ObservableProperty]
    private string _workflowId = string.Empty;

    [ObservableProperty]
    private string _workflowName = "Unassigned";

    [ObservableProperty]
    private bool _enabled = true;

    public string IncludeSubdirectoriesText => IncludeSubdirectories ? "Yes" : "No";

    public string MoveFilesToScreenshotsFolderText => MoveFilesToScreenshotsFolder ? "Yes" : "No";

    public string ConvertMovToMp4BeforeProcessingText => ConvertMovToMp4BeforeProcessing ? "Yes" : "No";

    partial void OnIncludeSubdirectoriesChanged(bool value)
    {
        OnPropertyChanged(nameof(IncludeSubdirectoriesText));
    }

    partial void OnMoveFilesToScreenshotsFolderChanged(bool value)
    {
        OnPropertyChanged(nameof(MoveFilesToScreenshotsFolderText));
    }

    partial void OnConvertMovToMp4BeforeProcessingChanged(bool value)
    {
        OnPropertyChanged(nameof(ConvertMovToMp4BeforeProcessingText));
    }


    public void UpdateStatus(bool isEnabled, bool workflowValid)
    {
        if (!isEnabled)
        {
            StatusText = "Disabled";
            StatusDetail = "Watch folders are disabled.";
            return;
        }

        if (!Enabled)
        {
            StatusText = "Disabled";
            StatusDetail = "Watch folder is disabled.";
            return;
        }

        if (!workflowValid)
        {
            StatusText = "Error";
            StatusDetail = "Workflow not found.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            StatusText = "Error";
            StatusDetail = "Folder path is empty.";
            return;
        }

        if (!Directory.Exists(FolderPath))
        {
            StatusText = "Error";
            StatusDetail = "Folder does not exist.";
            return;
        }

        StatusText = "Active";
        StatusDetail = "Watching for new files.";
    }

    public static WatchFolderSettingsViewModel FromSettings(WatchFolderSettings settings)
    {
        return new WatchFolderSettingsViewModel
        {
            FolderPath = settings.FolderPath,
            Filter = string.IsNullOrWhiteSpace(settings.Filter) ? "*.*" : settings.Filter,
            IncludeSubdirectories = settings.IncludeSubdirectories,
            MoveFilesToScreenshotsFolder = settings.MoveFilesToScreenshotsFolder,
            ConvertMovToMp4BeforeProcessing = settings.ConvertMovToMp4BeforeProcessing,
            WorkflowId = settings.WorkflowId,
            Enabled = settings.Enabled
        };
    }

    public WatchFolderSettings ToSettings()
    {
        return new WatchFolderSettings
        {
            FolderPath = FolderPath,
            Filter = string.IsNullOrWhiteSpace(Filter) ? "*.*" : Filter,
            IncludeSubdirectories = IncludeSubdirectories,
            MoveFilesToScreenshotsFolder = MoveFilesToScreenshotsFolder,
            ConvertMovToMp4BeforeProcessing = ConvertMovToMp4BeforeProcessing,
            WorkflowId = WorkflowId,
            Enabled = Enabled
        };
    }
}
