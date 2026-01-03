using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ShareX.Ava.UI.ViewModels;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace ShareX.Ava.UI.Views;

public partial class HotkeyEditView : Window
{
    private HotkeyEditViewModel? _viewModel;
    private bool _isRecording = false;
    private IBrush? _originalBackground;
    private static readonly IBrush RecordingBackground = new SolidColorBrush(Color.FromRgb(255, 235, 120));
    private static readonly IBrush RecordingForeground = Brushes.Black;
    
    private Key _previousKey;
    private KeyModifiers _previousModifiers;

    public HotkeyEditView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        LostFocus += OnLostFocus;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _originalBackground = HotkeyButton.Background;

        // Use parsing of events to catch everything
        HotkeyButton.AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        HotkeyButton.AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        
        // Initial update
        UpdateButtonContent();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as HotkeyEditViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            UpdateButtonContent();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HotkeyEditViewModel.KeyText))
        {
            if (!_isRecording)
            {
                UpdateButtonContent();
            }
        }
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_isRecording)
        {
            CancelRecording();
        }
    }

    #region Key Event Handlers

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isRecording || _viewModel == null) return;

        e.Handled = true;

        var key = e.Key;
        var modifiers = e.KeyModifiers;

        // Escape cancels
        if (key == Key.Escape)
        {
            CancelRecording();
            return;
        }
        
        // Backspace/Delete clears
        if (key == Key.Back || key == Key.Delete)
        {
            ClearHotkey();
            return;
        }

        // Modifier only?
        if (IsModifierKey(key))
        {
            UpdateRecordingDisplay(modifiers);
            return;
        }

        // Commit
        CommitHotkey(key, modifiers);
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (!_isRecording || _viewModel == null) return;
        
        e.Handled = true;

        // PrintScreen/Snapshot often triggers on KeyUp
        if (e.Key == Key.PrintScreen || e.Key == Key.Snapshot)
        {
            CommitHotkey(e.Key, e.KeyModifiers);
        }
    }

    #endregion

    #region Recording Logic

    private void HotkeyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isRecording)
        {
            CancelRecording();
        }
        else
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        if (_viewModel == null) return;

        _isRecording = true;
        _previousKey = _viewModel.SelectedKey;
        _previousModifiers = _viewModel.SelectedModifiers;

        // Visuals
        HotkeyButton.Background = RecordingBackground;
        HotkeyButton.Foreground = RecordingForeground;
        HotkeyButton.Content = "Press a key...";
        
        // Focus
        HotkeyButton.Focus();

        // Disable global hotkeys
        if (Application.Current is App app && app.HotkeyManager != null)
        {
            app.HotkeyManager.IgnoreHotkeys = true;
        }
    }

    private void StopRecording()
    {
        _isRecording = false;

        // Restore globals
        if (Application.Current is App app && app.HotkeyManager != null)
        {
            app.HotkeyManager.IgnoreHotkeys = false;
        }

        // Restore visuals
        HotkeyButton.Background = _originalBackground ?? Brushes.Transparent;
        HotkeyButton.ClearValue(Button.ForegroundProperty);

        UpdateButtonContent();
    }

    private void CancelRecording()
    {
        if (_viewModel != null)
        {
            _viewModel.SelectedKey = _previousKey;
            _viewModel.SelectedModifiers = _previousModifiers;
        }
        StopRecording();
    }

    private void CommitHotkey(Key key, KeyModifiers modifiers)
    {
        if (_viewModel == null) return;

        if (key == Key.None || IsModifierKey(key)) return;

        _viewModel.SelectedKey = key;
        _viewModel.SelectedModifiers = modifiers;
        
        StopRecording();
    }

    private void ClearHotkey()
    {
        if (_viewModel != null)
        {
            _viewModel.SelectedKey = Key.None;
            _viewModel.SelectedModifiers = KeyModifiers.None;
        }
        StopRecording();
    }

    private void UpdateRecordingDisplay(KeyModifiers modifiers)
    {
        var parts = new System.Collections.Generic.List<string>();
        
        if (modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");
        
        if (parts.Count > 0)
        {
            HotkeyButton.Content = string.Join(" + ", parts) + " + ...";
        }
        else
        {
            HotkeyButton.Content = "Press a key...";
        }
    }

    private void UpdateButtonContent()
    {
        if (_viewModel != null)
        {
            HotkeyButton.Content = _viewModel.KeyText;
        }
    }

    private bool IsModifierKey(Key key)
    {
        return key == Key.LeftCtrl || key == Key.RightCtrl ||
               key == Key.LeftAlt || key == Key.RightAlt ||
               key == Key.LeftShift || key == Key.RightShift ||
               key == Key.LWin || key == Key.RWin ||
               key == Key.DeadCharProcessed;
    }

    #endregion

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Save();
            Close(true);
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
