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
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XerahS.Core;
using XerahS.UI.ViewModels;
using XerahS.Core.Hotkeys;
using Avalonia; // For Application.Current
using XerahS.Core.Tasks;
using XerahS.Core.Managers;
using XerahS.Editor.Annotations;
using XerahS.Editor.ViewModels;
using XerahS.Editor.Views;
using XerahS.UI.Helpers;
using XerahS.UI.Services;

namespace XerahS.UI.Views
{
    public partial class MainWindow : Window
    {
        private EditorView? _editorView = null;

        /// <summary>
        /// Collection of user-configured workflows for menu binding.
        /// </summary>
        public ObservableCollection<WorkflowSettings> UserWorkflows { get; } = new ObservableCollection<WorkflowSettings>();

        public MainWindow()
        {
            InitializeComponent();
            KeyDown += OnKeyDown;

            // Initial Navigation
            var navView = this.FindControl<NavigationView>("NavView");
            if (navView != null)
            {
                // Force selection of first item
                if (navView.MenuItems[0] is NavigationViewItem item)
                {
                    navView.SelectedItem = item;
                    OnNavSelectionChanged(navView, new NavigationViewSelectionChangedEventArgs());
                }
            }

            LoadUserWorkflows();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is MainViewModel vm)
            {
                vm.NavigateRequested += OnNavigateRequested;
            }
        }

        private void OnNavigateRequested(object? sender, string tag)
        {
            NavigateTo(tag);
        }

        private void OnExitClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Loads user-configured workflows from SettingsManager into UserWorkflows collection.
        /// </summary>
        private void LoadUserWorkflows()
        {
            UserWorkflows.Clear();
            var workflows = SettingsManager.WorkflowsConfig?.Hotkeys;
            if (workflows != null)
            {
                foreach (var workflow in workflows)
                {
                    if (workflow.Job != WorkflowType.None)
                    {
                        UserWorkflows.Add(workflow);
                    }
                }
            }

            UpdateWorkflowMenuItems();
        }

        private void OnWorkflowMenuItemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is WorkflowSettings workflow)
            {
                _ = ExecuteCaptureAsync(workflow.Job, workflow.Id);
            }
        }

        private void UpdateWorkflowMenuItems()
        {
            var runWorkflowsMenuItem = this.FindControl<MenuItem>("RunWorkflowsMenuItem");
            if (runWorkflowsMenuItem == null)
            {
                return;
            }

            var workflowMenuItems = new List<MenuItem>();

            foreach (var workflow in UserWorkflows)
            {
                var workflowMenuItem = new MenuItem
                {
                    Header = GetWorkflowDisplayName(workflow),
                    DataContext = workflow
                };
                workflowMenuItem.Click += OnWorkflowMenuItemClick;
                workflowMenuItems.Add(workflowMenuItem);
            }

            if (workflowMenuItems.Count == 0)
            {
                workflowMenuItems.Add(new MenuItem
                {
                    Header = "No workflows configured",
                    IsEnabled = false
                });
            }

            runWorkflowsMenuItem.ItemsSource = workflowMenuItems;
        }

        private static string GetWorkflowDisplayName(WorkflowSettings workflow)
        {
            if (!string.IsNullOrWhiteSpace(workflow.TaskSettings?.Description))
            {
                return workflow.TaskSettings.Description;
            }

            return XerahS.Common.EnumExtensions.GetDescription(workflow.Job);
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            // Only maximize if we are NOT in silent run mode
            if (!SettingsManager.Settings.SilentRun)
            {
                // Maximize window and center it on screen
                this.WindowState = Avalonia.Controls.WindowState.Maximized;
            }

            // Update navigation items after settings are loaded
            var navView = this.FindControl<NavigationView>("NavView");
            if (navView != null)
            {
                UpdateNavigationItems(navView);
            }

            LoadUserWorkflows();

            if (Application.Current is App app && app.WorkflowManager != null)
            {
                app.WorkflowManager.WorkflowsChanged += (s, args) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        LoadUserWorkflows();

                        if (navView != null)
                        {
                            UpdateNavigationItems(navView);
                        }
                    });
                };
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            // If SilentRun is enabled and we are not explicitly exiting via Tray/Menu,
            // we should hide the window to tray instead of closing it.
            bool silentRun = SettingsManager.Settings.SilentRun;
            
            if (silentRun && !App.IsExiting)
            {
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;
                return;
            }

            base.OnClosing(e);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnNavSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
        {
            var navView = sender as NavigationView;
            var contentFrame = this.FindControl<ContentControl>("ContentFrame");
            var selectedItem = navView?.SelectedItem as NavigationViewItem;

            if (contentFrame == null || selectedItem == null)
            {
                return;
            }

            HandleNavigationTag(selectedItem.Tag?.ToString(), contentFrame);
        }

        private bool HandleNavigationTag(string? tag, ContentControl contentFrame)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            // Handle workflow execution by ID
            if (tag.StartsWith("Capture_", StringComparison.Ordinal))
            {
                var workflowId = tag.Replace("Capture_", "", StringComparison.Ordinal);
                if (!string.IsNullOrEmpty(workflowId))
                {
                    WorkflowSettings? workflow = null;

                    // Try to get workflow from WorkflowManager first
                    if (Application.Current is App app && app.WorkflowManager != null)
                    {
                        workflow = app.WorkflowManager.GetWorkflowById(workflowId);
                    }

                    // Fallback to SettingManager
                    if (workflow == null)
                    {
                        workflow = SettingsManager.WorkflowsConfig.Hotkeys.FirstOrDefault(w => w.Id == workflowId);
                    }

                    if (workflow != null)
                    {
                        _ = ExecuteCaptureAsync(workflow.Job, workflow.Id);
                        NavigateToEditor();
                        return true;
                    }
                }

                return false;
            }

            // Handle workflow execution by ID from menu
            if (tag.StartsWith("Workflow_", StringComparison.Ordinal))
            {
                var workflowId = tag.Replace("Workflow_", "", StringComparison.Ordinal);
                if (!string.IsNullOrEmpty(workflowId))
                {
                    var workflow = SettingsManager.WorkflowsConfig?.Hotkeys?.FirstOrDefault(w => w.Id == workflowId);
                    if (workflow != null)
                    {
                        _ = ExecuteCaptureAsync(workflow.Job, workflow.Id);
                        return true;
                    }
                }

                return false;
            }

            switch (tag)
            {
                case "Editor":
                    if (_editorView == null)
                    {
                        _editorView = new EditorView();
                        _editorView.ShowMenuBar = false; // Hide internal menu when hosted in MainWindow
                    }
                    contentFrame.Content = _editorView;
                    return true;
                case "Recording":
                    contentFrame.Content = new RecordingView();
                    return true;
                case "History":
                    contentFrame.Content = new HistoryView();
                    return true;
                case "Workflows":
                    contentFrame.Content = new WorkflowsView();
                    return true;
                case "Upload_ClipboardUploadWithContentViewer":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.ClipboardUploadWithContentViewer);
                    return true;
                case "Upload_FileUpload":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.FileUpload);
                    return true;
                case "Tools":
                    contentFrame.Content = new ToolsView();
                    return true;
                case "Tools_IndexFolder":
                    contentFrame.Content = new IndexFolderView();
                    return true;
                case "Tools_ColorPicker":
                    _ = ColorPickerToolService.HandleWorkflowAsync(WorkflowType.ColorPicker, this);
                    return true;
                case "Tools_ScreenColorPicker":
                    _ = ColorPickerToolService.HandleWorkflowAsync(WorkflowType.ScreenColorPicker, this);
                    return true;
                case "Tools_QrGenerator":
                    _ = QrCodeToolService.HandleWorkflowAsync(WorkflowType.QRCode, this);
                    return true;
                case "Tools_QrScanScreen":
                    _ = QrCodeToolService.HandleWorkflowAsync(WorkflowType.QRCodeDecodeFromScreen, this);
                    return true;
                case "Tools_QrScanRegion":
                    _ = QrCodeToolService.HandleWorkflowAsync(WorkflowType.QRCodeScanRegion, this);
                    return true;
                case "Tools_ImageCombiner":
                    _ = MediaToolsToolService.HandleWorkflowAsync(WorkflowType.ImageCombiner, this);
                    return true;
                case "Tools_ImageSplitter":
                    _ = MediaToolsToolService.HandleWorkflowAsync(WorkflowType.ImageSplitter, this);
                    return true;
                case "Tools_ImageThumbnailer":
                    _ = MediaToolsToolService.HandleWorkflowAsync(WorkflowType.ImageThumbnailer, this);
                    return true;
                case "Tools_VideoConverter":
                    _ = MediaToolsToolService.HandleWorkflowAsync(WorkflowType.VideoConverter, this);
                    return true;
                case "Tools_VideoThumbnailer":
                    _ = MediaToolsToolService.HandleWorkflowAsync(WorkflowType.VideoThumbnailer, this);
                    return true;
                case "Tools_AnalyzeImage":
                    _ = MediaToolsToolService.HandleWorkflowAsync(WorkflowType.AnalyzeImage, this);
                    return true;
                case "Tools_Ruler":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.Ruler);
                    return true;
                case "Tools_PinToScreenFromScreen":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.PinToScreenFromScreen);
                    return true;
                case "Tools_PinToScreenFromClipboard":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.PinToScreenFromClipboard);
                    return true;
                case "Tools_PinToScreenFromFile":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.PinToScreenFromFile);
                    return true;
                case "Tools_PinToScreenCloseAll":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.PinToScreenCloseAll);
                    return true;
                case "Tools_OCR":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.OCR);
                    return true;
                case "Tools_HashCheck":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.HashCheck);
                    return true;
                case "Tools_Metadata":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.Metadata);
                    return true;
                case "Tools_StripMetadata":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.StripMetadata);
                    return true;
                case "Tools_ClipboardViewer":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.ClipboardViewer);
                    return true;
                case "Tools_MonitorTest":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.MonitorTest);
                    return true;
                case "Settings":
                    contentFrame.Content = new SettingsView();
                    return true;
                case "Settings_App":
                    contentFrame.Content = new ApplicationSettingsView();
                    return true;
                case "Settings_Dest":
                    contentFrame.Content = new DestinationSettingsView();
                    return true;
                case "Debug":
                    contentFrame.Content = new DebugView();
                    return true;
                case "About":
                    contentFrame.Content = new AboutView();
                    return true;
                default:
                    return false;
            }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            // Skip if typing in a text input
            if (e.Source is TextBox) return;

            // Forward Crop action to EditorView if active
            if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Enter && vm.ActiveTool == EditorTool.Crop)
            {
                if (_editorView != null && _editorView.IsVisible)
                {
                    _editorView.PerformCrop();
                    e.Handled = true;
                    return;
                }
            }

            // Handle Ctrl key combinations
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                switch (e.Key)
                {
                    case Key.Z:
                        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                        {
                            vm.UndoCommand.Execute(null);
                            e.Handled = true;
                        }
                        return;
                    case Key.Y:
                        vm.RedoCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.C:
                        vm.CopyCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.S:
                        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                            vm.SaveAsCommand.Execute(null);
                        else
                            vm.QuickSaveCommand.Execute(null);
                        e.Handled = true;
                        return;
                }
            }

            // Tool selection shortcuts (single keys without modifiers)
            if (e.KeyModifiers == KeyModifiers.None)
            {
                switch (e.Key)
                {
                    case Key.V:
                        vm.SelectToolCommand.Execute(EditorTool.Select);
                        e.Handled = true;
                        break;
                    case Key.R:
                        vm.SelectToolCommand.Execute(EditorTool.Rectangle);
                        e.Handled = true;
                        break;
                    case Key.E:
                        vm.SelectToolCommand.Execute(EditorTool.Ellipse);
                        e.Handled = true;
                        break;
                    case Key.A:
                        vm.SelectToolCommand.Execute(EditorTool.Arrow);
                        e.Handled = true;
                        break;
                    case Key.L:
                        vm.SelectToolCommand.Execute(EditorTool.Line);
                        e.Handled = true;
                        break;
                    case Key.T:
                        vm.SelectToolCommand.Execute(EditorTool.Text);
                        e.Handled = true;
                        break;
                    case Key.N:
                        vm.SelectToolCommand.Execute(EditorTool.Number);
                        e.Handled = true;
                        break;
                    case Key.S:
                        vm.SelectToolCommand.Execute(EditorTool.Spotlight);
                        e.Handled = true;
                        break;
                    case Key.C:
                        vm.SelectToolCommand.Execute(EditorTool.Crop);
                        e.Handled = true;
                        break;
                    case Key.Delete:
                    case Key.Back:
                        vm.DeleteSelectedCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Escape:
                        vm.SelectToolCommand.Execute(EditorTool.Select);
                        e.Handled = true;
                        break;
                }
            }
        }


        private void OnBackdropPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.CloseModalCommand.Execute(null);
            }
        }



        public void NavigateToEditor()
        {
            NavigateTo("Editor");
        }

        public void NavigateToSettings()
        {
            NavigateTo("Settings");
        }

        public void NavigateToAbout()
        {
            NavigateTo("About");
        }

        private void NavigateTo(string navTag)
        {
            bool handled = false;
            var contentFrame = this.FindControl<ContentControl>("ContentFrame");
            var navView = this.FindControl<NavigationView>("NavView");
            if (navView != null)
            {
                var navItem = FindNavigationItemByTag(navView.MenuItems, navTag);
                if (navItem != null)
                {
                    if (!ReferenceEquals(navView.SelectedItem, navItem))
                    {
                        navView.SelectedItem = navItem;
                        handled = true;
                    }
                    else if (contentFrame != null)
                    {
                        handled = HandleNavigationTag(navTag, contentFrame);
                    }
                }
            }

            // Menu-bar actions may not have a corresponding NavigationView item.
            if (!handled && contentFrame != null)
            {
                _ = HandleNavigationTag(navTag, contentFrame);
            }

            // Ensure window is visible and active
            if (!this.IsVisible)
            {
                this.Show();
            }

            if (this.WindowState == Avalonia.Controls.WindowState.Minimized)
            {
                this.WindowState = Avalonia.Controls.WindowState.Normal;
            }

            this.Activate();
            this.Focus();
        }

        private static NavigationViewItem? FindNavigationItemByTag(IEnumerable? menuItems, string navTag)
        {
            if (menuItems == null)
            {
                return null;
            }

            foreach (var item in menuItems)
            {
                if (item is not NavigationViewItem navItem)
                {
                    continue;
                }

                if (string.Equals(navItem.Tag?.ToString(), navTag, StringComparison.Ordinal))
                {
                    return navItem;
                }

                var child = FindNavigationItemByTag(navItem.MenuItems, navTag);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private async Task ExecuteCaptureAsync(WorkflowType jobType, string? workflowId = null, AfterCaptureTasks afterCapture = AfterCaptureTasks.SaveImageToFile, SkiaSharp.SKBitmap? image = null)
        {
            TaskSettings settings;

            // Find an existing workflow - prefer by ID if provided, otherwise by job type
            WorkflowSettings? workflow = null;

            if (!string.IsNullOrEmpty(workflowId))
            {
                // Try to find by ID first
                if (Application.Current is App app && app.WorkflowManager != null)
                {
                    workflow = app.WorkflowManager.GetWorkflowById(workflowId);
                }

                if (workflow == null)
                {
                    workflow = SettingsManager.WorkflowsConfig.Hotkeys.FirstOrDefault(x => x.Id == workflowId);
                }
            }

            // Fallback to job type if no ID provided or not found
            if (workflow == null)
            {
                workflow = SettingsManager.WorkflowsConfig.Hotkeys.FirstOrDefault(x => x.Job == jobType);
            }

            if (workflow != null && workflow.TaskSettings != null)
            {
                // Clone workflow settings to avoid modifying the original instance during execution
                var jsonSettings = new Newtonsoft.Json.JsonSerializerSettings
                {
                    TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto,
                    ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace
                };
                var effectCount = workflow.TaskSettings?.ImageSettings?.ImageEffectsPreset?.Effects?.Count ?? 0;
                var presetName = workflow.TaskSettings?.ImageSettings?.ImageEffectsPreset?.Name ?? "(null)";
                Console.WriteLine($"[MainWindow] Clone workflow settings. Preset='{presetName}', Effects={effectCount}");
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(workflow.TaskSettings, jsonSettings);
                settings = Newtonsoft.Json.JsonConvert.DeserializeObject<TaskSettings>(json, jsonSettings)!;

                // Store the workflow ID in the task settings for troubleshooting
                settings.WorkflowId = workflow.Id;

                // Note: We deliberately ignore the 'afterCapture' parameter if a workflow is found,
                // as the workflow's configured tasks should take precedence.
                // We only use 'afterCapture' as a fallback when creating a temporary task setting.
            }
            else
            {
                // No workflow found, create brand new default settings (no globals)
                settings = new TaskSettings();
                settings.Job = jobType;
                // Apply the requested after capture actions since we have no user pref
                settings.AfterCaptureJob = afterCapture;
            }

            // Ensure Job is correct (if workflow had different job, we technically picked it by job, but safe to set)
            settings.Job = jobType;

            // Subscribe to task completion to update Editor preview
            void HandleTaskCompleted(object? s, WorkerTask task)
            {
                TaskManager.Instance.TaskCompleted -= HandleTaskCompleted;

                if (task.Info?.Metadata?.Image != null && DataContext is MainViewModel vm)
                {
                    vm.UpdatePreview(task.Info.Metadata.Image);
                }
            }

            TaskManager.Instance.TaskCompleted += HandleTaskCompleted;

            // Hide main window before capture to avoid capturing the app itself
            // This only applies to navbar-triggered captures, not hotkeys
            try
            {
                await Platform.Abstractions.PlatformServices.UI.HideMainWindowAsync();
            }
            catch
            {
                // Ignore errors - window hiding is not critical
            }

            try
            {
                await TaskManager.Instance.StartTask(settings, image);
            }
            finally
            {
                // Restore main window after capture
                try
                {
                    await Platform.Abstractions.PlatformServices.UI.RestoreMainWindowAsync();
                }
                catch
                {
                    // Ignore errors
                }
            }
        }

        private static Task ExecuteWorkflowFromNavigationAsync(WorkflowType jobType)
        {
            var workflow = SettingsManager.GetFirstWorkflow(jobType);

            // Upload Content nav fallback:
            // if no workflow is configured for ClipboardUploadWithContentViewer,
            // use FileUpload workflow when available.
            if (workflow == null && jobType == WorkflowType.ClipboardUploadWithContentViewer)
            {
                workflow = SettingsManager.GetFirstWorkflow(WorkflowType.FileUpload);
            }

            if (workflow != null)
            {
                return XerahS.Core.Helpers.TaskHelpers.ExecuteWorkflow(workflow, workflow.Id);
            }

            return XerahS.Core.Helpers.TaskHelpers.ExecuteJob(jobType, new TaskSettings { Job = jobType });
        }

        private void UpdateNavigationItems(NavigationView navView)
        {
            var captureItem = this.FindControl<NavigationViewItem>("CaptureNavItem");
            if (captureItem == null) return;

            // Use shared helper to update navigation items
            NavigationItemsHelper.UpdateCaptureNavigationItems(captureItem);
        }
    }
}
