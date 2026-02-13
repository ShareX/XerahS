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
using XerahS.Common;
using XerahS.Core;

namespace XerahS.Tests.Helpers;

[TestFixture]
[NonParallelizable]
public class SettingsManagerSecretsPathTests
{
    [Test]
    public void SecretsStoreFilePath_UsesMachineSpecificFileName_WhenEnabled()
    {
        bool original = SettingsManager.Settings.UseMachineSpecificSecretsStore;
        try
        {
            SettingsManager.Settings.UseMachineSpecificSecretsStore = true;

            string expectedMachineName = FileHelpers.SanitizeFileName(Environment.MachineName);
            string fileName = Path.GetFileName(SettingsManager.SecretsStoreFilePath);

            if (string.IsNullOrEmpty(expectedMachineName))
            {
                Assert.That(fileName, Is.EqualTo(SettingsManager.SecretsStoreFileName));
            }
            else
            {
                Assert.That(fileName, Is.EqualTo($"{SettingsManager.SecretsStoreFileNamePrefix}-{expectedMachineName}.{SettingsManager.SecretsStoreFileNameExtension}"));
            }
        }
        finally
        {
            SettingsManager.Settings.UseMachineSpecificSecretsStore = original;
        }
    }

    [Test]
    public void SecretsStoreFilePath_UsesSharedFileName_WhenDisabled()
    {
        bool original = SettingsManager.Settings.UseMachineSpecificSecretsStore;
        try
        {
            SettingsManager.Settings.UseMachineSpecificSecretsStore = false;

            string fileName = Path.GetFileName(SettingsManager.SecretsStoreFilePath);
            Assert.That(fileName, Is.EqualTo(SettingsManager.SecretsStoreFileName));
        }
        finally
        {
            SettingsManager.Settings.UseMachineSpecificSecretsStore = original;
        }
    }
}
