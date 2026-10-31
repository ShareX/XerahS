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
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using XerahS.Bootstrap;
using XerahS.Core;
using XerahS.UI.ViewModels;
using XerahS.Core.Hotkeys;
using Avalonia; // For Application.Current
using XerahS.Core.Tasks;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Presentation.ViewModels;
using ShareX.ImageEditor.Presentation.Views;
using XerahS.UI.Views.Dialogs;
using XerahS.UI.Services;
using XerahS.UI.Helpers;
using XerahS.UI.Services.SettingsSearch;

namespace XerahS.UI.Views
{
    public partial class MainWindow : Window
    {
        private const double DefaultWindowWidth = 1100;
        private const double DefaultWindowHeight = 650;
        private const int MinimumPersistedWindowDimension = 200;

        private readonly IDesktopTaskManager? _taskManager;
        private EditorView? _editorView = null;
        private DestinationSettingsView? _destinationSettingsView = null;
        private ApplicationSettingsView? _applicationSettingsView = null;
        private MainViewModel? _mainViewModel;
        private bool _isOpenImageInProgress;
        private bool _onboardingShown;
        private bool _suppressWindowActivation = true;
        private bool _silentRunHideApplied;

        /// <summary>
        /// Collection of user-configured workflows for menu binding.
        /// </summary>
        public ObservableCollection<WorkflowSettings> UserWorkflows { get; } = new ObservableCollection<WorkflowSettings>();
        public ObservableCollection<NavigationNode> NavigationNodes { get; } = new ObservableCollection<NavigationNode>();
        public IAsyncRelayCommand OpenImageMenuCommand { get; }
        public IRelayCommand ExitMenuCommand { get; }
        public IRelayCommand<string?> NavigateMenuCommand { get; }
        public IRelayCommand<WorkflowSettings?> RunWorkflowFromMenuCommand { get; }

        public MainWindow() : this(null)
        {
        }

        public MainWindow(IDesktopTaskManager? taskManager)
        {
            _taskManager = taskManager;
            OpenImageMenuCommand = new AsyncRelayCommand(OpenImageFromFileAsync);
            ExitMenuCommand = new RelayCommand(Close);
            NavigateMenuCommand = new RelayCommand<string?>(NavigateFromMenuTag);
            RunWorkflowFromMenuCommand = new RelayCommand<WorkflowSettings?>(RunWorkflowFromMenu);
            InitializeComponent();
            DataContextChanged += OnMainWindowDataContextChanged;
            KeyDown += OnKeyDown;
            // Tunnel so Esc closes a modal dialog even when a control inside it has focus.
            AddHandler(KeyDownEvent, OnModalEscapeKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            ApplyInitialWindowPlacement();

            if (this.FindControl<ContentControl>("ContentFrame") is ContentControl contentFrame)
            {
                contentFrame.PropertyChanged += OnContentFramePropertyChanged;
            }

#if !DEBUG
            // Video Editor is a work-in-progress; hide it in release builds.
            var menuItemVideoEditor = this.FindControl<MenuItem>("MenuItemVideoEditor");
            if (menuItemVideoEditor != null)
                menuItemVideoEditor.IsVisible = false;
#endif

            BuildNavigationNodes();

            var navigationTree = this.FindControl<TreeView>("NavigationTree");
            if (navigationTree != null)
            {
                navigationTree.ItemsSource = NavigationNodes;
            }

            LoadUserWorkflows();
            // Do not Show/Activate here. NavigateTo used to call Show() while the window
            // was still being constructed, which fired Opened before App could attach the
            // SilentRun hide-to-tray handler. Avalonia shows MainWindow after startup.
            NavigateTo("Editor");
            _suppressWindowActivation = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            DataContextChanged -= OnMainWindowDataContextChanged;

            if (this.FindControl<ContentControl>("ContentFrame") is ContentControl contentFrame)
            {
                contentFrame.PropertyChanged -= OnContentFramePropertyChanged;
            }

            if (_mainViewModel != null)
            {
                _mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
                _mainViewModel = null;
            }

            base.OnClosed(e);
        }

        private void OnMainWindowDataContextChanged(object? sender, EventArgs e)
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
            }

            if (sender is not MainWindow window || window.DataContext is not MainViewModel nextVm)
            {
                _mainViewModel = null;
                UpdateShellModalVisibility();
                return;
            }

            _mainViewModel = nextVm;
            _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
            UpdateShellModalVisibility();
        }

        private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainViewModel.IsModalOpen) or nameof(MainViewModel.ModalContent))
            {
                UpdateShellModalVisibility();
            }
        }

        private void OnContentFramePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ContentControl.ContentProperty)
            {
                UpdateShellModalVisibility();
                QueueNavigationFilterUpdate();
            }
        }

        private void UpdateShellModalVisibility()
        {
            Grid? overlay = this.FindControl<Grid>("MainWindowModalOverlay");
            if (overlay == null)
            {
                return;
            }

            bool isModalOpen = _mainViewModel?.IsModalOpen == true;
            bool editorOwnsModal = MainViewModel.IsEditorOwnedModal(_mainViewModel?.ModalContent);

            // Editor dialogs bring their own surface. App dialogs (update prompt and the
            // rest) use this overlay's opaque card, including while the editor page is open.
            overlay.IsVisible = isModalOpen && !editorOwnsModal;
        }

        private void NavigateFromMenuTag(string? navTag)
        {
            if (!string.IsNullOrWhiteSpace(navTag))
            {
                NavigateTo(navTag);
            }
        }

        private void RunWorkflowFromMenu(WorkflowSettings? workflow)
        {
            if (workflow != null)
            {
                _ = ExecuteCaptureAsync(workflow.Job, workflow.Id);
            }
        }

        private async Task OpenImageFromFileAsync()
        {
            if (_isOpenImageInProgress)
            {
                return;
            }

            _isOpenImageInProgress = true;

            try
            {
                if (DataContext is not MainViewModel vm)
                {
                    return;
                }

                string? path = await PickImagePathAsync();
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                if (vm.PreviewImage == null)
                {
                    ReplaceImageFromPath(vm, path);
                    return;
                }

                OpenImageChoice choice = await ShowOpenImageChoiceDialogAsync();
                switch (choice)
                {
                    case OpenImageChoice.ReplaceImage:
                        ReplaceImageFromPath(vm, path);
                        break;
                    case OpenImageChoice.AddAsShape:
                        await AddImageAsShapeFromPathAsync(path);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                XerahS.Common.DebugHelper.WriteException(ex, "File > Open failed");
            }
            finally
            {
                _isOpenImageInProgress = false;
            }
        }

        private async Task<string?> PickImagePathAsync()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null)
            {
                return null;
            }

            var options = new FilePickerOpenOptions
            {
                Title = "Open Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp", "*.tiff", "*.tif" }
                    },
                    FilePickerFileTypes.All
                }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (files.Count < 1)
            {
                return null;
            }

            string? path = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            return path;
        }

        private async Task<OpenImageChoice> ShowOpenImageChoiceDialogAsync()
        {
            try
            {
                var dialogs = UiViewModelFactoryAccessor.GetRequired().ViewDialogService;
                return await dialogs.ShowOpenImageChoiceAsync();
            }
            catch (Exception ex)
            {
                XerahS.Common.DebugHelper.WriteException(ex, "Failed to show open image choice dialog");
                return OpenImageChoice.Cancel;
            }
        }

        private void ReplaceImageFromPath(MainViewModel vm, string path)
        {
            SKBitmap? bitmap = null;

            try
            {
                bitmap = SKBitmap.Decode(path);
                if (bitmap == null || bitmap.Handle == IntPtr.Zero)
                {
                    bitmap?.Dispose();
                    return;
                }

                NavigateToEditor();
                vm.ClearCommand.Execute(null);

                // Ownership of bitmap is transferred to ViewModel.
                vm.UpdatePreview(bitmap, clearAnnotations: true);
                vm.ImageFilePath = path;
                bitmap = null;
            }
            catch (Exception ex)
            {
                XerahS.Common.DebugHelper.WriteException(ex, "Failed to load selected image");
                bitmap?.Dispose();
            }
        }

        private async Task AddImageAsShapeFromPathAsync(string path)
        {
            try
            {
                NavigateToEditor();

                if (_editorView == null)
                {
                    return;
                }

                // XIP0039 Guardrail 6: Call the now-public InsertImageAnnotation directly
                // instead of using reflection (BindingFlags.NonPublic).
                var bitmap = SKBitmap.Decode(path);
                if (bitmap == null || bitmap.Handle == IntPtr.Zero)
                {
                    bitmap?.Dispose();
                    return;
                }

                try
                {
                    _editorView.InsertImageAnnotation(bitmap, dropPosition: null);
                    bitmap = null; // Ownership transferred to inserted image annotation.
                }
                finally
                {
                    bitmap?.Dispose();
                }
            }
            catch (Exception ex)
            {
                XerahS.Common.DebugHelper.WriteException(ex, "Failed to add selected image as annotation");
            }

            await Task.CompletedTask;
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
                    Command = RunWorkflowFromMenuCommand,
                    CommandParameter = workflow
                };
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
            // First Opened only: later tray "Open Main Window" must stay visible.
            if (SilentRunStartupPolicy.ShouldHideMainWindowToTray(
                    SettingsManager.Settings.SilentRun, App.IsExiting, _silentRunHideApplied))
            {
                _silentRunHideApplied = true;
                if (!SettingsManager.Settings.ShowTray)
                {
                    SettingsManager.Settings.ShowTray = true;
                    TrayIconHelper.Instance.RefreshFromSettings();
                }
                SilentRunStartupPolicy.ApplyHiddenToTray(this);
                XerahS.Common.DebugHelper.WriteLine("SilentRun startup: main window hidden to tray.");
            }

            // Provide the native window handle to platform services so the Wayland GlobalShortcuts
            // portal can display a transient permissions dialog (GNOME returns response=2 without it).
            // On X11/XWayland the descriptor is "XID"; on native Wayland it is "wl_surface"
            // (xdg-foreign export not yet implemented, so that path still passes empty string).
            var platformHandle = TryGetPlatformHandle();
            XerahS.Common.DebugHelper.WriteLine(
                $"MainWindow: OnWindowOpened — platform handle descriptor={platformHandle?.HandleDescriptor ?? "<null>"}, handle={platformHandle?.Handle}");

            if (platformHandle != null)
            {
                XerahS.Platform.Abstractions.PlatformServices.NativeWindowHandleProvider = () =>
                    platformHandle.HandleDescriptor == "XID"
                        ? $"x11:0x{platformHandle.Handle:x}"
                        : null;
            }

            // Notify the hotkey service that the window is ready and the native window handle is
            // now available via NativeWindowHandleProvider. If the portal BindShortcuts call at
            // startup ran before this point (e.g. the 100ms debounce fired while the window was
            // still initialising — in debug builds startup can take 40+ seconds) and received
            // parentWindow="" which caused a response=2 failure, this triggers a portal retry so
            // hotkeys work globally without needing an app restart.
            // The window can open before bootstrap has initialized platform services; defer until ready.
            XerahS.Platform.Abstractions.PlatformServices.RunWhenInitialized(() =>
            {
                try
                {
                    XerahS.Platform.Abstractions.PlatformServices.Hotkey.NotifyWindowReady();
                }
                catch (Exception ex)
                {
                    XerahS.Common.DebugHelper.WriteException(ex, "MainWindow: NotifyWindowReady failed");
                }
            });

            UpdateNavigationItems();

            LoadUserWorkflows();

            if (Application.Current is App app && app.WorkflowManager != null)
            {
                app.WorkflowManager.WorkflowsChanged += (s, args) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        LoadUserWorkflows();

                        UpdateNavigationItems();
                    });
                };
            }

            // Pre-warm settings pages so first open and settings search indexing stay off the hot path.
            // Settings views need the UI view model factory, which bootstrap may not have configured yet.
            UiViewModelFactoryAccessor.RunWhenAvailable(() =>
                Dispatcher.UIThread.Post(() => _ = PreWarmSettingsSearchIndexAsync(), DispatcherPriority.Background));

            // Show onboarding wizard once on first run — guard prevents double-fire on repeated OnWindowOpened calls.
            // Skip when SilentRun hid the window: ShowDialog would re-show the owner.
            if (SettingsManager.Settings.IsFirstTimeRun && !_onboardingShown && !_silentRunHideApplied)
            {
                _onboardingShown = true;
                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        var wizard = new XerahS.UI.Onboarding.OnboardingWizardWindow();
                        var result = await wizard.ShowDialogAsync(this);

                        // The settings page may have been pre-warmed before the wizard changed the theme;
                        // sync it so its next auto-save does not write the old theme back.
                        if (_applicationSettingsView?.DataContext is SettingsViewModel settingsViewModel)
                        {
                            settingsViewModel.ThemeMode = SettingsManager.Settings.ThemeMode;
                        }

                        if (result.Completed)
                        {
                            XerahS.Common.DebugHelper.WriteLine("[Onboarding] Wizard completed, marking first-time run complete.");
                            SettingsManager.Settings.MarkFirstTimeRunCompleted(persist: false);
                        }
                    }
                    catch (Exception ex)
                    {
                        XerahS.Common.DebugHelper.WriteException(ex, "[Onboarding] Error showing wizard");
                    }
                }, DispatcherPriority.Background);
            }
        }

        private async Task PreWarmSettingsSearchIndexAsync()
        {
            try
            {
                _applicationSettingsView ??= CreateApplicationSettingsView();
                SettingsSearchService.Instance.MergeApplicationIndex(_applicationSettingsView);

                _destinationSettingsView ??= CreateDestinationSettingsView();
                if (_destinationSettingsView.DataContext is DestinationSettingsViewModel destinationVm)
                {
                    await destinationVm.Initialize();
                    var categories = destinationVm.Categories.Select(category =>
                        (category.Name, category.Instances.Select(instance => instance.DisplayName)));
                    SettingsSearchService.Instance.MergeDestinationIndex(_destinationSettingsView, categories);
                }

                EnrichNavigationSearchTextFromSettingsIndex();
            }
            catch (Exception ex)
            {
                XerahS.Common.DebugHelper.WriteException(ex, "Failed to pre-warm settings search index");
            }
        }

        private async Task PreWarmDestinationSettingsAsync()
        {
            await PreWarmSettingsSearchIndexAsync();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            PersistWindowPlacement();

            // If SilentRun ("Start minimized to tray") is enabled and we are not explicitly
            // exiting via Tray → Exit, hide the window to tray instead of closing the app.
            // This works on all platforms (Windows, Linux, macOS); no OS-specific logic.
            bool silentRun = SettingsManager.Settings.SilentRun;

            if (silentRun && !App.IsExiting)
            {
                e.Cancel = true;
                // Ensure tray icon is visible so user can restore or exit (handles edge case
                // where config had SilentRun true but ShowTray false, e.g. from another machine).
                if (!SettingsManager.Settings.ShowTray)
                {
                    SettingsManager.Settings.ShowTray = true;
                    TrayIconHelper.Instance.RefreshFromSettings();
                }
                this.Hide();
                this.ShowInTaskbar = false;
                return;
            }

            base.OnClosing(e);
        }

        private void ApplyInitialWindowPlacement()
        {
            ApplicationConfig settings = SettingsManager.Settings;
            bool rememberSize = settings.RememberMainFormSize;
            bool rememberPosition = settings.RememberMainFormPosition;
            bool appliedSize = false;

            if (rememberSize &&
                settings.MainFormSize.Width >= MinimumPersistedWindowDimension &&
                settings.MainFormSize.Height >= MinimumPersistedWindowDimension)
            {
                Width = settings.MainFormSize.Width;
                Height = settings.MainFormSize.Height;
                appliedSize = true;
            }

            if (!appliedSize && (rememberSize || rememberPosition))
            {
                Width = DefaultWindowWidth;
                Height = DefaultWindowHeight;
            }

            if (rememberPosition && settings.MainFormPosition != System.Drawing.Point.Empty)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = new PixelPoint(settings.MainFormPosition.X, settings.MainFormPosition.Y);
            }

            if (!settings.SilentRun && !rememberSize && !rememberPosition)
            {
                WindowState = Avalonia.Controls.WindowState.Maximized;
            }
        }

        private void PersistWindowPlacement()
        {
            ApplicationConfig settings = SettingsManager.Settings;
            if (!settings.RememberMainFormSize && !settings.RememberMainFormPosition)
            {
                return;
            }

            if (WindowState != Avalonia.Controls.WindowState.Normal)
            {
                return;
            }

            bool settingsChanged = false;

            if (settings.RememberMainFormSize)
            {
                int width = (int)Math.Round(Width);
                int height = (int)Math.Round(Height);

                if (width >= MinimumPersistedWindowDimension &&
                    height >= MinimumPersistedWindowDimension)
                {
                    var size = new System.Drawing.Size(width, height);
                    if (settings.MainFormSize != size)
                    {
                        settings.MainFormSize = size;
                        settingsChanged = true;
                    }
                }
            }

            if (settings.RememberMainFormPosition)
            {
                var position = new System.Drawing.Point(Position.X, Position.Y);
                if (settings.MainFormPosition != position)
                {
                    settings.MainFormPosition = position;
                    settingsChanged = true;
                }
            }

            if (settingsChanged)
            {
                SettingsManager.SaveApplicationConfig();
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
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
                _taskManager?.TaskCompleted -= HandleTaskCompleted;

                if (task.Info?.Metadata?.Image is { } image && DataContext is MainViewModel vm)
                {
                    int width = image.Width;
                    int height = image.Height;
                    SKBitmap? previewCopy = image.Copy();
                    if (previewCopy == null || previewCopy.Handle == IntPtr.Zero)
                    {
                        previewCopy?.Dispose();
                        XerahS.Common.DebugHelper.WriteLine("Skipped preview update from navbar task completion: failed to clone bitmap.");
                        return;
                    }

                    // UpdatePreview takes ownership and can dispose the supplied bitmap during property-change handling.
                    vm.UpdatePreview(previewCopy);
                    XerahS.Common.DebugHelper.WriteLine($"Updated preview from navbar task completion: {width}x{height}");
                }
            }

            if (_taskManager == null)
            {
                return;
            }

            _taskManager.TaskCompleted += HandleTaskCompleted;

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
                await _taskManager.StartTask(settings, image);
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

    }
}
