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

using System.Runtime.InteropServices;
using System.Text;
using XerahS.Common;

namespace XerahS.Platform.Windows
{
    public class InputManager
    {
        public List<INPUT> InputList { get; private set; }

        public bool AutoClearAfterSend { get; set; }

        public InputManager()
        {
            InputList = new List<INPUT>();
        }

        public bool SendInputs()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

            INPUT[] inputList = InputList.ToArray();
            uint len = (uint)inputList.Length;
            uint successfulInputs = NativeMethods.SendInput(len, inputList, Marshal.SizeOf(typeof(INPUT)));
            if (AutoClearAfterSend) ClearInputs();
            return successfulInputs == len;
        }

        public void ClearInputs()
        {
            InputList.Clear();
        }

        private void AddKeyInput(VirtualKeyCode keyCode, bool isKeyUp)
        {
            INPUT input = new INPUT();
            input.Type = InputType.InputKeyboard;
            input.Data.Keyboard = new KEYBDINPUT();
            input.Data.Keyboard.wVk = keyCode;
            if (isKeyUp) input.Data.Keyboard.dwFlags = KeyboardEventFlags.KEYEVENTF_KEYUP;
            InputList.Add(input);
        }

        public void AddKeyDown(VirtualKeyCode keyCode)
        {
            AddKeyInput(keyCode, false);
        }

        public void AddKeyUp(VirtualKeyCode keyCode)
        {
            AddKeyInput(keyCode, true);
        }

        public void AddKeyPress(VirtualKeyCode keyCode)
        {
            AddKeyInput(keyCode, false);
            AddKeyInput(keyCode, true);
        }

        public void AddKeyPressModifiers(VirtualKeyCode keyCode, params VirtualKeyCode[] modifiers)
        {
            foreach (VirtualKeyCode modifier in modifiers)
            {
                AddKeyDown(modifier);
            }

            AddKeyPress(keyCode);

            foreach (VirtualKeyCode modifier in modifiers)
            {
                AddKeyUp(modifier);
            }
        }

        public void AddKeyPressText(string text)
        {
            byte[] chars = Encoding.ASCII.GetBytes(text);

            for (int i = 0; i < chars.Length; i++)
            {
                ushort scanCode = chars[i];

                INPUT input = new INPUT();
                input.Type = InputType.InputKeyboard;
                input.Data.Keyboard = new KEYBDINPUT();
                input.Data.Keyboard.wScan = scanCode;
                input.Data.Keyboard.dwFlags = KeyboardEventFlags.KEYEVENTF_UNICODE;
                if ((scanCode & 0xFF00) == 0xE000) input.Data.Keyboard.dwFlags |= KeyboardEventFlags.KEYEVENTF_EXTENDEDKEY;
                InputList.Add(input);

                input.Data.Keyboard.dwFlags |= KeyboardEventFlags.KEYEVENTF_KEYUP;
                InputList.Add(input);
            }
        }

        private void AddMouseInput(XerahS.Platform.Windows.MouseButtons button, bool isMouseUp)
        {
            INPUT input = new INPUT();
            input.Type = InputType.InputMouse;
            input.Data.Mouse = new MOUSEINPUT();

            if (button == XerahS.Platform.Windows.MouseButtons.Left)
            {
                input.Data.Mouse.dwFlags = isMouseUp ? MouseEventFlags.MOUSEEVENTF_LEFTUP : MouseEventFlags.MOUSEEVENTF_LEFTDOWN;
            }
            else if (button == XerahS.Platform.Windows.MouseButtons.Right)
            {
                input.Data.Mouse.dwFlags = isMouseUp ? MouseEventFlags.MOUSEEVENTF_RIGHTUP : MouseEventFlags.MOUSEEVENTF_RIGHTDOWN;
            }
            else if (button == XerahS.Platform.Windows.MouseButtons.Middle)
            {
                input.Data.Mouse.dwFlags = isMouseUp ? MouseEventFlags.MOUSEEVENTF_MIDDLEUP : MouseEventFlags.MOUSEEVENTF_MIDDLEDOWN;
            }
            else if (button == XerahS.Platform.Windows.MouseButtons.XButton1)
            {
                input.Data.Mouse.mouseData = (uint)MouseEventDataXButtons.XBUTTON1;
                input.Data.Mouse.dwFlags = isMouseUp ? MouseEventFlags.MOUSEEVENTF_XUP : MouseEventFlags.MOUSEEVENTF_XDOWN;
            }
            else if (button == XerahS.Platform.Windows.MouseButtons.XButton2)
            {
                input.Data.Mouse.mouseData = (uint)MouseEventDataXButtons.XBUTTON2;
                input.Data.Mouse.dwFlags = isMouseUp ? MouseEventFlags.MOUSEEVENTF_XUP : MouseEventFlags.MOUSEEVENTF_XDOWN;
            }

            InputList.Add(input);
        }

        public void AddMouseDown(XerahS.Platform.Windows.MouseButtons button = XerahS.Platform.Windows.MouseButtons.Left)
        {
            AddMouseInput(button, false);
        }

        public void AddMouseUp(XerahS.Platform.Windows.MouseButtons button = XerahS.Platform.Windows.MouseButtons.Left)
        {
            AddMouseInput(button, true);
        }

        public void AddMouseClick(XerahS.Platform.Windows.MouseButtons button = XerahS.Platform.Windows.MouseButtons.Left)
        {
            AddMouseDown(button);
            AddMouseUp(button);
        }

        public void AddMouseClick(int x, int y, XerahS.Platform.Windows.MouseButtons button = XerahS.Platform.Windows.MouseButtons.Left)
        {
            AddMouseMove(x, y);
            AddMouseClick(button);
        }

        public void AddMouseClick(System.Drawing.Point position, XerahS.Platform.Windows.MouseButtons button = XerahS.Platform.Windows.MouseButtons.Left)
        {
            AddMouseMove(position);
            AddMouseClick(button);
        }

        public void AddMouseMove(int x, int y)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            INPUT input = new INPUT();
            input.Type = InputType.InputMouse;
            input.Data.Mouse = new MOUSEINPUT();
            input.Data.Mouse.dx = (int)Math.Ceiling((double)(x * 65535) / NativeMethods.GetSystemMetrics(SystemMetric.SM_CXSCREEN)) + 1;
            input.Data.Mouse.dy = (int)Math.Ceiling((double)(y * 65535) / NativeMethods.GetSystemMetrics(SystemMetric.SM_CYSCREEN)) + 1;
            input.Data.Mouse.dwFlags = MouseEventFlags.MOUSEEVENTF_MOVE | MouseEventFlags.MOUSEEVENTF_ABSOLUTE;
            InputList.Add(input);
        }

        public void AddMouseMove(System.Drawing.Point position)
        {
            AddMouseMove(position.X, position.Y);
        }

        public void AddMouseWheel(int delta)
        {
            INPUT input = new INPUT();
            input.Type = InputType.InputMouse;
            input.Data.Mouse = new MOUSEINPUT();
            input.Data.Mouse.dwFlags = MouseEventFlags.MOUSEEVENTF_WHEEL;
            input.Data.Mouse.mouseData = (uint)delta;
            InputList.Add(input);
        }
    }
}
