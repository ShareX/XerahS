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

using XerahS.Platform.Abstractions;

namespace XerahS.CLI.Services
{
    /// <summary>
    /// Minimal IToastService implementation for headless CLI execution.
    /// Outputs toast notifications to console instead of showing UI toasts.
    /// </summary>
    public class HeadlessToastService : IToastService
    {
        public void ShowToast(ToastConfig config)
        {
            if (config == null) return;

            // Output simplified toast information to console
            Console.WriteLine($"[NOTIFICATION] {config.Title}");
            if (!string.IsNullOrEmpty(config.Text))
            {
                Console.WriteLine($"  {config.Text}");
            }

            // Show file path or URL if present
            if (!string.IsNullOrEmpty(config.FilePath))
            {
                Console.WriteLine($"  File: {config.FilePath}");
            }
            if (!string.IsNullOrEmpty(config.URL))
            {
                Console.WriteLine($"  URL: {config.URL}");
            }
        }

        public void CloseActiveToast()
        {
            // No-op in CLI mode
        }
    }
}
