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

using NUnit.Framework;
using XerahS.Core.Security;
using XerahS.Uploaders.PluginSystem;

namespace XerahS.Tests;

[TestFixture]
public class SecretStoreFallbackTests
{
    [Test]
    public void GetSecret_PrefersPrimaryAndFallsBackToLegacy()
    {
        var primary = new MemoryStore();
        var legacy = new MemoryStore();
        var store = new SecretStore.LegacyFileFallbackSecretStore(primary, legacy);

        legacy.SetSecret("p", "k", "n", "old");
        Assert.That(store.GetSecret("p", "k", "n"), Is.EqualTo("old"));
        Assert.That(store.HasSecret("p", "k", "n"), Is.True);

        primary.SetSecret("p", "k", "n", "new");
        Assert.That(store.GetSecret("p", "k", "n"), Is.EqualTo("new"));
    }

    [Test]
    public void SetSecret_WritesOnlyPrimary_AndDeleteClearsBoth()
    {
        var primary = new MemoryStore();
        var legacy = new MemoryStore();
        var store = new SecretStore.LegacyFileFallbackSecretStore(primary, legacy);

        store.SetSecret("p", "k", "n", "value");
        Assert.That(legacy.HasSecret("p", "k", "n"), Is.False);

        legacy.SetSecret("p", "k", "n", "stale");
        store.DeleteSecret("p", "k", "n");
        Assert.That(store.GetSecret("p", "k", "n"), Is.Null);
        Assert.That(store.HasSecret("p", "k", "n"), Is.False);
    }

    [Test]
    public void GetSecret_ReturnsNullWhenLegacyStoreThrows()
    {
        var store = new SecretStore.LegacyFileFallbackSecretStore(new MemoryStore(), new ThrowingStore());
        Assert.That(store.GetSecret("p", "k", "n"), Is.Null);
    }

    private sealed class MemoryStore : ISecretStore
    {
        private readonly Dictionary<string, string> _values = new();

        public string? GetSecret(string providerId, string secretKey, string name) =>
            _values.GetValueOrDefault($"{providerId}:{secretKey}:{name}");

        public void SetSecret(string providerId, string secretKey, string name, string value) =>
            _values[$"{providerId}:{secretKey}:{name}"] = value;

        public void DeleteSecret(string providerId, string secretKey, string name) =>
            _values.Remove($"{providerId}:{secretKey}:{name}");

        public bool HasSecret(string providerId, string secretKey, string name) =>
            _values.ContainsKey($"{providerId}:{secretKey}:{name}");
    }

    private sealed class ThrowingStore : ISecretStore
    {
        public string? GetSecret(string providerId, string secretKey, string name) => throw new IOException("unreadable");

        public void SetSecret(string providerId, string secretKey, string name, string value) => throw new IOException("unreadable");

        public void DeleteSecret(string providerId, string secretKey, string name) => throw new IOException("unreadable");

        public bool HasSecret(string providerId, string secretKey, string name) => throw new IOException("unreadable");
    }
}
