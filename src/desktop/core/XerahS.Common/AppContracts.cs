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

namespace XerahS.Common;

/// <summary>
/// Central repository for all application-level named contracts: IPC channel names,
/// command-line flags, well-known workflow IDs, and D-Bus/XDG desktop integration keys.
/// Having these in one place lets agents search semantically (grep "SingleInstance")
/// instead of hunting for raw GUIDs or magic strings scattered across entry points.
/// </summary>
public static class AppContracts
{
    /// <summary>
    /// Single-instance enforcement: named mutex and pipe used to coordinate
    /// a single running instance of the application across all entry points.
    /// </summary>
    public static class SingleInstance
    {
        /// <summary>Named mutex ensuring only one instance acquires the primary lock.</summary>
        public const string MutexName = "XerahS-82E6AC09-0FFC-4992-B793-3F79E1F71E70";

        /// <summary>
        /// Logical named pipe used to relay command-line arguments from subsequent instances.
        /// On Unix, <see cref="SingleInstanceManager.GetPlatformPipeName"/> remaps this to a
        /// short rooted /tmp socket because AF_UNIX paths cannot exceed 104 bytes.
        /// </summary>
        public const string PipeName = "XerahS-Pipe-1F42DA49-7B2A-4E6F-8A3C-D56F09E0C481";
    }

    /// <summary>
    /// Command-line flags recognised by the application and CLI entry points.
    /// </summary>
    public static class Cli
    {
        /// <summary>
        /// Legacy marker used by Windows startup registrations. A secondary invocation containing
        /// only this marker is passive and must not surface an already-running instance.
        /// </summary>
        public const string SilentStartupFlag = "-silent";

        /// <summary>
        /// Relayed by automation (omaxerahs, agents) after writing WorkflowsConfig so the running
        /// instance reloads workflows from disk. Handled without surfacing the main window.
        /// </summary>
        public const string ReloadWorkflowsFlag = "--reload-workflows";

        public static bool IsReloadWorkflowsInvocation(IReadOnlyCollection<string>? args)
        {
            return args is { Count: > 0 } &&
                args.All(arg => arg.Equals(ReloadWorkflowsFlag, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Relayed by "omaxerahs workflow run" (Hyprland keybindings, XIP0088) as
        /// <c>--run-workflow &lt;id&gt;</c>. Runs the workflow like a hotkey without surfacing the main window.
        /// </summary>
        public const string RunWorkflowFlag = "--run-workflow";

        public static bool TryGetRunWorkflowId(IReadOnlyList<string>? args, out string workflowId)
        {
            workflowId = string.Empty;
            if (args is not { Count: 2 } ||
                !args[0].Equals(RunWorkflowFlag, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(args[1]))
            {
                return false;
            }

            workflowId = args[1].Trim();
            return true;
        }

        /// <summary>
        /// Relayed by an external capture front end (e.g. a GNOME Shell extension that does its own
        /// region selection) as <c>--process-image &lt;png&gt; &lt;workflow-id&gt; [x,y,w,h]</c>. Runs the
        /// workflow's after-capture tasks on that image instead of capturing. The optional rectangle is
        /// the monitor (logical desktop coordinates) where a task-mode editor should open.
        /// </summary>
        public const string ProcessImageFlag = "--process-image";

        public static bool TryGetProcessImageArgs(IReadOnlyList<string>? args, out string imagePath, out string workflowId, out string? monitorRect)
        {
            imagePath = string.Empty;
            workflowId = string.Empty;
            monitorRect = null;
            if (args is not { Count: 3 or 4 } ||
                !args[0].Equals(ProcessImageFlag, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(args[1]) ||
                string.IsNullOrWhiteSpace(args[2]))
            {
                return false;
            }

            imagePath = args[1].Trim();
            workflowId = args[2].Trim();
            monitorRect = args.Count == 4 ? args[3].Trim() : null;
            return true;
        }

        /// <summary>Flag used by helper processes (e.g. screen capture helpers) to forward a capture back to the running instance.</summary>
        public const string SendToFlag = "--send-to";

        /// <summary>
        /// Overrides the app personal/settings folder for the current process.
        /// Intended for isolated debug and tooling scenarios.
        /// </summary>
        public const string SettingsFolderFlag = "--settings-folder";

        /// <summary>Legacy flag for installing a plugin directly from the CLI (superseded by the plugin exporter tool).</summary>
        public const string LegacyInstallPluginFlag = "-InstallPlugin";

        /// <summary>
        /// Default workflow ID used by <c>verify-recording</c> command when no workflow is specified.
        /// This is the built-in "Screen recording" workflow shipped with XerahS.
        /// </summary>
        public const string DefaultRecordingWorkflowId = "67f116dc";

        public static bool IsPassiveStartupInvocation(IReadOnlyCollection<string>? args)
        {
            return args is { Count: > 0 } &&
                args.All(arg => arg.Equals(SilentStartupFlag, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Desktop integration constants for Linux (XDG, D-Bus, Thunar, KDE, etc.).
    /// </summary>
    public static class LinuxIntegration
    {
        /// <summary>D-Bus header key used to mark a message as a SendTo relay from a helper process.</summary>
        public const string SendToMarkerKey = "X-XerahS-SendTo";

        /// <summary>Value associated with <see cref="SendToMarkerKey"/> when the relay is active.</summary>
        public const string SendToMarkerValue = "true";
    }
}
