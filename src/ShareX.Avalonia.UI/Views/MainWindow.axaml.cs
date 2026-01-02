using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ShareX.Ava.UI.ViewModels;
using ShareX.Ava.Annotations.Models;
using FluentAvalonia.UI.Controls;

namespace ShareX.Ava.UI.Views
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
            this.WindowState = WindowState.Maximized;
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
            
            if (contentFrame != null && selectedItem != null)
            {
                var tag = selectedItem.Tag?.ToString();
                
                switch (tag)
                {
                    case "Editor":
                        if (_editorView == null) _editorView = new EditorView();
                        contentFrame.Content = _editorView;
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
                    case "Settings_Task":
                        contentFrame.Content = new TaskSettingsView();
                        break;
                    case "Settings_Hotkey":
                        contentFrame.Content = new HotkeySettingsView();
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
            
            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Maximized;
            }
            
            this.Activate();
            this.Focus();
        }
    }
}
