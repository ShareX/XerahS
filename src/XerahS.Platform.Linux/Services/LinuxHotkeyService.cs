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

using Avalonia.Input;
using global::Avalonia.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using XerahS.Common;
using XerahS.Platform.Abstractions;
using HotkeyStatus = XerahS.Platform.Abstractions.HotkeyStatus;

namespace XerahS.Platform.Linux.Services;

public sealed class LinuxHotkeyService : IHotkeyService
{
    private readonly IntPtr _display;
    private readonly IntPtr _rootWindow;
    private readonly Thread? _eventThread;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Dictionary<ushort, HotkeyRegistration> _registrations = new();
    private readonly object _lock = new();
    private ushort _nextId = 1;
    private bool _isDisposed;

    // X error handling - must be static for the delegate to work with P/Invoke
    private static bool _grabError;
    private static readonly NativeMethods.XErrorHandler _errorHandler = HandleXError;

    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;
    public bool IsSuspended { get; set; }

    public LinuxHotkeyService()
    {
        _display = NativeMethods.XOpenDisplay(null);
        if (_display == IntPtr.Zero)
        {
            DebugHelper.WriteLine("LinuxHotkeyService: Unable to open X display; hotkeys disabled.");
            return;
        }

        _rootWindow = NativeMethods.XDefaultRootWindow(_display);
        NativeMethods.XSelectInput(_display, _rootWindow, NativeMethods.KeyPressMask);

        _eventThread = new Thread(EventLoop)
        {
            IsBackground = true,
            Name = "LinuxHotkeyEventLoop"
        };
        _eventThread.Start();
    }

    private void EventLoop()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            if (_display == IntPtr.Zero)
            {
                Thread.Sleep(100);
                continue;
            }

            if (NativeMethods.XPending(_display) > 0)
            {
                _ = NativeMethods.XNextEvent(_display, out var xevent);
                if (xevent.type == NativeMethods.KeyPress)
                {
                    try
                    {
                        HandleKeyPress(xevent.key);
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteException(ex, "LinuxHotkeyService: Exception in event loop");
                    }
                }
                continue;
            }

            Thread.Sleep(10);
        }
    }

    private void HandleKeyPress(XKeyEvent keyEvent)
    {
        if (IsSuspended)
        {
            return;
        }

        HotkeyInfo? triggered = null;
        lock (_lock)
        {
            foreach (var registration in _registrations.Values)
            {
                if (registration.Keycode != keyEvent.keycode)
                {
                    continue;
                }

                if ((keyEvent.state & registration.BaseModifierMask) == registration.BaseModifierMask)
                {
                    triggered = registration.Info;
                    break;
                }
            }
        }

        if (triggered != null)
        {
            var args = new HotkeyTriggeredEventArgs(triggered);
            Dispatcher.UIThread.Post(() =>
            {
                HotkeyTriggered?.Invoke(this, args);
            });
        }
    }

    public bool RegisterHotkey(HotkeyInfo hotkeyInfo)
    {
        if (!hotkeyInfo.IsValid)
        {
            hotkeyInfo.Status = HotkeyStatus.NotConfigured;
            return false;
        }

        if (_display == IntPtr.Zero)
        {
            hotkeyInfo.Status = HotkeyStatus.UnsupportedPlatform;
            return false;
        }

        lock (_lock)
        {
            if (hotkeyInfo.Id == 0)
            {
                hotkeyInfo.Id = _nextId++;
            }

            int keycode = GetKeycode(hotkeyInfo.Key);
            if (keycode == 0)
            {
                hotkeyInfo.Status = HotkeyStatus.Failed;
                DebugHelper.WriteLine($"LinuxHotkeyService: Unable to map key {hotkeyInfo.Key}");
                return false;
            }

            var baseMask = GetModifierMask(hotkeyInfo.Modifiers);
            var registration = new HotkeyRegistration(hotkeyInfo.Id, keycode, baseMask, hotkeyInfo);

            var (success, message) = TryGrab(registration);
            if (!success)
            {
                hotkeyInfo.Status = HotkeyStatus.Failed;
                DebugHelper.WriteLine($"LinuxHotkeyService: Failed to grab {hotkeyInfo}. {message}");
                return false;
            }

            _registrations[hotkeyInfo.Id] = registration;
            hotkeyInfo.Status = HotkeyStatus.Registered;
            return true;
        }
    }

    public bool UnregisterHotkey(HotkeyInfo hotkeyInfo)
    {
        if (_display == IntPtr.Zero || hotkeyInfo.Id == 0)
        {
            hotkeyInfo.Status = HotkeyStatus.NotConfigured;
            return false;
        }

        lock (_lock)
        {
            if (!_registrations.TryGetValue(hotkeyInfo.Id, out var registration))
            {
                hotkeyInfo.Status = HotkeyStatus.NotConfigured;
                return false;
            }

            foreach (var mask in registration.GrabMasks)
            {
                NativeMethods.XUngrabKey(_display, registration.Keycode, mask, _rootWindow);
            }

            NativeMethods.XFlush(_display);
            _registrations.Remove(hotkeyInfo.Id);
            hotkeyInfo.Status = HotkeyStatus.NotConfigured;
            return true;
        }
    }

    public void UnregisterAll()
    {
        if (_display == IntPtr.Zero)
        {
            return;
        }

        lock (_lock)
        {
            foreach (var registration in _registrations.Values)
            {
                foreach (var mask in registration.GrabMasks)
                {
                    NativeMethods.XUngrabKey(_display, registration.Keycode, mask, _rootWindow);
                }
            }

            NativeMethods.XFlush(_display);
            _registrations.Clear();
        }
    }

    public bool IsRegistered(HotkeyInfo hotkeyInfo)
    {
        lock (_lock)
        {
            return hotkeyInfo.Id != 0 && _registrations.ContainsKey(hotkeyInfo.Id);
        }
    }

    private static int HandleXError(IntPtr display, ref XErrorEvent error)
    {
        if (error.error_code == NativeMethods.BadAccess)
        {
            _grabError = true;
        }

        return 0;
    }

    private (bool success, string? error) TryGrab(HotkeyRegistration registration)
    {
        var grabbed = new List<uint>();

        // Install our error handler to catch BadAccess errors
        IntPtr previousHandler = NativeMethods.XSetErrorHandler(_errorHandler);

        try
        {
            foreach (var mask in registration.GrabMasks)
            {
                _grabError = false;

                _ = NativeMethods.XGrabKey(_display, registration.Keycode, mask, _rootWindow, false, NativeMethods.GrabModeAsync, NativeMethods.GrabModeAsync);

                // Sync to ensure any errors are processed before we check
                NativeMethods.XSync(_display, false);

                if (_grabError)
                {
                    foreach (var rollbackMask in grabbed)
                    {
                        NativeMethods.XUngrabKey(_display, registration.Keycode, rollbackMask, _rootWindow);
                    }

                    NativeMethods.XSync(_display, false);
                    return (false, "Key combination is already grabbed by another application");
                }

                grabbed.Add(mask);
            }

            return (true, null);
        }
        finally
        {
            // Restore the previous error handler
            NativeMethods.XSetErrorHandlerPtr(previousHandler);
        }
    }

    private static uint GetModifierMask(KeyModifiers modifiers)
    {
        uint mask = 0;
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            mask |= NativeMethods.ControlMask;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            mask |= NativeMethods.ShiftMask;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            mask |= NativeMethods.Mod1Mask;
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            mask |= NativeMethods.Mod4Mask;
        }

        return mask;
    }

    private int GetKeycode(Key key)
    {
        var keysym = ConvertKeyToKeysym(key);
        if (keysym == IntPtr.Zero)
        {
            return 0;
        }

        var keycode = NativeMethods.XKeysymToKeycode(_display, keysym);
        if (keycode == 0)
        {
            return 0;
        }

        return keycode;
    }

    private static IntPtr ConvertKeyToKeysym(Key key)
    {
        if (SpecialKeyNames.TryGetValue(key, out var symbol))
        {
            return NativeMethods.XStringToKeysym(symbol);
        }

        if (key >= Key.A && key <= Key.Z)
        {
            return NativeMethods.XStringToKeysym(key.ToString());
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return NativeMethods.XStringToKeysym($"{(int)(key - Key.D0)}");
        }

        if (key >= Key.F1 && key <= Key.F24)
        {
            return NativeMethods.XStringToKeysym(key.ToString());
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return NativeMethods.XStringToKeysym("KP_" + (int)(key - Key.NumPad0));
        }

        return IntPtr.Zero;
    }

    private static readonly Dictionary<Key, string> SpecialKeyNames = new()
    {
        { Key.PrintScreen, "Print" },
        { Key.Scroll, "Scroll_Lock" },
        { Key.Pause, "Pause" },
        { Key.CapsLock, "Caps_Lock" },
        { Key.Space, "space" },
        { Key.Tab, "Tab" },
        { Key.Return, "Return" },  // Key.Enter is the same value as Key.Return in Avalonia
        { Key.Back, "BackSpace" },
        { Key.Escape, "Escape" },
        { Key.Delete, "Delete" },
        { Key.Insert, "Insert" },
        { Key.Home, "Home" },
        { Key.End, "End" },
        { Key.PageUp, "Page_Up" },
        { Key.PageDown, "Page_Down" },
        { Key.Left, "Left" },
        { Key.Right, "Right" },
        { Key.Up, "Up" },
        { Key.Down, "Down" },
        { Key.NumLock, "Num_Lock" },
        { Key.OemPlus, "plus" },
        { Key.OemMinus, "minus" },
        { Key.OemComma, "comma" },
        { Key.OemPeriod, "period" },
        { Key.Oem1, "semicolon" },
        { Key.Oem2, "slash" },
        { Key.Oem3, "grave" },
        { Key.Oem4, "bracketleft" },
        { Key.Oem5, "backslash" },
        { Key.Oem6, "bracketright" },
        { Key.Oem7, "apostrophe" },
        { Key.Apps, "Menu" },
        { Key.Divide, "KP_Divide" },
        { Key.Multiply, "KP_Multiply" },
        { Key.Add, "KP_Add" },
        { Key.Subtract, "KP_Subtract" },
        { Key.Decimal, "KP_Decimal" }
    };

    private sealed class HotkeyRegistration
    {
        public ushort Id { get; }
        public int Keycode { get; }
        public uint BaseModifierMask { get; }
        public IReadOnlyList<uint> GrabMasks { get; }
        public HotkeyInfo Info { get; }

        public HotkeyRegistration(ushort id, int keycode, uint baseModifierMask, HotkeyInfo info)
        {
            Id = id;
            Keycode = keycode;
            BaseModifierMask = baseModifierMask;
            Info = info;
            GrabMasks = BuildModifierMasks(baseModifierMask);
        }

        private static IReadOnlyList<uint> BuildModifierMasks(uint baseMask)
        {
            var masks = new HashSet<uint> { baseMask };
            masks.Add(baseMask | NativeMethods.LockMask);
            masks.Add(baseMask | NativeMethods.Mod2Mask);
            masks.Add(baseMask | NativeMethods.LockMask | NativeMethods.Mod2Mask);
            return new List<uint>(masks);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _cancellation.Cancel();
        _eventThread?.Join(500);
        UnregisterAll();

        if (_display != IntPtr.Zero)
        {
            NativeMethods.XCloseDisplay(_display);
        }

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
