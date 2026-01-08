using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using XerahS.Core;
using XerahS.Core.Managers;
using XerahS.Core.Tasks;
using ShareX.Editor.Annotations;
using ShareX.Editor.ViewModels;
using ShareX.Editor.Views;

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

                switch (tag)
                {
                    case "Capture_0":
                    case "Capture_1":
                    case "Capture_2":
                        // Execute workflow by index
                        if (int.TryParse(tag.Replace("Capture_", ""), out int workflowIndex))
                        {
                            var workflows = SettingManager.WorkflowsConfig.Hotkeys.Take(3).ToList();
                            if (workflowIndex < workflows.Count)
                            {
                                var workflow = workflows[workflowIndex];
                                _ = ExecuteCaptureAsync(workflow.Job);
                                NavigateToEditor();
                            }
                        }
                        break;
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

        private async Task ExecuteCaptureAsync(HotkeyType jobType, AfterCaptureTasks afterCapture = AfterCaptureTasks.SaveImageToFile, SkiaSharp.SKBitmap? image = null)
        {
            TaskSettings settings;

            // Find an existing workflow for this job type
            var workflow = SettingManager.WorkflowsConfig.Hotkeys.FirstOrDefault(x => x.Job == jobType);

            if (workflow != null && workflow.TaskSettings != null)
            {
                // Clone workflow settings to avoid modifying the original instance during execution
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(workflow.TaskSettings);
                settings = Newtonsoft.Json.JsonConvert.DeserializeObject<TaskSettings>(json)!;

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

            // Clear existing items
            captureItem.MenuItems.Clear();

            // Get first 3 workflows
            var workflows = SettingManager.WorkflowsConfig.Hotkeys.Take(3).ToList();

            for (int i = 0; i < workflows.Count; i++)
            {
                var workflow = workflows[i];
                var description = string.IsNullOrEmpty(workflow.TaskSettings.Description)
                    ? XerahS.Common.EnumExtensions.GetDescription(workflow.Job)
                    : workflow.TaskSettings.Description;

                var navItem = new NavigationViewItem
                {
                    Content = description,
                    Tag = $"Capture_{i}" // Use index-based tag
                };

                captureItem.MenuItems.Add(navItem);
            }
        }
    }
}
