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

internal static class DoctorCommand
{
    internal static Command Create()
    {
        var command = new Command("doctor", "Report whether images can be uploaded (Image destination, or File destination fallback). Read-only; never mutates destinations.");
        var jsonOption = JsonStdout.CreateJsonOption();
        command.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            JsonStdout.Enabled = parseResult.GetValue(jsonOption);
            return RunAsync().GetAwaiter().GetResult();
        });
        return command;
    }

    internal static async Task<int> RunAsync()
    {
        try
        {
            await UploadHost.EnsureBootstrappedAsync();
            var inspection = UploadHost.InspectImageDestination();
            var response = UploadHost.CreateDoctorResponse(inspection);
            JsonStdout.Write(response);

            if (!JsonStdout.Enabled)
            {
                Console.Error.WriteLine(response.Ok
                    ? $"Image uploads ready: {response.Image.DisplayName} ({response.Image.Category} destination)"
                    : "No usable image or file uploader is configured in XerahS.");
            }

            return response.Ok ? 0 : 1;
        }
        catch (Exception ex)
        {
            var (code, message) = ErrorMapper.FromException(ex);
            if (code == CliErrorCodes.Provider)
            {
                code = CliErrorCodes.Incompatible;
            }

            return JsonStdout.WriteFailureAndExit(code, message);
        }
    }
}
