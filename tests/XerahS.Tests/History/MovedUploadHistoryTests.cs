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
using XerahS.History;

namespace XerahS.Tests.History;

[TestFixture]
public sealed class MovedUploadHistoryTests
{
    [Test]
    public void FindBySourceFileName_MatchesTheMovedPathAndIgnoresASuffixCollision()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "xerahs-moved-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string fileName = "screenshot-2026-10-14_06-11-32.png";

        try
        {
            using var manager = new HistoryManagerSQLite(Path.Combine(tempDirectory, "history.db"));
            Assert.That(manager.AppendHistoryItem(new HistoryItem
            {
                FileName = "20261014T0611_Y0Y2WqEK4F.png",
                FilePath = Path.Combine(tempDirectory, "Screenshots", "2026-10", fileName),
                DateTime = DateTime.Now.AddMinutes(-1),
                Type = "File",
                URL = "https://mike.getsharex.com/ShareX/2026/10/20261014T0611_Y0Y2WqEK4F.png"
            }), Is.True);
            Assert.That(manager.AppendHistoryItem(new HistoryItem
            {
                FileName = "other.png",
                FilePath = Path.Combine(tempDirectory, "not-" + fileName),
                DateTime = DateTime.Now,
                Type = "File",
                URL = "https://example.test/not-the-screenshot"
            }), Is.True);

            HistoryItem? match = manager.FindBySourceFileName(fileName, DateTime.Now.AddMinutes(-10));

            Assert.That(match, Is.Not.Null);
            Assert.That(match!.URL, Is.EqualTo("https://mike.getsharex.com/ShareX/2026/10/20261014T0611_Y0Y2WqEK4F.png"));
            Assert.That(manager.FindBySourceFileName(fileName, DateTime.Now.AddSeconds(30)), Is.Null);
            Assert.That(manager.FindBySourceFileName("other-shot.png"), Is.Null);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
