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

namespace XerahS.WatchFolder.Daemon;

internal sealed class DaemonOptions
{
    public bool RunAsService { get; private set; }
    public bool ScopeExplicitlySet { get; private set; }
    public WatchFolderDaemonScope Scope { get; private set; } = WatchFolderDaemonScope.User;
    public string? SettingsFolder { get; private set; }
    public int StopTimeoutSeconds { get; private set; } = 30;

    public static DaemonOptions Parse(string[] args)
    {
        var options = new DaemonOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--service":
                    options.RunAsService = true;
                    break;
                case "--scope":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("Missing value for --scope.");
                    }

                    string scopeValue = args[++i];
                    options.Scope = scopeValue.Equals("system", StringComparison.OrdinalIgnoreCase)
                        ? WatchFolderDaemonScope.System
                        : WatchFolderDaemonScope.User;
                    options.ScopeExplicitlySet = true;
                    break;
                case "--settings-folder":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("Missing value for --settings-folder.");
                    }

                    options.SettingsFolder = args[++i];
                    break;
                case "--stop-timeout-seconds":
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("Missing value for --stop-timeout-seconds.");
                    }

                    if (!int.TryParse(args[++i], out int timeout) || timeout <= 0)
                    {
                        throw new ArgumentException("Invalid value for --stop-timeout-seconds.");
                    }

                    options.StopTimeoutSeconds = timeout;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        if (options.RunAsService && !options.ScopeExplicitlySet)
        {
            options.Scope = WatchFolderDaemonScope.System;
        }

        return options;
    }
}
