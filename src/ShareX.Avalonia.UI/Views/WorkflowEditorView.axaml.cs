using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class WorkflowEditorView : Window
{
    public WorkflowEditorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private bool _isRecording = false;

    private void HotkeyButton_Click(object? sender, RoutedEventArgs e)
    {
        _isRecording = true;
        if (sender is Button btn)
        {
            btn.Content = "Press keys...";
        }
        this.KeyDown += OnKeyDown;
        this.KeyUp += OnKeyUp; // Optional clean up
    }

    private void OnKeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        if (!_isRecording) return;

        e.Handled = true;

        // Ignore modifier-only events for "finalizing" but track them
        if (IsModifier(e.Key)) return;

        if (e.Key == global::Avalonia.Input.Key.Escape)
        {
            StopRecording();
            // Maybe clear?
            return;
        }

        if (DataContext is WorkflowEditorViewModel vm)
        {
            vm.SelectedKey = e.Key;
            vm.SelectedModifiers = e.KeyModifiers;
        }

        StopRecording();
    }

    private void OnKeyUp(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        // usually irrelevant for simple hotkey
    }

    private void StopRecording()
    {
        _isRecording = false;
        this.KeyDown -= OnKeyDown;
        this.KeyUp -= OnKeyUp;
        if (DataContext is WorkflowEditorViewModel vm)
        {
            // Trigger property change to refresh text if needed
            // But VM.KeyText should update automatically via binding
        }
    }

    private bool IsModifier(global::Avalonia.Input.Key key)
    {
        return key == global::Avalonia.Input.Key.LeftCtrl ||
               key == global::Avalonia.Input.Key.RightCtrl ||
               key == global::Avalonia.Input.Key.LeftAlt ||
               key == global::Avalonia.Input.Key.RightAlt ||
               key == global::Avalonia.Input.Key.LeftShift ||
               key == global::Avalonia.Input.Key.RightShift ||
               key == global::Avalonia.Input.Key.LWin ||
               key == global::Avalonia.Input.Key.RWin;
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowEditorViewModel vm)
        {
            vm.Save();
            Close(true);
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
