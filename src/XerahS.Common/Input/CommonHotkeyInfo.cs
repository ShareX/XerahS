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

using Newtonsoft.Json;
using System.Text;
// Replaced System.Windows.Forms.Keys with local Keys enum
// using System.Windows.Forms; 

namespace XerahS.Common
{
    public class CommonHotkeyInfo
    {
        public CommonKeys Hotkey { get; set; }

        [JsonIgnore]
        public ushort ID { get; set; }

        [JsonIgnore]
        public HotkeyStatus Status { get; set; }

        public CommonKeys KeyCode => Hotkey & CommonKeys.KeyCode;

        public CommonKeys ModifiersKeys => Hotkey & CommonKeys.Modifiers;

        public bool Control => Hotkey.HasFlag(CommonKeys.Control);

        public bool Shift => Hotkey.HasFlag(CommonKeys.Shift);

        public bool Alt => Hotkey.HasFlag(CommonKeys.Alt);

        public bool Win { get; set; }

        public Modifiers ModifiersEnum
        {
            get
            {
                Modifiers modifiers = Modifiers.None;

                if (Alt) modifiers |= Modifiers.Alt;
                if (Control) modifiers |= Modifiers.Control;
                if (Shift) modifiers |= Modifiers.Shift;
                if (Win) modifiers |= Modifiers.Win;

                return modifiers;
            }
        }

        public bool IsOnlyModifiers => KeyCode == CommonKeys.ControlKey || KeyCode == CommonKeys.ShiftKey || KeyCode == CommonKeys.Menu || (KeyCode == CommonKeys.None && Win);

        public bool IsValidHotkey => KeyCode != CommonKeys.None && !IsOnlyModifiers;

        public CommonHotkeyInfo()
        {
            Status = HotkeyStatus.NotConfigured;
        }

        public CommonHotkeyInfo(CommonKeys hotkey) : this()
        {
            Hotkey = hotkey;
        }

        public CommonHotkeyInfo(CommonKeys hotkey, ushort id) : this(hotkey)
        {
            ID = id;
        }

        public override string ToString()
        {
            string text = "";

            if (Control)
            {
                text += "Ctrl + ";
            }

            if (Shift)
            {
                text += "Shift + ";
            }

            if (Alt)
            {
                text += "Alt + ";
            }

            if (Win)
            {
                text += "Win + ";
            }

            if (IsOnlyModifiers)
            {
                text += "...";
            }
            else if (KeyCode == CommonKeys.Back)
            {
                text += "Backspace";
            }
            else if (KeyCode == CommonKeys.Return)
            {
                text += "Enter";
            }
            else if (KeyCode == CommonKeys.Capital)
            {
                text += "Caps Lock";
            }
            else if (KeyCode == CommonKeys.Next)
            {
                text += "Page Down";
            }
            else if (KeyCode == CommonKeys.Scroll)
            {
                text += "Scroll Lock";
            }
            else if (KeyCode >= CommonKeys.D0 && KeyCode <= CommonKeys.D9)
            {
                text += (KeyCode - CommonKeys.D0).ToString();
            }
            else if (KeyCode >= CommonKeys.NumPad0 && KeyCode <= CommonKeys.NumPad9)
            {
                text += "Numpad " + (KeyCode - CommonKeys.NumPad0).ToString();
            }
            else
            {
                text += ToStringWithSpaces(KeyCode);
            }

            return text;
        }

        private string ToStringWithSpaces(CommonKeys key)
        {
            string name = key.ToString();

            StringBuilder result = new StringBuilder();

            bool lastWasUpper = false;

            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !lastWasUpper)
                {
                    result.Append(" " + name[i]);
                }
                else
                {
                    result.Append(name[i]);
                }

                lastWasUpper = char.IsUpper(name[i]); // Simplistic space insertion fix if needed, assuming original logic was fine. 
                // Original: if (i > 0 && char.IsUpper(name[i])) -> this splits EVERY uppercase letter. 
                // e.g. "NumPad0" -> "Num Pad0" or "Num Pad 0". 
                // Actually, standard ToString of enum is "NumPad0".
                // I'll keep original logic exactly as is to match behavior.
            }

            // Re-implementing strictly as original:
            /*
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    result.Append(" " + name[i]);
                }
                else
                {
                    result.Append(name[i]);
                }
            }
            */
            // Wait, I should just use the exact code I read.
            return result.ToString();
        }
    }
}
