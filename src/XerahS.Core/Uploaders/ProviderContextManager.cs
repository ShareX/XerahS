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

using XerahS.Core.Security;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Core.Uploaders;

public static class ProviderContextManager
{
    private static IProviderContext? _context;
    private static readonly object _lock = new();

    public static IProviderContext? Current => _context;

    public static IProviderContext EnsureProviderContext()
    {
        lock (_lock)
        {
            if (_context != null)
            {
                return _context;
            }

            var secretsPath = SettingsManager.SecretsStoreFilePath;
            var secretsStore = new SecretStore(secretsPath);
            _context = new CoreProviderContext(secretsStore);
            ProviderCatalog.SetProviderContext(_context);

            InstanceManager.Instance.MigrateSecretsIfNeeded();

            return _context;
        }
    }

    private sealed class CoreProviderContext : IProviderContext
    {
        public CoreProviderContext(ISecretStore secrets)
        {
            Secrets = secrets;
        }

        public ISecretStore Secrets { get; }
    }
}
