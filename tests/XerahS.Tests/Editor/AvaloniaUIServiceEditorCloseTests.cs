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
using ShareX.ImageEditor.Presentation.ViewModels;
using XerahS.UI.Services;

namespace XerahS.Tests.Editor;

[TestFixture]
public class AvaloniaUIServiceEditorCloseTests
{
    [Test]
    public void ShouldReturnNullForEditorClose_ReturnsTrue_ForDirectWindowClose()
    {
        bool result = AvaloniaUIService.ShouldReturnNullForEditorClose(
            taskMode: false,
            closeRequestedByViewModel: false,
            taskResult: MainViewModel.EditorTaskResult.None);

        Assert.That(result, Is.True);
    }

    [TestCase(MainViewModel.EditorTaskResult.ContinueNoSave)]
    [TestCase(MainViewModel.EditorTaskResult.Cancel)]
    public void ShouldReturnNullForEditorClose_ReturnsTrue_ForTaskModeContinueWithoutSave(MainViewModel.EditorTaskResult taskResult)
    {
        bool result = AvaloniaUIService.ShouldReturnNullForEditorClose(
            taskMode: true,
            closeRequestedByViewModel: true,
            taskResult: taskResult);

        Assert.That(result, Is.True);
    }

    [Test]
    public void ShouldReturnNullForEditorClose_ReturnsFalse_ForSaveableViewModelClose()
    {
        bool result = AvaloniaUIService.ShouldReturnNullForEditorClose(
            taskMode: false,
            closeRequestedByViewModel: true,
            taskResult: MainViewModel.EditorTaskResult.Continue);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsTaskCancelRequested_ReturnsTrue_ForTaskModeCancel()
    {
        bool result = AvaloniaUIService.IsTaskCancelRequested(
            taskMode: true,
            closeRequestedByViewModel: true,
            taskResult: MainViewModel.EditorTaskResult.Cancel);

        Assert.That(result, Is.True);
    }

    [TestCase(true, true, MainViewModel.EditorTaskResult.ContinueNoSave)]
    [TestCase(true, true, MainViewModel.EditorTaskResult.Continue)]
    [TestCase(false, true, MainViewModel.EditorTaskResult.Cancel)]
    [TestCase(true, false, MainViewModel.EditorTaskResult.Cancel)]
    public void IsTaskCancelRequested_ReturnsFalse_WhenNotTaskModeCancel(
        bool taskMode,
        bool closeRequestedByViewModel,
        MainViewModel.EditorTaskResult taskResult)
    {
        bool result = AvaloniaUIService.IsTaskCancelRequested(taskMode, closeRequestedByViewModel, taskResult);

        Assert.That(result, Is.False);
    }
}
