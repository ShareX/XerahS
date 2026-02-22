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

using System.Threading;
using System.Threading.Tasks;
using XerahS.Platform.Linux.Capture.Contracts;

namespace XerahS.Platform.Linux.Capture.Providers;

internal sealed class CliCaptureProvider : ILinuxCaptureProvider
{
    private readonly ILinuxCaptureRuntime _runtime;

    public CliCaptureProvider(ILinuxCaptureRuntime runtime)
    {
        _runtime = runtime;
    }

    public string ProviderId => "cli-tools";

    public LinuxCaptureStage Stage => LinuxCaptureStage.X11;

    public bool CanHandle(LinuxCaptureRequest request, ILinuxCaptureContext context)
    {
        return !context.IsSandboxed && !context.IsWayland;
    }

    public async Task<LinuxCaptureResult> TryCaptureAsync(
        LinuxCaptureRequest request,
        ILinuxCaptureContext context,
        CancellationToken cancellationToken = default)
    {
        var bitmap = await _runtime.TryCliCaptureAsync(
            request.Kind,
            context.Desktop,
            request.WindowService,
            request.Options).ConfigureAwait(false);

        if (bitmap != null)
        {
            return LinuxCaptureResult.Success(ProviderId, bitmap);
        }

        return LinuxCaptureResult.Failure(ProviderId);
    }
}
