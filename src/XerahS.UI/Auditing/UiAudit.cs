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
using Avalonia;
using Avalonia.Controls;

namespace XerahS.UI.Auditing
{
    public class UiAudit : AvaloniaObject
    {
        /// <summary>
        /// Indicates if the control is detected as unwired (no Command or Click handler).
        /// Setting this to true triggers validity visuals in Debug builds (DarkRed foreground).
        /// </summary>
        public static readonly AttachedProperty<bool> IsUnwiredProperty =
            AvaloniaProperty.RegisterAttached<UiAudit, Control, bool>("IsUnwired");

        public static bool GetIsUnwired(Control element)
        {
            return element.GetValue(IsUnwiredProperty);
        }

        public static void SetIsUnwired(Control element, bool value)
        {
            element.SetValue(IsUnwiredProperty, value);
        }

        /// <summary>
        /// Manually suppresses the unwired check. 
        /// Use this when a control is wired via Code Behind or other mechanism not detected by static analysis.
        /// </summary>
        public static readonly AttachedProperty<bool> IsWiredManualProperty =
            AvaloniaProperty.RegisterAttached<UiAudit, Control, bool>("IsWiredManual");

        public static bool GetIsWiredManual(Control element)
        {
            return element.GetValue(IsWiredManualProperty);
        }

        public static void SetIsWiredManual(Control element, bool value)
        {
            element.SetValue(IsWiredManualProperty, value);
        }

        /// <summary>
        /// Enables runtime analysis of controls to detect unwired elements.
        /// Should be called in Debug builds.
        /// </summary>
        public static void InitializeRuntimeChecks()
        {
            Control.LoadedEvent.AddClassHandler<Control>((sender, args) =>
            {
                if (sender is not Control control) return;

                // Optimization: Ignore if already marked
                if (GetIsUnwired(control) || GetIsWiredManual(control)) return;

                bool isUnwired = false;

                if (control is Button button)
                {
                    // Button: Check Command and Flyout
                    if (button.Command == null && button.Flyout == null)
                    {
                        isUnwired = true;
                    }
                }
                else if (control is MenuItem menuItem)
                {
                    // MenuItem: Check Command, Flyout (if any), ItemsSource, and Items count (sub-menu)
                    if (menuItem.Command == null && 
                        menuItem.ItemsSource == null && 
                        menuItem.ItemCount == 0)
                    {
                        isUnwired = true;
                    }
                }
                // Note: CheckBox/RadioButton/ToggleSwitch (ToggleButton) are skipped 
                // because validating data binding (IsChecked) is difficult at runtime.

                if (isUnwired)
                {
                    SetIsUnwired(control, true);
                }
            });
        }
    }
}
