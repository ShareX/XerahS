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
using ShareX.ImageEditor;
using Common = XerahS.Common;
using XerahS.Common;
using XerahS.Core;
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;

namespace XerahS.UI.ViewModels
{
    /// <summary>
    /// ViewModel for task-level settings.
    /// Split into partial-class files by concern:
    ///   TaskSettingsViewModel.Capture.cs      — capture settings
    ///   TaskSettingsViewModel.FFmpeg.cs        — FFmpeg detection, download, diagnostics
    ///   TaskSettingsViewModel.Upload.cs        — file naming / upload settings
    ///   TaskSettingsViewModel.AfterCapture.cs  — after-capture task flags
    ///   TaskSettingsViewModel.AfterUpload.cs   — after-upload task flags
    ///   TaskSettingsViewModel.General.cs       — notifications, toast, sound settings
    ///   TaskSettingsViewModel.Image.cs         — image format, quality, thumbnails
    ///   TaskSettingsViewModel.IndexFolder.cs   — index folder settings + browse commands
    /// </summary>
    public partial class TaskSettingsViewModel : ObservableObject
    {
        private TaskSettings _settings;
        private EditorCore _effectsEditorCore;

        public ImageEffectsViewModel ImageEffects { get; private set; }

        public TaskSettingsViewModel(TaskSettings settings) : this(settings, null) { }

        public TaskSettingsViewModel(TaskSettings settings, EditorCore? editorCore)
        {
            _settings = settings;
            _effectsEditorCore = editorCore ?? new EditorCore();
            ImageEffects = new ImageEffectsViewModel(Model.ImageSettings, _effectsEditorCore);
            ImageEffects.UpdatePreview();
            RefreshFFmpegState();
        }

        public IEnumerable<EImageFormat> ImageFormats => Enum.GetValues(typeof(EImageFormat)).Cast<EImageFormat>();
        public IEnumerable<ContentPlacement> ContentAlignments => Enum.GetValues(typeof(ContentPlacement)).Cast<ContentPlacement>();
        public IEnumerable<ToastClickAction> ToastClickActions => Enum.GetValues(typeof(ToastClickAction)).Cast<ToastClickAction>();
        public IEnumerable<IndexerOutput> IndexerOutputs => Enum.GetValues(typeof(IndexerOutput)).Cast<IndexerOutput>();

        // Expose underlying model if needed
        public TaskSettings Model => _settings;
        public TaskSettingsAdvanced AdvancedSettings => _settings.AdvancedSettings;

        public WorkflowType Job
        {
            get => _settings.Job;
            set
            {
                if (_settings.Job != value)
                {
                    _settings.Job = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsIndexFolderJob));
                    OnPropertyChanged(nameof(IsScreenCaptureJob));
                    OnPropertyChanged(nameof(IsScreenRecordJob));
                }
            }
        }

        public bool IsIndexFolderJob => _settings.Job == WorkflowType.IndexFolder;

        /// <summary>
        /// Returns true if the current job is a screen capture job (image output).
        /// </summary>
        public bool IsScreenCaptureJob => _settings.Job.GetHotkeyCategory() == Common.EnumExtensions.WorkflowType_Category_ScreenCapture;

        /// <summary>
        /// Returns true if the current job is a screen record job (video output).
        /// </summary>
        public bool IsScreenRecordJob => _settings.Job.GetHotkeyCategory() == Common.EnumExtensions.WorkflowType_Category_ScreenRecord;
    }
}
