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
using XerahS.Tests.Xip0052;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.Editor;

[TestFixture]
public class HistoryCopyTargetsTests
{
    private static HistoryItem Item(long id, string? path, string? url) => new() { Id = id, FilePath = path ?? string.Empty, URL = url ?? string.Empty };

    [Test]
    public void JoinLines_PutsOneValuePerLineAndSkipsBlanks()
    {
        string joined = HistoryViewModel.JoinLines(new string?[] { "/a/one.png", null, "", "/b/two.png" });

        Assert.That(joined, Is.EqualTo("/a/one.png" + Environment.NewLine + "/b/two.png"));
    }

    [Test]
    public void CopyTargets_UseWholeSelectionOnlyWhenClickedEntryIsPartOfIt()
    {
        var viewModel = new HistoryViewModel(new FakeDesktopTaskManager(), new FakeDialogService(), false);
        HistoryItem a = Item(1, "/a.png", "https://x/a"), b = Item(2, "/b.png", ""), c = Item(3, "/c.png", "https://x/c");
        viewModel.HistoryItems.Add(a);
        viewModel.HistoryItems.Add(b);
        viewModel.HistoryItems.Add(c);

        viewModel.SetSelectedHistoryItems(new[] { a, b });

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.GetCopyTargets(a), Is.EqualTo(new[] { a, b }));
            Assert.That(viewModel.GetCopyTargets(c), Is.EqualTo(new[] { c }), "An entry outside the selection is copied alone.");
            Assert.That(viewModel.HasMultipleSelection, Is.True);
            Assert.That(viewModel.SelectedPathCount, Is.EqualTo(2));
            Assert.That(viewModel.SelectedUrlCount, Is.EqualTo(1));
            Assert.That(viewModel.IsInMultiSelection(a), Is.True);
            Assert.That(viewModel.IsInMultiSelection(c), Is.False);
        });

        viewModel.SetSelectedHistoryItems(new[] { a });
        Assert.That(viewModel.GetCopyTargets(a), Is.EqualTo(new[] { a }));
        Assert.That(viewModel.HasMultipleSelection, Is.False);
    }
}
