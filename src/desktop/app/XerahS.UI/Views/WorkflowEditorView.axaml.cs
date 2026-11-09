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
using Avalonia;
using System.ComponentModel;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using XerahS.Common;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class WorkflowEditorView : UserControl
{
    public WorkflowEditorView()
    {
        DebugHelper.WriteLine("[WorkflowEditorView] ctor start");
        InitializeComponent();
        DebugHelper.WriteLine("[WorkflowEditorView] InitializeComponent completed");

        AttachedToVisualTree += (_, _) =>
        {
            if (_taskSettings != null)
            {
                _taskSettings.PropertyChanged -= OnTaskSettingsChanged;
                _taskSettings.PropertyChanged += OnTaskSettingsChanged;
            }
        };
        DetachedFromVisualTree += (_, _) =>
        {
            StopRecording();
            if (_taskSettings != null) _taskSettings.PropertyChanged -= OnTaskSettingsChanged;
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private bool _isRecording = false;
    private Button? _recordingButton;
    private TaskSettingsViewModel? _taskSettings;

    protected override void OnDataContextChanged(EventArgs e)
    {
        StopRecording();
        if (_taskSettings != null) _taskSettings.PropertyChanged -= OnTaskSettingsChanged;
        base.OnDataContextChanged(e);
        _taskSettings = (DataContext as WorkflowEditorViewModel)?.TaskSettings;
        if (_taskSettings != null) _taskSettings.PropertyChanged += OnTaskSettingsChanged;
        this.FindControl<TabControl>("WorkflowSections")?.SetCurrentValue(TabControl.SelectedIndexProperty, 0);
    }

    private void OnTaskSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TaskSettingsViewModel.Job)) return;
        // A job change may remove the currently selected image/video section.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var tabs = this.FindControl<TabControl>("WorkflowSections");
            if (tabs?.SelectedItem is TabItem { IsVisible: false }) tabs.SelectedIndex = 0;
        });
    }

    private void HotkeyButton_Click(object? sender, RoutedEventArgs e)
    {
        StopRecording();
        _isRecording = true;
        if (sender is Button btn)
        {
            _recordingButton = btn;
            btn.SetCurrentValue(ContentControl.ContentProperty, "Press keys...");
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
            _recordingButton?.SetCurrentValue(ContentControl.ContentProperty, vm.KeyText);
        }
        _recordingButton = null;
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
            vm.CloseRequested?.Invoke(true);
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowEditorViewModel vm)
        {
            vm.CloseRequested?.Invoke(false);
        }
    }
}
