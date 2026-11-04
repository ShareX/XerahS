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

using System.CommandLine;
using XerahS.OmaXerahs.Models;
using XerahS.OmaXerahs.Services;

namespace XerahS.OmaXerahs.Commands;

internal static class CapabilitiesCommand
{
    internal static Command Create()
    {
        var command = new Command("capabilities", "Print the omaxerahs plugin contract.");
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            return Run();
        });
        return command;
    }

    internal static CapabilitiesResponse BuildResponse()
    {
        return new CapabilitiesResponse
        {
            SchemaVersion = 1,
            Name = "omaxerahs",
            Version = UploadHost.GetVersion(),
            MinPluginProtocol = 1,
            Capabilities = new[]
            {
                "doctor.image",
                "upload.image",
                "upload.destination",
                "workflow.list",
                "workflow.show",
                "workflow.tasks",
                "workflow.run",
                "capture",
                "effects.import",
                "effects.show",
                "effects.toggle",
                "effects.clear",
                "image.resize",
                "image.convert",
                "image.watermark",
                "image.gif",
                "skill.install"
            }
            .Concat(HasOmaSnapCapture() ? new[] { "capture.omasnap" } : Array.Empty<string>())
            .ToArray()
        };
    }

    /// <summary>OmaSnap host mode passed its probe (Hyprland only; elsewhere no process is started).</summary>
    internal static bool HasOmaSnapCapture()
    {
#if LINUX
        if (OperatingSystem.IsLinux())
        {
            return XerahS.Platform.Linux.Services.LinuxDesktopProfile.Current.IsOmaSnapUsableAsync().GetAwaiter().GetResult();
        }
#endif
        return false;
    }

    internal static int Run()
    {
        JsonStdout.Write(BuildResponse());
        return 0;
    }
}
