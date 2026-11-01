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

namespace XerahS.Tests.Assistant;

[TestFixture]
public sealed class HistoryManagerSQLiteTests
{
    [Test]
    public void AppendHistoryItem_RaisesHistoryAppendedOnlyForSavedItems()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        int raised = 0;
        Action handler = () => Interlocked.Increment(ref raised);
        HistoryManager.HistoryAppended += handler;

        try
        {
            using var manager = new HistoryManagerSQLite(Path.Combine(tempDirectory, "history.db"));
            manager.AppendHistoryItem(new HistoryItem { FileName = string.Empty });
            Assert.That(raised, Is.EqualTo(0));

            Assert.That(manager.AppendHistoryItem(new HistoryItem
            {
                FileName = "done.png",
                DateTime = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc),
                URL = "https://cdn.example.test/done.png"
            }), Is.True);
            Assert.That(raised, Is.EqualTo(1));
        }
        finally
        {
            HistoryManager.HistoryAppended -= handler;
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void GetHistoryItem_ReturnsExactDurableRow()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var expected = new HistoryItem
            {
                FileName = "toast-capture.png",
                FilePath = Path.Combine(tempDirectory, "toast-capture.png"),
                DateTime = new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc),
                Type = "Image",
                URL = "https://cdn.example.test/toast-capture.png"
            };
            using var manager = new HistoryManagerSQLite(Path.Combine(tempDirectory, "history.db"));
            Assert.That(manager.AppendHistoryItem(expected), Is.True);

            HistoryItem? actual = manager.GetHistoryItem(expected.Id);

            Assert.Multiple(() =>
            {
                Assert.That(actual, Is.Not.Null);
                Assert.That(actual!.Id, Is.EqualTo(expected.Id));
                Assert.That(actual.URL, Is.EqualTo(expected.URL));
                Assert.That(manager.GetHistoryItem(0), Is.Null);
                Assert.That(manager.GetHistoryItem(long.MaxValue), Is.Null);
            });
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void SearchHistoryItems_ReturnsPagedMetadataAndOcrMatches()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string dbPath = Path.Combine(tempDirectory, "history.db");
            var metadataItem = new HistoryItem
            {
                FileName = "quarterly-roadmap.png",
                FilePath = Path.Combine(tempDirectory, "metadata.png"),
                DateTime = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                Type = "Image"
            };
            var ocrItem = new HistoryItem
            {
                FileName = "capture.png",
                FilePath = Path.Combine(tempDirectory, "ocr.png"),
                DateTime = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc),
                Type = "Image"
            };

            using var manager = new HistoryManagerSQLite(dbPath);
            manager.AppendHistoryItem(metadataItem);
            manager.AppendHistoryItem(ocrItem);
            new HistoryOcrIndexStore(dbPath).UpsertText(ocrItem.Id, ocrItem.FilePath, null, "Quarterly roadmap review", "test", "en");

            (List<HistoryItem> firstPage, int totalCount) = manager.SearchHistoryItems("ROADMAP", 0, 1);
            (List<HistoryItem> secondPage, int repeatedCount) = manager.SearchHistoryItems("roadmap", 1, 1);

            Assert.Multiple(() =>
            {
                Assert.That(totalCount, Is.EqualTo(2));
                Assert.That(repeatedCount, Is.EqualTo(2));
                Assert.That(firstPage.Select(item => item.Id), Is.EqualTo(new[] { ocrItem.Id }));
                Assert.That(secondPage.Select(item => item.Id), Is.EqualTo(new[] { metadataItem.Id }));
            });
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void ContainsFilePath_FindsEntriesBeyondFirstPage()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string dbPath = Path.Combine(tempDirectory, "history.db");

            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                for (int i = 0; i < 1005; i++)
                {
                    manager.AppendHistoryItem(new HistoryItem
                    {
                        FileName = $"shot-{i}.png",
                        FilePath = Path.Combine(tempDirectory, $"shot-{i}.png"),
                        DateTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(i),
                        Type = "Image"
                    });
                }

                string lastFilePath = Path.Combine(tempDirectory, "shot-1004.png");
                Assert.That(manager.ContainsFilePath(lastFilePath, pageSize: 200), Is.True);
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }


    [Test]
    public void ContainsFilePath_MatchesSymbolicLinkEquivalentPath()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string dbPath = Path.Combine(tempDirectory, "history.db");
            string targetPath = Path.Combine(tempDirectory, "target.png");
            string linkPath = Path.Combine(tempDirectory, "linked.png");
            File.WriteAllText(targetPath, "image placeholder");

            try
            {
                File.CreateSymbolicLink(linkPath, targetPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Ignore($"Symbolic links are not available in this test environment: {ex.Message}");
            }

            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "target.png",
                    FilePath = targetPath,
                    DateTime = new DateTime(2026, 5, 2, 7, 0, 0, DateTimeKind.Utc),
                    Type = "Image"
                });

                Assert.That(manager.ContainsFilePath(linkPath), Is.True);
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void ContainsFilePath_UsesHostPathCasingSemantics()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string dbPath = Path.Combine(tempDirectory, "history.db");

            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                string originalPath = Path.Combine(tempDirectory, "shot.png");
                string differentCasePath = Path.Combine(tempDirectory, "SHOT.png");

                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "shot.png",
                    FilePath = originalPath,
                    DateTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Type = "Image"
                });

                bool exists = manager.ContainsFilePath(differentCasePath);

                if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
                {
                    Assert.That(exists, Is.True);
                }
                else
                {
                    Assert.That(exists, Is.False);
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void GetLatestByFilePath_ReturnsNewestMatchingHistoryItem()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string dbPath = Path.Combine(tempDirectory, "history.db");
            string filePath = Path.Combine(tempDirectory, "shot.png");

            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "shot-old.png",
                    FilePath = filePath,
                    DateTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Type = "Image",
                    Tags = new Dictionary<string, string?> { ["OcrText"] = "old" }
                });

                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "shot-new.png",
                    FilePath = filePath,
                    DateTime = new DateTime(2026, 1, 1, 0, 1, 0, DateTimeKind.Utc),
                    Type = "Image",
                    Tags = new Dictionary<string, string?> { ["OcrText"] = "new" }
                });

                HistoryItem? match = manager.GetLatestByFilePath(filePath);

                Assert.That(match, Is.Not.Null);
                Assert.That(match!.FileName, Is.EqualTo("shot-new.png"));
                Assert.That(match.Tags["OcrText"], Is.EqualTo("new"));
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void Delete_RemovesOcrIndexRows()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string dbPath = Path.Combine(tempDirectory, "history.db");

            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "shot1.png",
                    FilePath = Path.Combine(tempDirectory, "shot1.png"),
                    DateTime = new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Utc),
                    Type = "Image"
                });
                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "shot2.png",
                    FilePath = Path.Combine(tempDirectory, "shot2.png"),
                    DateTime = new DateTime(2026, 5, 18, 0, 1, 0, DateTimeKind.Utc),
                    Type = "Image"
                });
            }

            var ocrStore = new HistoryOcrIndexStore(dbPath);
            ocrStore.UpsertText(1, Path.Combine(tempDirectory, "shot1.png"), null, "Alpha text", "test", "en");
            ocrStore.UpsertText(2, Path.Combine(tempDirectory, "shot2.png"), null, "Beta text", "test", "en");

            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                manager.Delete(new HistoryItem { Id = 1 });
            }

            Assert.That(ocrStore.GetText(1), Is.Null, "OCR text for deleted item 1 should be removed");
            Assert.That(ocrStore.GetText(2), Is.EqualTo("Beta text"), "OCR text for non-deleted item 2 should remain");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void Delete_RemovesOcrIndexRows_BulkDelete()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string dbPath = Path.Combine(tempDirectory, "history.db");

            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "shot1.png",
                    FilePath = Path.Combine(tempDirectory, "shot1.png"),
                    DateTime = new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Utc),
                    Type = "Image"
                });
                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "shot2.png",
                    FilePath = Path.Combine(tempDirectory, "shot2.png"),
                    DateTime = new DateTime(2026, 5, 18, 0, 1, 0, DateTimeKind.Utc),
                    Type = "Image"
                });
                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "shot3.png",
                    FilePath = Path.Combine(tempDirectory, "shot3.png"),
                    DateTime = new DateTime(2026, 5, 18, 0, 2, 0, DateTimeKind.Utc),
                    Type = "Image"
                });
            }

            var ocrStore = new HistoryOcrIndexStore(dbPath);
            ocrStore.UpsertText(1, Path.Combine(tempDirectory, "shot1.png"), null, "Alpha", "test", "en");
            ocrStore.UpsertText(2, Path.Combine(tempDirectory, "shot2.png"), null, "Beta", "test", "en");
            ocrStore.UpsertText(3, Path.Combine(tempDirectory, "shot3.png"), null, "Gamma", "test", "en");

            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                manager.Delete(new HistoryItem { Id = 1 }, new HistoryItem { Id = 3 });
            }

            Assert.That(ocrStore.GetText(1), Is.Null, "OCR text for deleted item 1 should be removed");
            Assert.That(ocrStore.GetText(2), Is.EqualTo("Beta"), "OCR text for non-deleted item 2 should remain");
            Assert.That(ocrStore.GetText(3), Is.Null, "OCR text for deleted item 3 should be removed");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void Delete_Noop_WhenNoOcrIndexRowsExist()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"xerahs-history-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string dbPath = Path.Combine(tempDirectory, "history.db");

            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                manager.AppendHistoryItem(new HistoryItem
                {
                    FileName = "shot1.png",
                    FilePath = Path.Combine(tempDirectory, "shot1.png"),
                    DateTime = new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Utc),
                    Type = "Image"
                });
            }

            // No OCR rows exist yet — delete should not throw.
            using (var manager = new HistoryManagerSQLite(dbPath))
            {
                Assert.DoesNotThrow(() => manager.Delete(new HistoryItem { Id = 1 }));
            }

            var ocrStore = new HistoryOcrIndexStore(dbPath);
            Assert.That(ocrStore.GetText(1), Is.Null);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
