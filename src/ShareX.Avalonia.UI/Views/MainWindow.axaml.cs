using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using XerahS.Core;
using XerahS.UI.ViewModels;
using XerahS.Core.Hotkeys;
using Avalonia; // For Application.Current
using XerahS.Core.Tasks;
using XerahS.Core.Managers;
using ShareX.Editor.Annotations;
using ShareX.Editor.ViewModels;
using ShareX.Editor.Views;
using XerahS.UI.Helpers;

namespace XerahS.UI.Views
{
    public partial class MainWindow : Window
    {
        private EditorView? _editorView;

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
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            // Maximize window and center it on screen
            this.WindowState = Avalonia.Controls.WindowState.Maximized;

            // Update navigation items after settings are loaded
            var navView = this.FindControl<NavigationView>("NavView");
            if (navView != null)
            {
                UpdateNavigationItems(navView);
            }

            if (Application.Current is App app && app.WorkflowManager != null)
            {
                app.WorkflowManager.WorkflowsChanged += (s, args) =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (navView != null)
                        {
                            UpdateNavigationItems(navView);
                        }
                    });
                };
            }
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

            if (contentFrame != null && selectedItem != null && DataContext is MainViewModel vm)
            {
                var tag = selectedItem.Tag?.ToString();

                // Handle workflow execution by ID
                if (tag != null && tag.StartsWith("Capture_"))
                {
                    var workflowId = tag.Replace("Capture_", "");
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
                            workflow = SettingManager.WorkflowsConfig.Hotkeys.FirstOrDefault(w => w.Id == workflowId);
                        }

                        if (workflow != null)
                        {
                            _ = ExecuteCaptureAsync(workflow.Job, workflow.Id);
                            NavigateToEditor();
                        }
                    }
                }

                switch (tag)
                {
                    case "Editor":
                        if (_editorView == null) _editorView = new EditorView();
                        contentFrame.Content = _editorView;
                        break;
                    case "Recording":
                        contentFrame.Content = new RecordingView();
                        break;
                    case "History":
                        contentFrame.Content = new HistoryView();
                        break;
                    case "Workflows":
                        contentFrame.Content = new WorkflowsView();
                        break;
                    case "Settings":
                        contentFrame.Content = new SettingsView();
                        break;
                    case "Settings_App":
                        contentFrame.Content = new ApplicationSettingsView();
                        break;


                    case "Settings_Dest":
                        contentFrame.Content = new DestinationSettingsView();
                        break;
                    case "Debug":
                        contentFrame.Content = new DebugView();
                        break;
                }
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
                        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                            vm.RedoCommand.Execute(null);
                        else
                            vm.UndoCommand.Execute(null);
                        e.Handled = true;
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



        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            // Setup listeners if needed
        }
        public void NavigateToEditor()
        {
            var navView = this.FindControl<NavigationView>("NavView");
            if (navView != null)
            {
                // Navigate to Editor (Tag="Editor")
                foreach (var item in navView.MenuItems)
                {
                    if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == "Editor")
                    {
                        navView.SelectedItem = navItem;
                        break;
                    }
                }
            }

            // Ensure window is visible and active
            if (!this.IsVisible)
            {
                this.Show();
            }

            if (this.WindowState == Avalonia.Controls.WindowState.Minimized)
            {
                this.WindowState = Avalonia.Controls.WindowState.Maximized;
            }

            this.Activate();
            this.Focus();
        }

        public void NavigateToSettings()
        {
            var navView = this.FindControl<NavigationView>("NavView");
            if (navView != null)
            {
                // Navigate to Settings (Tag="Settings")
                foreach (var item in navView.MenuItems)
                {
                    if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == "Settings")
                    {
                        navView.SelectedItem = navItem;
                        break;
                    }
                }
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

        private async Task ExecuteCaptureAsync(HotkeyType jobType, string? workflowId = null, AfterCaptureTasks afterCapture = AfterCaptureTasks.SaveImageToFile, SkiaSharp.SKBitmap? image = null)
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
                    workflow = SettingManager.WorkflowsConfig.Hotkeys.FirstOrDefault(x => x.Id == workflowId);
                }
            }

            // Fallback to job type if no ID provided or not found
            if (workflow == null)
            {
                workflow = SettingManager.WorkflowsConfig.Hotkeys.FirstOrDefault(x => x.Job == jobType);
            }

            if (workflow != null && workflow.TaskSettings != null)
            {
                // Clone workflow settings to avoid modifying the original instance during execution
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(workflow.TaskSettings);
                settings = Newtonsoft.Json.JsonConvert.DeserializeObject<TaskSettings>(json)!;

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
            await TaskManager.Instance.StartTask(settings, image);
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
