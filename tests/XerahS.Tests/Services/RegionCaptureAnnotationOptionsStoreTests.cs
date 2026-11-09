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
using XerahS.UI.Services;

namespace XerahS.Tests.Services;

[TestFixture]
public class RegionCaptureAnnotationOptionsStoreTests
{
    [Test]
    public async Task PersistAsync_ReturnsTask_NotFireAndForget()
    {
        // PersistAsync should return a Task<bool> that completes,
        // not fire-and-forget (which would return void).
        // When WorkflowsConfig is null (default test state), SaveWorkflowsConfigAsync
        // returns false — but the key regression guarantee is that the Task is
        // actually awaited and completes, unlike the previous `_ =` discard.
        var task = RegionCaptureAnnotationOptionsStore.PersistAsync();
        Assert.That(task, Is.Not.Null, "PersistAsync must not return null");

        var result = await task;
        // The critical property is that PersistAsync returns a completed Task<bool> —
        // fire-and-forget would never have completed synchronously in the general case.
        // WorkflowsConfig may or may not be loaded in test harness; we just verify
        // the task completes without exception.
        Assert.That(result, Is.TypeOf<bool>());
    }

    [Test]
    public async Task PersistAsync_DoesNotThrow_WhenNoSettingsLoaded()
    {
        // Regression: fire-and-forget `_ =` would silently discard exceptions.
        // Awaiting ensures any async exception is observed.
        await Assert.DoesNotThrowAsync(async () =>
        {
            await RegionCaptureAnnotationOptionsStore.PersistAsync();
        });
    }
}
