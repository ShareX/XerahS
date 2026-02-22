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
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using XerahS.Core;

namespace XerahS.UI.ViewModels
{
    public partial class TaskSettingsViewModel
    {
        #region Index Folder Commands

        [RelayCommand]
        private async Task BrowseIndexerFolderAsync()
        {
            var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (window?.StorageProvider == null)
            {
                return;
            }

            var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Folder to Index",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                IndexerFolderPath = folders[0].Path.LocalPath;
            }
        }

        [RelayCommand]
        private async Task BrowseIndexerCssFileAsync()
        {
            var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (window?.StorageProvider == null)
            {
                return;
            }

            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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
                IndexerCustomCssFilePath = files[0].Path.LocalPath;
            }
        }

        #endregion

        #region Index Folder Settings

        public string IndexerFolderPath
        {
            get => _settings.ToolsSettings.IndexerFolderPath;
            set
            {
                if (_settings.ToolsSettings.IndexerFolderPath != value)
                {
                    _settings.ToolsSettings.IndexerFolderPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public IndexerOutput IndexerOutput
        {
            get => _settings.ToolsSettings.IndexerSettings.Output;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.Output != value)
                {
                    _settings.ToolsSettings.IndexerSettings.Output = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerSkipHiddenFolders
        {
            get => _settings.ToolsSettings.IndexerSettings.SkipHiddenFolders;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.SkipHiddenFolders != value)
                {
                    _settings.ToolsSettings.IndexerSettings.SkipHiddenFolders = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerSkipHiddenFiles
        {
            get => _settings.ToolsSettings.IndexerSettings.SkipHiddenFiles;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.SkipHiddenFiles != value)
                {
                    _settings.ToolsSettings.IndexerSettings.SkipHiddenFiles = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerSkipFiles
        {
            get => _settings.ToolsSettings.IndexerSettings.SkipFiles;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.SkipFiles != value)
                {
                    _settings.ToolsSettings.IndexerSettings.SkipFiles = value;
                    OnPropertyChanged();
                }
            }
        }

        public int IndexerMaxDepthLevel
        {
            get => _settings.ToolsSettings.IndexerSettings.MaxDepthLevel;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.MaxDepthLevel != value)
                {
                    _settings.ToolsSettings.IndexerSettings.MaxDepthLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerShowSizeInfo
        {
            get => _settings.ToolsSettings.IndexerSettings.ShowSizeInfo;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.ShowSizeInfo != value)
                {
                    _settings.ToolsSettings.IndexerSettings.ShowSizeInfo = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerAddFooter
        {
            get => _settings.ToolsSettings.IndexerSettings.AddFooter;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.AddFooter != value)
                {
                    _settings.ToolsSettings.IndexerSettings.AddFooter = value;
                    OnPropertyChanged();
                }
            }
        }

        public string IndexerIndentationText
        {
            get => _settings.ToolsSettings.IndexerSettings.IndentationText;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.IndentationText != value)
                {
                    _settings.ToolsSettings.IndexerSettings.IndentationText = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerAddEmptyLineAfterFolders
        {
            get => _settings.ToolsSettings.IndexerSettings.AddEmptyLineAfterFolders;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.AddEmptyLineAfterFolders != value)
                {
                    _settings.ToolsSettings.IndexerSettings.AddEmptyLineAfterFolders = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerUseCustomCssFile
        {
            get => _settings.ToolsSettings.IndexerSettings.UseCustomCSSFile;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.UseCustomCSSFile != value)
                {
                    _settings.ToolsSettings.IndexerSettings.UseCustomCSSFile = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerDisplayPath
        {
            get => _settings.ToolsSettings.IndexerSettings.DisplayPath;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.DisplayPath != value)
                {
                    _settings.ToolsSettings.IndexerSettings.DisplayPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerDisplayPathLimited
        {
            get => _settings.ToolsSettings.IndexerSettings.DisplayPathLimited;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.DisplayPathLimited != value)
                {
                    _settings.ToolsSettings.IndexerSettings.DisplayPathLimited = value;
                    OnPropertyChanged();
                }
            }
        }

        public string IndexerCustomCssFilePath
        {
            get => _settings.ToolsSettings.IndexerSettings.CustomCSSFilePath;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.CustomCSSFilePath != value)
                {
                    _settings.ToolsSettings.IndexerSettings.CustomCSSFilePath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerUseAttribute
        {
            get => _settings.ToolsSettings.IndexerSettings.UseAttribute;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.UseAttribute != value)
                {
                    _settings.ToolsSettings.IndexerSettings.UseAttribute = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerCreateParseableJson
        {
            get => _settings.ToolsSettings.IndexerSettings.CreateParseableJson;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.CreateParseableJson != value)
                {
                    _settings.ToolsSettings.IndexerSettings.CreateParseableJson = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IndexerBinaryUnits
        {
            get => _settings.ToolsSettings.IndexerSettings.BinaryUnits;
            set
            {
                if (_settings.ToolsSettings.IndexerSettings.BinaryUnits != value)
                {
                    _settings.ToolsSettings.IndexerSettings.BinaryUnits = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion
    }
}
