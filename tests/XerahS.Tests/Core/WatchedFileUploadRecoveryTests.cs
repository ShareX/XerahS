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
using XerahS.Core;
using XerahS.Core.Tasks.Processors;

namespace XerahS.Tests.Core;

[TestFixture]
public sealed class WatchedFileUploadRecoveryTests
{
    [Test]
    public void WouldMove_AcceptsAScreenshotInTheWatchedFolder()
    {
        var folder = new WatchFolderSettings
        {
            Enabled = true,
            MoveFilesToScreenshotsFolder = true,
            FolderPath = "/home/mike/Pictures",
            Filter = "screenshot-*.png"
        };

        Assert.That(WatchedFileUploadRecovery.WouldMove("/home/mike/Pictures/screenshot-2026-10-14_06-11-32.png", folder), Is.True);
        Assert.That(WatchedFileUploadRecovery.WouldMove("/home/mike/Pictures/notes.txt", folder), Is.False);
        Assert.That(WatchedFileUploadRecovery.WouldMove("/tmp/screenshot-2026-10-14_06-11-32.png", folder), Is.False);
    }
}
