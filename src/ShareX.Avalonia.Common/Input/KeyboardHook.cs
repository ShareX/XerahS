#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
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

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ShareX.Avalonia.Common
{
    public delegate void KeyEventHandler(object sender, KeyEventArgs e);

    public class KeyEventArgs : EventArgs
    {
        public Keys KeyData { get; }
        public Keys KeyCode => KeyData & Keys.KeyCode;
        public Keys Modifiers => KeyData & Keys.Modifiers;
        public bool Shift => (KeyData & Keys.Shift) == Keys.Shift;
        public bool Control => (KeyData & Keys.Control) == Keys.Control;
        public bool Alt => (KeyData & Keys.Alt) == Keys.Alt;
        public bool Handled { get; set; }
        public bool SuppressKeyPress { get; set; }

        public KeyEventArgs(Keys keyData)
        {
            KeyData = keyData;
        }
    }

    public class KeyboardHook : IDisposable
    {
        public event KeyEventHandler KeyDown, KeyUp;

        private NativeMethods.HookProc keyboardHookProc;
        private IntPtr keyboardHookHandle = IntPtr.Zero;

        public KeyboardHook()
        {
            keyboardHookProc = KeyboardHookProc;
            keyboardHookHandle = SetHook(NativeConstants.WH_KEYBOARD_LL, keyboardHookProc);
        }

        ~KeyboardHook()
        {
            Dispose();
        }

        private static IntPtr SetHook(int hookType, NativeMethods.HookProc hookProc)
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            using (ProcessModule currentModule = currentProcess.MainModule)
            {
                IntPtr moduleHandle = NativeMethods.GetModuleHandle(currentModule.ModuleName);
                return NativeMethods.SetWindowsHookEx(hookType, hookProc, moduleHandle, 0);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                bool handled = false;

                int msg = wParam.ToInt32();

                if (msg == NativeConstants.WM_KEYDOWN || msg == NativeConstants.WM_SYSKEYDOWN)
                {
                    handled = OnKeyDown(lParam);
                }
                else if (msg == NativeConstants.WM_KEYUP || msg == NativeConstants.WM_SYSKEYUP)
                {
                    handled = OnKeyUp(lParam);
                }

                if (handled)
                {
                    return new IntPtr(1);
                }
            }

            return NativeMethods.CallNextHookEx(keyboardHookHandle, nCode, wParam, lParam);
        }

        private bool OnKeyDown(IntPtr key)
        {
            if (KeyDown != null)
            {
                KeyEventArgs keyEventArgs = GetKeyEventArgs(key);
                KeyDown(this, keyEventArgs);
                return keyEventArgs.Handled || keyEventArgs.SuppressKeyPress;
            }

            return false;
        }

        private bool OnKeyUp(IntPtr key)
        {
            if (KeyUp != null)
            {
                KeyEventArgs keyEventArgs = GetKeyEventArgs(key);
                KeyUp(this, keyEventArgs);
                return keyEventArgs.Handled || keyEventArgs.SuppressKeyPress;
            }

            return false;
        }

        private KeyEventArgs GetKeyEventArgs(IntPtr key)
        {
            int vkCode = Marshal.ReadInt32(key); // KBDLLHOOKSTRUCT.vkCode is first
            Keys keyData = (Keys)vkCode | GetModifierKeys();
            return new KeyEventArgs(keyData);
        }

        private static Keys GetModifierKeys()
        {
            Keys modifiers = Keys.None;

            if ((NativeMethods.GetKeyState(VirtualKeyCode.SHIFT) & 0x8000) != 0)
            {
                modifiers |= Keys.Shift;
            }

            if ((NativeMethods.GetKeyState(VirtualKeyCode.CONTROL) & 0x8000) != 0)
            {
                modifiers |= Keys.Control;
            }

            if ((NativeMethods.GetKeyState(VirtualKeyCode.MENU) & 0x8000) != 0)
            {
                modifiers |= Keys.Alt;
            }

            return modifiers;
        }

        public void Dispose()
        {
            if (keyboardHookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(keyboardHookHandle);
                keyboardHookHandle = IntPtr.Zero;
            }
        }
    }
}
