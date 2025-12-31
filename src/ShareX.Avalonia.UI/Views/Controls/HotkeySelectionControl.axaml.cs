using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ShareX.Avalonia.Core;
using ShareX.Avalonia.Core.Hotkeys;
using ShareX.Avalonia.Common;
using ShareX.Avalonia.UI.ViewModels;
using System;
using System.Linq;

namespace ShareX.Avalonia.UI.Views.Controls;

public partial class HotkeySelectionControl : UserControl
{
    private bool _isEditing;
    private HotkeyItemViewModel? _viewModel;
    private IBrush? _originalBackground;
    
    // Yellow/amber color for editing mode
    private static readonly IBrush EditingBackground = new SolidColorBrush(Color.FromRgb(255, 235, 120));

    public HotkeySelectionControl()
    {
        InitializeComponent();
        
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Store original background
        _originalBackground = HotkeyButton.Background;
        
        // Use AddHandler with handledEventsToo to capture KeyDown/KeyUp
        // even when handled by button internal logic
        HotkeyButton.AddHandler(
            KeyDownEvent, 
            HotkeyButton_KeyDown, 
            RoutingStrategies.Tunnel, 
            handledEventsToo: true);
        
        HotkeyButton.AddHandler(
            KeyUpEvent, 
            HotkeyButton_KeyUp, 
            RoutingStrategies.Tunnel, 
            handledEventsToo: true);
            
        // Also handle on the UserControl itself for better capture
        this.AddHandler(
            KeyDownEvent,
            OnUserControlKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as HotkeyItemViewModel;
        
        if (_viewModel != null)
        {
            PopulateTaskMenu();
        }
    }

    private void PopulateTaskMenu()
    {
        if (TaskContextMenu == null || _viewModel == null) return;
        
        TaskContextMenu.Items.Clear();
        
        var hotkeyTypes = Enum.GetValues(typeof(HotkeyType)).Cast<HotkeyType>();
        
        foreach (var hotkeyType in hotkeyTypes)
        {
            var menuItem = new MenuItem
            {
                Header = EnumExtensions.GetDescription(hotkeyType),
                Tag = hotkeyType
            };
            
            menuItem.Click += (s, e) =>
            {
                if (_viewModel != null && s is MenuItem mi && mi.Tag is HotkeyType type)
                {
                    _viewModel.Model.Job = type;
                    _viewModel.Refresh();
                }
            };
            
            TaskContextMenu.Items.Add(menuItem);
        }
    }

    private void TaskButton_Click(object? sender, RoutedEventArgs e)
    {
        TaskContextMenu?.Open(TaskButton);
    }

    private void HotkeyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isEditing)
        {
            // Click while editing stops editing
            StopEditing();
        }
        else
        {
            StartEditing();
        }
    }
    
    private void OnUserControlKeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        // Forward to button handler if we're in editing mode
        if (_isEditing)
        {
            HotkeyButton_KeyDown(sender, e);
        }
    }

    private void HotkeyButton_KeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        if (!_isEditing || _viewModel == null) return;
        
        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            // Cancel editing without saving
            _viewModel.Model.HotkeyInfo.Key = Key.None;
            _viewModel.Model.HotkeyInfo.Modifiers = KeyModifiers.None;
            StopEditing();
            return;
        }

        // Skip modifier-only keys - just update display
        if (IsModifierKey(e.Key))
        {
            // Update display to show modifiers being pressed
            _viewModel.Model.HotkeyInfo.Modifiers = e.KeyModifiers;
            _viewModel.Refresh();
            UpdateButtonDisplay();
            return;
        }

        // Capture the key combination
        _viewModel.Model.HotkeyInfo.Key = e.Key;
        _viewModel.Model.HotkeyInfo.Modifiers = e.KeyModifiers;
        
        StopEditing();
    }

    private void HotkeyButton_KeyUp(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        if (!_isEditing || _viewModel == null) return;
        
        e.Handled = true;

        // PrintScreen doesn't trigger KeyDown, only KeyUp
        if (e.Key == Key.PrintScreen)
        {
            _viewModel.Model.HotkeyInfo.Key = e.Key;
            _viewModel.Model.HotkeyInfo.Modifiers = e.KeyModifiers;
            StopEditing();
        }
    }

    private void StartEditing()
    {
        if (_viewModel == null) return;
        
        _isEditing = true;
        
        // Temporarily disable global hotkeys
        if (global::Avalonia.Application.Current is App app && app.HotkeyManager != null)
        {
            app.HotkeyManager.IgnoreHotkeys = true;
        }

        // Clear current hotkey
        _viewModel.Model.HotkeyInfo.Key = Key.None;
        _viewModel.Model.HotkeyInfo.Modifiers = KeyModifiers.None;
        _viewModel.Refresh();
        
        // Update button appearance to indicate editing mode (yellowish)
        HotkeyButton.Background = EditingBackground;
        HotkeyButton.Foreground = Brushes.Black;
        HotkeyButton.Content = "Press a key...";
        
        // Ensure button has focus to receive key events
        HotkeyButton.Focus();
    }

    private void StopEditing()
    {
        _isEditing = false;
        
        // Re-enable global hotkeys
        if (global::Avalonia.Application.Current is App app && app.HotkeyManager != null)
        {
            app.HotkeyManager.IgnoreHotkeys = false;
            
            // Re-register the hotkey with the new binding
            if (_viewModel != null)
            {
                app.HotkeyManager.RegisterHotkey(_viewModel.Model);
            }
        }

        // Restore button appearance
        HotkeyButton.Background = _originalBackground ?? Brushes.Transparent;
        HotkeyButton.ClearValue(Button.ForegroundProperty);
        
        _viewModel?.Refresh();
        UpdateButtonDisplay();
        
        // Raise event for parent to update
        OnHotkeyChanged();
    }
    
    private void UpdateButtonDisplay()
    {
        if (_viewModel != null)
        {
            HotkeyButton.Content = _viewModel.KeyString;
        }
    }

    private bool IsModifierKey(Key key)
    {
        return key == Key.LeftCtrl || key == Key.RightCtrl ||
               key == Key.LeftAlt || key == Key.RightAlt ||
               key == Key.LeftShift || key == Key.RightShift ||
               key == Key.LWin || key == Key.RWin;
    }

    private void EditButton_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: Open TaskSettings editor dialog
        // For now, just open the task menu as a placeholder
        TaskContextMenu?.Open(TaskButton);
    }

    public event EventHandler? HotkeyChanged;
    public event EventHandler? Selected;

    protected virtual void OnHotkeyChanged()
    {
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnSelected()
    {
        Selected?.Invoke(this, EventArgs.Empty);
    }
}
