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

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using XerahS.Platform.Abstractions;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views;

public partial class ScrollingCaptureWindow : Window
{
    private static readonly HotkeyInfo s_escapeStopHotkey = new(Key.Escape);
    private bool _escapeHotkeyRegistered;

    public ScrollingCaptureWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is ScrollingCaptureViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        UnregisterEscapeStopHotkey();
        if (DataContext is ScrollingCaptureViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
        (DataContext as ScrollingCaptureViewModel)?.Cleanup();
        base.OnClosing(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ScrollingCaptureViewModel.IsCapturing))
        {
            return;
        }

        if (DataContext is ScrollingCaptureViewModel vm)
        {
            if (vm.IsCapturing)
            {
                RegisterEscapeStopHotkey();
            }
            else
            {
                UnregisterEscapeStopHotkey();
            }
        }
    }

    private void RegisterEscapeStopHotkey()
    {
        if (_escapeHotkeyRegistered || !PlatformServices.IsInitialized)
        {
            return;
        }

        try
        {
            if (PlatformServices.Hotkey.RegisterHotkey(s_escapeStopHotkey))
            {
                _escapeHotkeyRegistered = true;
                PlatformServices.Hotkey.HotkeyTriggered += OnHotkeyTriggered;
            }
        }
        catch
        {
            // Ignore - hotkey may not be supported on this platform
        }
    }

    private void UnregisterEscapeStopHotkey()
    {
        if (!_escapeHotkeyRegistered || !PlatformServices.IsInitialized)
        {
            return;
        }

        try
        {
            PlatformServices.Hotkey.HotkeyTriggered -= OnHotkeyTriggered;
            PlatformServices.Hotkey.UnregisterHotkey(s_escapeStopHotkey);
        }
        finally
        {
            _escapeHotkeyRegistered = false;
        }
    }

    private void OnHotkeyTriggered(object? sender, HotkeyTriggeredEventArgs e)
    {
        if (e.HotkeyInfo.Key != Key.Escape || DataContext is not ScrollingCaptureViewModel vm || !vm.IsCapturing)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => vm.StopCapture());
    }
}
