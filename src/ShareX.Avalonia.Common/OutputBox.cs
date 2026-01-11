#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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

using System.Diagnostics;

namespace XerahS.Common
{
    public static class OutputBox
    {
        public static void Show(string text, string? title)
        {
            WriteDebug(text, title);
        }

        public static void Show(string text, string? title, bool isError)
        {
            WriteDebug(text, title);
        }

        private static void WriteDebug(string text, string? title)
        {
            string prefix = string.IsNullOrEmpty(title) ? "Output" : title;
            Debug.WriteLine($"{prefix}: {text}");
        }
    }
}
