#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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
using Avalonia.Threading;
using XerahS.Platform.Abstractions;
using SharpHook;
using SharpHook.Data;
using System.Diagnostics;
using DebugHelper = XerahS.Common.DebugHelper;

namespace XerahS.Platform.MacOS.Services
{
    public class MacOSHotkeyService : IHotkeyService
    {
        private readonly object _lock = new();
        private readonly Dictionary<ushort, HotkeyInfo> _registeredHotkeys = new();
        private readonly Dictionary<(Key Key, KeyModifiers Modifiers), HotkeyInfo> _hotkeysByCombo = new();
        private readonly HashSet<KeyCode> _pressedKeys = new();
        private readonly SimpleGlobalHook _hook;
        private ushort _nextId = 1;
        private bool _isSuspended;
        private bool _disposed;
        private bool _loggedUnsupported;
        private bool? _accessibilityEnabled;
        private bool _hookRunning;

        public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

        public MacOSHotkeyService()
        {
            _hook = new SimpleGlobalHook(GlobalHookType.Keyboard, null, true);
            _hook.KeyPressed += OnKeyPressed;
            _hook.KeyReleased += OnKeyReleased;
            _hook.HookEnabled += OnHookEnabled;
            _hook.HookDisabled += OnHookDisabled;
        }

        public bool RegisterHotkey(HotkeyInfo hotkeyInfo)
        {
            if (_isSuspended)
            {
                if (hotkeyInfo != null)
                {
                    hotkeyInfo.Status = XerahS.Platform.Abstractions.HotkeyStatus.NotConfigured;
                }

                return false;
            }

            if (!IsAccessibilityEnabled())
            {
                LogAccessibilityRequired();
                if (hotkeyInfo != null)
                {
                    hotkeyInfo.Status = XerahS.Platform.Abstractions.HotkeyStatus.Failed;
                }

                return false;
            }

            if (hotkeyInfo == null || !hotkeyInfo.IsValid)
            {
                if (hotkeyInfo != null)
                {
                    hotkeyInfo.Status = XerahS.Platform.Abstractions.HotkeyStatus.NotConfigured;
                }

                return false;
            }

            lock (_lock)
            {
                var combo = (hotkeyInfo.Key, hotkeyInfo.Modifiers);
                if (_hotkeysByCombo.ContainsKey(combo))
                {
                    hotkeyInfo.Status = XerahS.Platform.Abstractions.HotkeyStatus.Failed;
                    return false;
                }

                if (hotkeyInfo.Id == 0)
                {
                    hotkeyInfo.Id = _nextId++;
                }

                _registeredHotkeys[hotkeyInfo.Id] = hotkeyInfo;
                _hotkeysByCombo[combo] = hotkeyInfo;
                hotkeyInfo.Status = XerahS.Platform.Abstractions.HotkeyStatus.Registered;
            }

            EnsureHookRunning();
            return true;
        }

        public bool UnregisterHotkey(HotkeyInfo hotkeyInfo)
        {
            if (hotkeyInfo == null)
            {
                return false;
            }

            bool removed = false;

            lock (_lock)
            {
                if (hotkeyInfo.Id != 0 && _registeredHotkeys.Remove(hotkeyInfo.Id))
                {
                    removed = true;
                }

                _hotkeysByCombo.Remove((hotkeyInfo.Key, hotkeyInfo.Modifiers));
                hotkeyInfo.Status = XerahS.Platform.Abstractions.HotkeyStatus.NotConfigured;
            }

            if (removed)
            {
                StopHookIfIdle();
            }

            return removed;
        }

        public void UnregisterAll()
        {
            lock (_lock)
            {
                foreach (var hotkey in _registeredHotkeys.Values)
                {
                    hotkey.Status = XerahS.Platform.Abstractions.HotkeyStatus.NotConfigured;
                }

                _registeredHotkeys.Clear();
                _hotkeysByCombo.Clear();
                _pressedKeys.Clear();
            }

            StopHookIfIdle(force: true);
        }

        public bool IsRegistered(HotkeyInfo hotkeyInfo)
        {
            if (hotkeyInfo == null)
            {
                return false;
            }

            lock (_lock)
            {
                return hotkeyInfo.Id != 0 && _registeredHotkeys.ContainsKey(hotkeyInfo.Id);
            }
        }

        public bool IsSuspended
        {
            get => _isSuspended;
            set => _isSuspended = value;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            UnregisterAll();

            _hook.KeyPressed -= OnKeyPressed;
            _hook.KeyReleased -= OnKeyReleased;
            _hook.HookEnabled -= OnHookEnabled;
            _hook.HookDisabled -= OnHookDisabled;
            _hook.Dispose();

            GC.SuppressFinalize(this);
        }

        private void EnsureHookRunning()
        {
            if (_hookRunning)
            {
                return;
            }

            try
            {
                _hook.RunAsync();
                _hookRunning = true;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSHotkeyService: Failed to start SharpHook.");
                lock (_lock)
                {
                    foreach (var hotkey in _registeredHotkeys.Values)
                    {
                        hotkey.Status = XerahS.Platform.Abstractions.HotkeyStatus.Failed;
                    }
                }
            }
        }

        private void StopHookIfIdle(bool force = false)
        {
            if (!_hookRunning)
            {
                return;
            }

            lock (_lock)
            {
                if (!force && _registeredHotkeys.Count > 0)
                {
                    return;
                }
            }

            try
            {
                _hook.Stop();
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSHotkeyService: Failed to stop SharpHook.");
            }
            finally
            {
                _hookRunning = false;
            }
        }

        private void OnHookEnabled(object? sender, EventArgs e)
        {
            DebugHelper.WriteLine("MacOSHotkeyService: SharpHook enabled.");
        }

        private void OnHookDisabled(object? sender, EventArgs e)
        {
            DebugHelper.WriteLine("MacOSHotkeyService: SharpHook disabled.");
            lock (_lock)
            {
                _pressedKeys.Clear();
            }
        }

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            if (_isSuspended)
            {
                return;
            }

            var keyCode = e.Data.KeyCode;
            HotkeyInfo? hotkeyInfo = null;
            KeyModifiers modifiers;

            lock (_lock)
            {
                if (!_pressedKeys.Add(keyCode))
                {
                    return;
                }

                modifiers = GetCurrentModifiers();

                var key = MapKeyCodeToAvaloniaKey(keyCode);
                if (key == Key.None)
                {
                    return;
                }

                _hotkeysByCombo.TryGetValue((key, modifiers), out hotkeyInfo);
            }

            if (hotkeyInfo == null)
            {
                return;
            }

            DebugHelper.WriteLine($"Hotkey triggered: {hotkeyInfo}");
            Dispatcher.UIThread.Post(() =>
            {
                HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(hotkeyInfo));
            });
        }

        private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
        {
            var keyCode = e.Data.KeyCode;
            lock (_lock)
            {
                _pressedKeys.Remove(keyCode);
            }
        }

        private bool IsAccessibilityEnabled()
        {
            if (_accessibilityEnabled.HasValue)
            {
                return _accessibilityEnabled.Value;
            }

            const string script = "tell application \\\"System Events\\\" to get UI elements enabled";
            var output = RunOsaScriptWithOutput(script);
            _accessibilityEnabled = string.Equals(output?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
            return _accessibilityEnabled.Value;
        }

        private static string? RunOsaScriptWithOutput(string script)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e \"{script}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return null;
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 ? output : null;
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "MacOSHotkeyService.RunOsaScriptWithOutput failed");
                return null;
            }
        }

        private void LogAccessibilityRequired()
        {
            if (_loggedUnsupported)
            {
                return;
            }

            _loggedUnsupported = true;
            DebugHelper.WriteLine("MacOSHotkeyService: Accessibility permission is required for global hotkeys.");
        }

        private static KeyModifiers GetCurrentModifiers(HashSet<KeyCode> pressedKeys)
        {
            var modifiers = KeyModifiers.None;

            if (pressedKeys.Contains(KeyCode.VcLeftControl) || pressedKeys.Contains(KeyCode.VcRightControl))
            {
                modifiers |= KeyModifiers.Control;
            }

            if (pressedKeys.Contains(KeyCode.VcLeftAlt) || pressedKeys.Contains(KeyCode.VcRightAlt))
            {
                modifiers |= KeyModifiers.Alt;
            }

            if (pressedKeys.Contains(KeyCode.VcLeftShift) || pressedKeys.Contains(KeyCode.VcRightShift))
            {
                modifiers |= KeyModifiers.Shift;
            }

            if (pressedKeys.Contains(KeyCode.VcLeftMeta) || pressedKeys.Contains(KeyCode.VcRightMeta))
            {
                modifiers |= KeyModifiers.Meta;
            }

            return modifiers;
        }

        private KeyModifiers GetCurrentModifiers()
        {
            return GetCurrentModifiers(_pressedKeys);
        }

        private static Key MapKeyCodeToAvaloniaKey(KeyCode keyCode)
        {
            return keyCode switch
            {
                KeyCode.VcEscape => Key.Escape,
                KeyCode.VcF1 => Key.F1,
                KeyCode.VcF2 => Key.F2,
                KeyCode.VcF3 => Key.F3,
                KeyCode.VcF4 => Key.F4,
                KeyCode.VcF5 => Key.F5,
                KeyCode.VcF6 => Key.F6,
                KeyCode.VcF7 => Key.F7,
                KeyCode.VcF8 => Key.F8,
                KeyCode.VcF9 => Key.F9,
                KeyCode.VcF10 => Key.F10,
                KeyCode.VcF11 => Key.F11,
                KeyCode.VcF12 => Key.F12,
                KeyCode.VcF13 => Key.F13,
                KeyCode.VcF14 => Key.F14,
                KeyCode.VcF15 => Key.F15,
                KeyCode.VcF16 => Key.F16,
                KeyCode.VcF17 => Key.F17,
                KeyCode.VcF18 => Key.F18,
                KeyCode.VcF19 => Key.F19,
                KeyCode.VcF20 => Key.F20,
                KeyCode.VcF21 => Key.F21,
                KeyCode.VcF22 => Key.F22,
                KeyCode.VcF23 => Key.F23,
                KeyCode.VcF24 => Key.F24,
                KeyCode.VcBackQuote => Key.Oem3,
                KeyCode.Vc0 => Key.D0,
                KeyCode.Vc1 => Key.D1,
                KeyCode.Vc2 => Key.D2,
                KeyCode.Vc3 => Key.D3,
                KeyCode.Vc4 => Key.D4,
                KeyCode.Vc5 => Key.D5,
                KeyCode.Vc6 => Key.D6,
                KeyCode.Vc7 => Key.D7,
                KeyCode.Vc8 => Key.D8,
                KeyCode.Vc9 => Key.D9,
                KeyCode.VcMinus => Key.OemMinus,
                KeyCode.VcEquals => Key.OemPlus,
                KeyCode.VcBackspace => Key.Back,
                KeyCode.VcTab => Key.Tab,
                KeyCode.VcCapsLock => Key.Capital,
                KeyCode.VcA => Key.A,
                KeyCode.VcB => Key.B,
                KeyCode.VcC => Key.C,
                KeyCode.VcD => Key.D,
                KeyCode.VcE => Key.E,
                KeyCode.VcF => Key.F,
                KeyCode.VcG => Key.G,
                KeyCode.VcH => Key.H,
                KeyCode.VcI => Key.I,
                KeyCode.VcJ => Key.J,
                KeyCode.VcK => Key.K,
                KeyCode.VcL => Key.L,
                KeyCode.VcM => Key.M,
                KeyCode.VcN => Key.N,
                KeyCode.VcO => Key.O,
                KeyCode.VcP => Key.P,
                KeyCode.VcQ => Key.Q,
                KeyCode.VcR => Key.R,
                KeyCode.VcS => Key.S,
                KeyCode.VcT => Key.T,
                KeyCode.VcU => Key.U,
                KeyCode.VcV => Key.V,
                KeyCode.VcW => Key.W,
                KeyCode.VcX => Key.X,
                KeyCode.VcY => Key.Y,
                KeyCode.VcZ => Key.Z,
                KeyCode.VcOpenBracket => Key.Oem4,
                KeyCode.VcCloseBracket => Key.Oem6,
                KeyCode.VcBackslash => Key.Oem5,
                KeyCode.VcSemicolon => Key.Oem1,
                KeyCode.VcQuote => Key.Oem7,
                KeyCode.VcEnter => Key.Return,
                KeyCode.VcComma => Key.OemComma,
                KeyCode.VcPeriod => Key.OemPeriod,
                KeyCode.VcSlash => Key.Oem2,
                KeyCode.VcSpace => Key.Space,
                KeyCode.VcInsert => Key.Insert,
                KeyCode.VcDelete => Key.Delete,
                KeyCode.VcHome => Key.Home,
                KeyCode.VcEnd => Key.End,
                KeyCode.VcPageUp => Key.PageUp,
                KeyCode.VcPageDown => Key.PageDown,
                KeyCode.VcUp => Key.Up,
                KeyCode.VcLeft => Key.Left,
                KeyCode.VcRight => Key.Right,
                KeyCode.VcDown => Key.Down,
                KeyCode.VcNumLock => Key.NumLock,
                KeyCode.VcNumPadClear => Key.Clear,
                KeyCode.VcNumPadDivide => Key.Divide,
                KeyCode.VcNumPadMultiply => Key.Multiply,
                KeyCode.VcNumPadSubtract => Key.Subtract,
                KeyCode.VcNumPadEquals => Key.OemPlus,
                KeyCode.VcNumPadAdd => Key.Add,
                KeyCode.VcNumPadEnter => Key.Return,
                KeyCode.VcNumPadDecimal => Key.Decimal,
                KeyCode.VcNumPadSeparator => Key.OemComma,
                KeyCode.VcNumPad0 => Key.NumPad0,
                KeyCode.VcNumPad1 => Key.NumPad1,
                KeyCode.VcNumPad2 => Key.NumPad2,
                KeyCode.VcNumPad3 => Key.NumPad3,
                KeyCode.VcNumPad4 => Key.NumPad4,
                KeyCode.VcNumPad5 => Key.NumPad5,
                KeyCode.VcNumPad6 => Key.NumPad6,
                KeyCode.VcNumPad7 => Key.NumPad7,
                KeyCode.VcNumPad8 => Key.NumPad8,
                KeyCode.VcNumPad9 => Key.NumPad9,
                KeyCode.VcPrintScreen => Key.PrintScreen,
                KeyCode.VcScrollLock => Key.Scroll,
                KeyCode.VcPause => Key.Pause,
                _ => Key.None
            };
        }
    }
}
