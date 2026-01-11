using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using XerahS.Common;
using XerahS.Core;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views.Controls;

/// <summary>
/// A control for capturing and displaying hotkey combinations.
/// Supports Normal and Recording modes as per user specification.
/// </summary>
public partial class HotkeySelectionControl : UserControl
{
    // Static debug log - writes to Debug output and collects in list
    private static Action<string>? _debugLog;
    private static readonly System.Collections.Generic.List<string> _debugMessages = new();

    public static void SetDebugCallback(Action<string> callback)
    {
        _debugLog = (msg) =>
        {
            _debugMessages.Add(msg);
            XerahS.Common.DebugHelper.WriteLine($"[Hotkey] {msg}");
            callback(msg);
        };
    }

    public static void Log(string message)
    {
        var time = DateTime.Now.ToString("HH:mm:ss.fff");
        var formattedMsg = $"[{time}] {message}";
        // Also log to DebugHelper for file logging
        XerahS.Common.DebugHelper.WriteLine($"[Hotkey] {message}");
        _debugLog?.Invoke(formattedMsg);
    }

    public static string GetDebugLog() => string.Join("\n", _debugMessages);


    private enum ControlMode
    {
        Normal,
        Recording
    }

    private ControlMode _mode = ControlMode.Normal;
    private HotkeyItemViewModel? _viewModel;
    private IBrush? _originalBackground;
    private Key _previousKey;
    private KeyModifiers _previousModifiers;

    // Visual feedback colors
    private static readonly IBrush RecordingBackground = new SolidColorBrush(Color.FromRgb(255, 235, 120));
    private static readonly IBrush RecordingForeground = Brushes.Black;

    public HotkeySelectionControl()
    {
        InitializeComponent();

        Log("HotkeySelectionControl: Constructor called");

        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        LostFocus += OnLostFocus;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Log("OnLoaded: START");

        _originalBackground = HotkeyButton.Background;

        // Set up debug logger if not already set - use static list for simplicity
        if (_debugLog == null)
        {
            // Use a static StringBuilder that can be read later
            _debugLog = (msg) =>
            {
                _debugMessages.Add(msg);
                System.Diagnostics.Debug.WriteLine($"[HotkeyDebug] {msg}");
            };
            Log("Debug logger initialized (check Debug output and static _debugMessages)");
        }

        // CRITICAL FIX: Add handlers directly to HotkeyButton (where focus is)
        // Use both Tunnel and Bubble strategies to catch events in all phases
        HotkeyButton.AddHandler(
            KeyDownEvent,
            OnPreviewKeyDown,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);

        HotkeyButton.AddHandler(
            KeyUpEvent,
            OnPreviewKeyUp,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);

        Log($"OnLoaded: END - HotkeyButton={HotkeyButton != null}");

        // Handle selection even if children (Buttons) handle the event
        this.AddHandler(PointerPressedEvent, OnControlPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
    }

    private void OnControlPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Find the parent ListBoxItem and select it
        if (this.FindAncestorOfType<ListBoxItem>() is ListBoxItem listBoxItem)
        {
            if (!listBoxItem.IsSelected)
            {
                listBoxItem.IsSelected = true;
                // We don't mark as handled because we want the button to still work (e.g. open menu)
            }
        }
    }

    // Remove the old handler if it exists or keep it for XAML compatibility if I don't remove it from XAML yet
    // I will remove the XAML attribute in next step. For now I name this differently.


    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as HotkeyItemViewModel;

        if (_viewModel != null)
        {
            PopulateTaskMenu();
        }
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        // Cancel recording if focus is lost
        if (_mode == ControlMode.Recording)
        {
            Log("OnLostFocus: Recording mode active, canceling");
            CancelRecording();
        }
    }

    #region Key Event Handlers

    private void OnPreviewKeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        Log($"OnPreviewKeyDown: Key={e.Key}, Mods={e.KeyModifiers}, Mode={_mode}");

        if (_mode != ControlMode.Recording || _viewModel == null)
        {
            Log("OnPreviewKeyDown: Ignoring - not in recording mode or viewModel null");
            return;
        }

        // Mark as handled to prevent bubbling
        e.Handled = true;

        var key = e.Key;
        var modifiers = e.KeyModifiers;

        // Escape cancels recording
        if (key == Key.Escape)
        {
            CancelRecording();
            return;
        }

        // Backspace or Delete clears the hotkey
        if (key == Key.Back || key == Key.Delete)
        {
            ClearHotkey();
            return;
        }

        // Check if this is a modifier-only key
        if (IsModifierKey(key))
        {
            // Update display to show live modifier preview
            UpdateRecordingDisplay(modifiers);
            return;
        }

        // Non-modifier key pressed - commit the combination
        CommitHotkey(key, modifiers);
    }

    private void OnPreviewKeyUp(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        if (_mode != ControlMode.Recording || _viewModel == null) return;

        e.Handled = true;

        // PrintScreen and some media keys only fire on KeyUp
        if (e.Key == Key.PrintScreen || e.Key == Key.Snapshot)
        {
            CommitHotkey(e.Key, e.KeyModifiers);
        }
    }

    #endregion

    #region Recording State Machine

    private void StartRecording()
    {
        Log("StartRecording: ENTERED");

        // Set mode and visual feedback FIRST, before any checks
        _mode = ControlMode.Recording;
        Log("StartRecording: Mode set to Recording");

        // Visual feedback: yellow background - MUST happen even if ViewModel is null
        HotkeyButton.Background = RecordingBackground;
        HotkeyButton.Foreground = RecordingForeground;
        HotkeyButton.Content = "Press a key...";
        Log("StartRecording: Visual feedback set (yellow)");

        // Take keyboard focus - critical for capturing keys
        HotkeyButton.Focusable = true;
        HotkeyButton.Focus();
        Log($"StartRecording: Focus set, IsFocused={HotkeyButton.IsFocused}");

        // Now handle ViewModel-specific logic
        if (_viewModel != null)
        {
            _previousKey = _viewModel.Model.HotkeyInfo.Key;
            _previousModifiers = _viewModel.Model.HotkeyInfo.Modifiers;
            Log($"StartRecording: Saved previous key={_previousKey}, mods={_previousModifiers}");

            // Set status to Recording so the indicator dot turns yellow
            _viewModel.Model.HotkeyInfo.Status = Platform.Abstractions.HotkeyStatus.Recording;
            _viewModel.Refresh();
        }
        else
        {
            Log("StartRecording: WARNING - _viewModel is NULL");
        }

        // Disable global hotkeys while recording
        if (global::Avalonia.Application.Current is App app && app.WorkflowManager != null)
        {
            app.WorkflowManager.IgnoreHotkeys = true;
            Log("StartRecording: Global hotkeys disabled");
        }

        Log("StartRecording: COMPLETED");
    }

    private void StopRecording()
    {
        Log("StopRecording: ENTERED");
        _mode = ControlMode.Normal;
        Log("StopRecording: Mode set to Normal");

        // Re-enable global hotkeys
        if (global::Avalonia.Application.Current is App app && app.WorkflowManager != null)
        {
            app.WorkflowManager.IgnoreHotkeys = false;
            Log("StopRecording: Global hotkeys re-enabled");

            // Re-register the hotkey with new binding
            if (_viewModel != null)
            {
                var key = _viewModel.Model.HotkeyInfo.Key;
                var mods = _viewModel.Model.HotkeyInfo.Modifiers;
                var id = _viewModel.Model.HotkeyInfo.Id;
                Log($"StopRecording: Attempting to register hotkey: {_viewModel.Model.HotkeyInfo}");
                Log($"StopRecording: Details - Key={key}, Modifiers={mods}, Id={id}");
                var success = app.WorkflowManager.RegisterHotkey(_viewModel.Model);
                Log($"StopRecording: RegisterHotkey returned: {success}, Status: {_viewModel.Model.HotkeyInfo.Status}");
            }
            else
            {
                Log("StopRecording: WARNING - _viewModel is NULL, cannot register");
            }
        }
        else
        {
            Log("StopRecording: WARNING - HotkeyManager not available");
        }

        // Restore visual appearance
        HotkeyButton.Background = _originalBackground ?? Brushes.Transparent;
        HotkeyButton.ClearValue(Button.ForegroundProperty);

        _viewModel?.Refresh();
        UpdateButtonContent();
        Log($"StopRecording: Button content updated to: {HotkeyButton.Content}");

        OnHotkeyChanged();
        Log("StopRecording: COMPLETED");
    }

    private void CancelRecording()
    {
        if (_viewModel != null)
        {
            // Restore previous value
            _viewModel.Model.HotkeyInfo.Key = _previousKey;
            _viewModel.Model.HotkeyInfo.Modifiers = _previousModifiers;
        }

        StopRecording();
    }

    private void CommitHotkey(Key key, KeyModifiers modifiers)
    {
        if (_viewModel == null) return;

        // Validate: must not be modifier-only
        if (key == Key.None || IsModifierKey(key))
        {
            // Reject - don't commit
            return;
        }

        _viewModel.Model.HotkeyInfo.Key = key;
        _viewModel.Model.HotkeyInfo.Modifiers = modifiers;

        StopRecording();
    }

    private void ClearHotkey()
    {
        if (_viewModel != null)
        {
            _viewModel.Model.HotkeyInfo.Key = Key.None;
            _viewModel.Model.HotkeyInfo.Modifiers = KeyModifiers.None;
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

    #endregion

    #region Event Handlers

    private void HotkeyButton_Click(object? sender, RoutedEventArgs e)
    {
        Log($"HotkeyButton_Click: FIRED - current mode={_mode}");

        if (_mode == ControlMode.Recording)
        {
            Log("HotkeyButton_Click: Already recording, canceling");
            CancelRecording();
        }
        else
        {
            Log("HotkeyButton_Click: Starting recording");
            StartRecording();
        }
    }

    private void TaskButton_Click(object? sender, RoutedEventArgs e)
    {
        TaskContextMenu?.Open(TaskButton);
    }

    private void EditButton_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: Open TaskSettings editor dialog
        TaskContextMenu?.Open(TaskButton);
    }

    #endregion

    #region Helpers

    private void UpdateButtonContent()
    {
        if (_viewModel != null)
        {
            var info = _viewModel.Model.HotkeyInfo;
            if (info.IsValid)
            {
                HotkeyButton.Content = info.ToString();
            }
            else
            {
                HotkeyButton.Content = "None";
            }
        }
    }

    private bool IsModifierKey(Key key)
    {
        return key == Key.LeftCtrl || key == Key.RightCtrl ||
               key == Key.LeftAlt || key == Key.RightAlt ||
               key == Key.LeftShift || key == Key.RightShift ||
               key == Key.LWin || key == Key.RWin ||
               key == Key.DeadCharProcessed; // Also skip this pseudo-key
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

    #endregion

    #region Events

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

    #endregion
}

/// <summary>
/// Keyboard focus helper for Avalonia
/// </summary>
internal static class Keyboard
{
    public static void Focus(Control control)
    {
        control.Focus(NavigationMethod.Directional);
    }
}
