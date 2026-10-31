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
public class StatisticsTests
{
    [TestCase("capture.png", "Image", HistoryStatsKind.Image)]
    [TestCase("clip.gif", "Image", HistoryStatsKind.Recording)]
    [TestCase("clip.mp4", "File", HistoryStatsKind.Recording)]
    [TestCase("notes.txt", "Text", HistoryStatsKind.Other)]
    public void ClassifiesImagesAndRecordingsLikeShareX(string name, string type, HistoryStatsKind expected)
    {
        Assert.That(HistoryStats.GetKind(new() { FileName = name, Type = type }), Is.EqualTo(expected));
    }

    [Test]
    public void CalculatesRollingWeeksHoursAndStreaks()
    {
        DateTime now = new(2026, 10, 28, 12, 0, 0);
        HistoryItem[] items =
        [
            Item(now.AddHours(-1), "today.png"), Item(now.AddDays(-1), "yesterday.png"),
            Item(now.AddDays(-2), "clip.gif"), Item(now.AddDays(-8), "lastweek.txt"),
            Item(now.AddDays(-14), "outside.txt"), Item(now.AddDays(1), "future.png")
        ];
        HistoryStats stats = HistoryStats.Calculate(items, now, DayOfWeek.Monday);
        Assert.Multiple(() =>
        {
            Assert.That(stats.Total.Total, Is.EqualTo(6));
            Assert.That(stats.Total.Recordings, Is.EqualTo(1));
            Assert.That(stats.Past7Days, Is.EqualTo(3));
            Assert.That(stats.Previous7Days, Is.EqualTo(1));
            Assert.That(stats.Streak, Is.EqualTo(3));
            Assert.That(stats.TodayTally.Total, Is.EqualTo(1));
            Assert.That(stats.Hours[11].Images, Is.EqualTo(1));
            Assert.That(stats.Hours[12].Total, Is.EqualTo(5));
            Assert.That(stats.GetWeekStart(now), Is.EqualTo(new DateTime(2026, 10, 26)));
        });
    }

    [Test]
    public void ContinuesStreakFromYesterdayAndFoldsLongTail()
    {
        DateTime now = new(2026, 10, 28);
        var items = Enumerable.Range(1, 9).Select(i => Item(now.AddDays(-i), $"file{i}.png", $"app{i}")).ToArray();
        HistoryStats stats = HistoryStats.Calculate(items, now, DayOfWeek.Monday);
        Assert.Multiple(() =>
        {
            Assert.That(stats.Streak, Is.EqualTo(9));
            Assert.That(stats.LongestStreak, Is.EqualTo(9));
            Assert.That(stats.Apps, Has.Count.EqualTo(7));
            Assert.That(stats.Apps[^1].Name, Is.Empty);
            Assert.That(stats.Apps[^1].Count, Is.EqualTo(3));
        });
    }

    [Test]
    public void EmptyAndUndatedHistoryProduceEmptyDashboard()
    {
        HistoryStats stats = HistoryStats.Calculate([new HistoryItem()], new DateTime(2026, 10, 28), DayOfWeek.Monday);
        Assert.Multiple(() =>
        {
            Assert.That(stats.Total.Total, Is.Zero);
            Assert.That(stats.DaysCounted, Is.Zero);
            Assert.That(stats.FirstDay, Is.Null);
            Assert.That(stats.Streak, Is.Zero);
        });
    }

    [Test]
    public void DatabaseDashboardIncludesAllPages()
    {
        string directory = Path.Combine(Path.GetTempPath(), "xerahs-stats-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var manager = new HistoryManagerSQLite(Path.Combine(directory, "history.db"));
            var items = Enumerable.Range(0, 121).Select(i => Item(new DateTime(2026, 10, 27), $"capture-{i}.png")).ToArray();
            Assert.That(manager.AppendHistoryItems(items), Is.True);
            Assert.That(manager.GetHistoryItems(0, 50), Has.Count.EqualTo(50));
            var stats = HistoryStats.Calculate(manager.GetStatisticsItems(), new DateTime(2026, 10, 28), DayOfWeek.Monday);
            Assert.That(stats.Total.Total, Is.EqualTo(121));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static HistoryItem Item(DateTime date, string name, string? app = null) => new()
    {
        DateTime = date, FileName = name, FilePath = Path.Combine(Path.GetTempPath(), name),
        Tags = app == null ? new() : new() { ["ProcessName"] = app }
    };
}
