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

using System.Diagnostics;

namespace XerahS.App
{
    /// <summary>
    /// On Omarchy, keeps the bundled "xerahs" agent skill installed and current so the coding agent
    /// Omarchy launches ("omarchy agent") knows how to drive XerahS through omaxerahs. Omarchy links
    /// its own skills the same way; packages cannot write into the user's home, so the app does it.
    /// </summary>
    internal static class AgentSkillBootstrapper
    {
        /// <summary>Omarchy detection comes from the platform desktop profile (XIP0088), the single source of truth.</summary>
        internal static bool IsOmarchy()
        {
            return XerahS.Platform.Abstractions.PlatformServices.DesktopProfile?.IsOmarchy == true;
        }

        internal static void EnsureInstalled()
        {
            if (!OperatingSystem.IsLinux() || !IsOmarchy())
            {
                return;
            }

            string omaxerahs = Path.Combine(AppContext.BaseDirectory, "omaxerahs");
            if (!File.Exists(omaxerahs))
            {
                XerahS.Common.DebugHelper.WriteLine($"Agent skill: omaxerahs not found next to XerahS ({omaxerahs}); skipping.");
                return;
            }

            try
            {
                ProcessTextOutput result = Process.RunAndCaptureText(
                    new ProcessStartInfo(omaxerahs)
                    {
                        ArgumentList = { "skill", "install", "--json" },
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    },
                    TimeSpan.FromSeconds(30));

                XerahS.Common.DebugHelper.WriteLine($"Agent skill: omaxerahs skill install exited {result.ExitStatus.ExitCode}: {result.StandardOutput.Trim()}");
            }
            catch (TimeoutException)
            {
                XerahS.Common.DebugHelper.WriteLine("Agent skill: omaxerahs skill install timed out.");
            }
            catch (Exception ex)
            {
                XerahS.Common.DebugHelper.WriteException(ex, "Agent skill: failed to run omaxerahs skill install");
            }
        }
    }
}
