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
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Omacut.Hosting;

namespace XerahS.CLI.Services;

/// <summary>
/// Runs the native Omacut editor from the console host. The CLI has no Avalonia app of its own,
/// so a dedicated UI thread boots one just for the editor window and ends when it closes.
/// </summary>
internal static class CliVideoEditorHost
{
    private static readonly Lock Gate = new();
    private static bool _started;

    public static Task<string?> ShowAsync(OmacutEditorOptions options)
    {
        if (OperatingSystem.IsMacOS())
        {
            // AppKit only runs on the process main thread, which the CLI command pipeline owns.
            throw new PlatformNotSupportedException("The interactive video editor needs the XerahS app on macOS. Use --headless from the CLI.");
        }

        lock (Gate)
        {
            if (_started)
            {
                throw new InvalidOperationException("The video editor can only be opened once per CLI run.");
            }

            _started = true;
        }

        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                AppBuilder.Configure<CliEditorApp>().UsePlatformDetect().WithInterFont().SetupWithoutStarting();
                var window = new OmacutWindow(options) { WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen };
                using var closed = new CancellationTokenSource();
                window.Closed += (_, _) => closed.Cancel();
                window.Show();
                Dispatcher.UIThread.MainLoop(closed.Token);
                completion.TrySetResult(window.ExportedPath);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "XerahS.CLI video editor",
        };

        if (OperatingSystem.IsWindows())
        {
            thread.SetApartmentState(ApartmentState.STA);
        }

        thread.Start();
        return completion.Task;
    }

    private sealed class CliEditorApp : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            RequestedThemeVariant = ThemeVariant.Dark;
        }
    }
}
