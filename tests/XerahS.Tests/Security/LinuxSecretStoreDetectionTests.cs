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
using NUnit.Framework;
using XerahS.Core.Security;

namespace XerahS.Tests.Security;

[TestFixture]
public class LinuxSecretStoreDetectionTests
{
    /// <summary>secret-tool has no --version flag, so detection once always failed and Cloud sign-in was refused.</summary>
    [Test]
    public void SecretStore_UsesLibsecret_WhenTheKeyringIsReachable()
    {
        if (!OperatingSystem.IsLinux() || !KeyringReachable())
        {
            Assert.Ignore("No reachable Secret Service keyring on this machine.");
        }

        string directory = Path.Combine(Path.GetTempPath(), $"xerahs-secret-detect-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var store = new SecretStore(Path.Combine(directory, "SecretsStore.json"));

            Assert.Multiple(() =>
            {
                Assert.That(store.IsFallback, Is.False, store.BackendName);
                Assert.That(store.BackendName, Does.Contain("libsecret"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static bool KeyringReachable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "secret-tool",
                Arguments = "lookup service xerahs-test-probe key availability",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });

            string error = process!.StandardError.ReadToEnd();
            return process.WaitForExit(5000) && process.ExitCode is 0 or 1 && string.IsNullOrWhiteSpace(error);
        }
        catch
        {
            return false;
        }
    }
}
