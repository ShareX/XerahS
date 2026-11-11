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

using Avalonia.Headless.NUnit;
using NUnit.Framework;
using SkiaSharp;
using XerahS.Core;
using XerahS.UI.ViewModels;

namespace XerahS.Tests.UI;

[TestFixture]
public sealed class AfterCaptureGoalTests
{
    private SKBitmap _image = null!;
    private AfterCaptureViewModel? _viewModel;

    [SetUp]
    public void SetUp()
    {
        _image = new SKBitmap(2, 2);
    }

    [TearDown]
    public void TearDown()
    {
        _viewModel?.PreviewImage.Dispose();
        _viewModel = null;
        _image.Dispose();
    }

    [AvaloniaTest]
    public void ImageEffects_StartAsTheWorkflowHasThemAndCanBeSkippedForThisCapture()
    {
        _viewModel = new AfterCaptureViewModel(
            _image,
            AfterCaptureTasks.AddImageEffects | AfterCaptureTasks.CopyImageToClipboard,
            AfterUploadTasks.None);

        Assert.That(_viewModel.AddImageEffects, Is.True);

        _viewModel.AddImageEffects = false;

        Assert.That(_viewModel.AfterCaptureTasks.HasFlag(AfterCaptureTasks.AddImageEffects), Is.False);
        Assert.That(_viewModel.AfterCaptureTasks.HasFlag(AfterCaptureTasks.CopyImageToClipboard), Is.True);

        // Changing the goal keeps the choice.
        _viewModel.SelectedGoal = AfterCaptureGoal.CopyFilePath;
        Assert.That(_viewModel.AddImageEffects, Is.False);

        _viewModel.AddImageEffects = true;
        _viewModel.SelectedGoal = AfterCaptureGoal.CopyUrl;
        Assert.That(_viewModel.AfterCaptureTasks.HasFlag(AfterCaptureTasks.AddImageEffects), Is.True);
    }

    [AvaloniaTest]
    public void ImageEffects_AreOffWhenTheWorkflowHasThemOff()
    {
        _viewModel = new AfterCaptureViewModel(_image, AfterCaptureTasks.CopyImageToClipboard, AfterUploadTasks.None);

        Assert.That(_viewModel.AddImageEffects, Is.False);
    }

    [AvaloniaTest]
    public void InferGoal_PrefersCopyUrlWhenUploadOrCopyUrlIsSet()
    {
        Assert.That(
            AfterCaptureViewModel.InferGoal(
                AfterCaptureTasks.UploadImageToHost | AfterCaptureTasks.CopyImageToClipboard,
                AfterUploadTasks.None),
            Is.EqualTo(AfterCaptureGoal.CopyUrl));
        Assert.That(
            AfterCaptureViewModel.InferGoal(AfterCaptureTasks.None, AfterUploadTasks.CopyURLToClipboard),
            Is.EqualTo(AfterCaptureGoal.CopyUrl));
    }

    [AvaloniaTest]
    public void InferGoal_UsesCopyFilePathThenCopyImageThenCopyUrl()
    {
        Assert.That(
            AfterCaptureViewModel.InferGoal(AfterCaptureTasks.CopyFilePathToClipboard, AfterUploadTasks.None),
            Is.EqualTo(AfterCaptureGoal.CopyFilePath));
        Assert.That(
            AfterCaptureViewModel.InferGoal(AfterCaptureTasks.CopyImageToClipboard, AfterUploadTasks.None),
            Is.EqualTo(AfterCaptureGoal.CopyImage));
        Assert.That(
            AfterCaptureViewModel.InferGoal(AfterCaptureTasks.ShowAfterCaptureWindow, AfterUploadTasks.None),
            Is.EqualTo(AfterCaptureGoal.CopyUrl));
    }

    [AvaloniaTest]
    public void Constructor_AppliesCopyUrlFlagsAndPreservesShowAfterCaptureWindow()
    {
        _viewModel = Create(
            AfterCaptureTasks.ShowAfterCaptureWindow | AfterCaptureTasks.UploadImageToHost,
            AfterUploadTasks.CopyURLToClipboard);

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.SelectedGoal, Is.EqualTo(AfterCaptureGoal.CopyUrl));
            Assert.That(_viewModel.AfterCaptureTasks.HasFlag(AfterCaptureTasks.ShowAfterCaptureWindow), Is.True);
            Assert.That(_viewModel.AfterCaptureTasks.HasFlag(AfterCaptureTasks.UploadImageToHost), Is.True);
            Assert.That(_viewModel.AfterCaptureTasks.HasFlag(AfterCaptureTasks.CopyImageToClipboard), Is.False);
            Assert.That(_viewModel.AfterUploadTasks.HasFlag(AfterUploadTasks.CopyURLToClipboard), Is.True);
        });
    }

    [AvaloniaTest]
    public void Constructor_CopyUrlGoalAddsUploadWhenOnlyCopyUrlFlagIsPresent()
    {
        _viewModel = Create(AfterCaptureTasks.None, AfterUploadTasks.CopyURLToClipboard);

        Assert.That(_viewModel.UploadImageToHost, Is.True);
        Assert.That(_viewModel.CopyURLToClipboard, Is.True);
    }

    [AvaloniaTest]
    public void SwitchingToCopyImage_SetsCopyImageAndDropsUploadAndUrl()
    {
        _viewModel = Create(
            AfterCaptureTasks.ShowAfterCaptureWindow
            | AfterCaptureTasks.UploadImageToHost
            | AfterCaptureTasks.SaveImageToFile
            | AfterCaptureTasks.AnnotateMedia,
            AfterUploadTasks.CopyURLToClipboard | AfterUploadTasks.UseURLShortener);

        _viewModel.SelectedGoal = AfterCaptureGoal.CopyImage;

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.CopyImageToClipboard, Is.True);
            Assert.That(_viewModel.UploadImageToHost, Is.False);
            Assert.That(_viewModel.CopyURLToClipboard, Is.False);
            Assert.That(_viewModel.CopyFilePathToClipboard, Is.False);
            Assert.That(_viewModel.SaveImageToFile, Is.True);
            Assert.That(_viewModel.AnnotateMedia, Is.True);
            Assert.That(_viewModel.UseURLShortener, Is.True);
            Assert.That(_viewModel.ShowSaveImageOption, Is.True);
            Assert.That(_viewModel.ShowUrlOptions, Is.False);
            Assert.That(
                _viewModel.AfterCaptureTasks.HasFlag(AfterCaptureTasks.ShowAfterCaptureWindow),
                Is.True);
        });
    }

    [AvaloniaTest]
    public void SwitchingToCopyFilePath_ForcesSaveAndCopyPath()
    {
        _viewModel = Create(
            AfterCaptureTasks.CopyImageToClipboard | AfterCaptureTasks.AnnotateMedia,
            AfterUploadTasks.None);

        _viewModel.SelectedGoal = AfterCaptureGoal.CopyFilePath;

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.SaveImageToFile, Is.True);
            Assert.That(_viewModel.CopyFilePathToClipboard, Is.True);
            Assert.That(_viewModel.CopyImageToClipboard, Is.False);
            Assert.That(_viewModel.UploadImageToHost, Is.False);
            Assert.That(_viewModel.AnnotateMedia, Is.True);
            Assert.That(_viewModel.ShowSaveImageOption, Is.False);
        });
    }

    [AvaloniaTest]
    public void SwitchingToCopyUrl_SetsUploadAndCopyUrlAndKeepsSave()
    {
        _viewModel = Create(
            AfterCaptureTasks.CopyImageToClipboard | AfterCaptureTasks.SaveImageToFile,
            AfterUploadTasks.None);

        _viewModel.SelectedGoal = AfterCaptureGoal.CopyUrl;

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.UploadImageToHost, Is.True);
            Assert.That(_viewModel.CopyURLToClipboard, Is.True);
            Assert.That(_viewModel.CopyImageToClipboard, Is.False);
            Assert.That(_viewModel.CopyFilePathToClipboard, Is.False);
            Assert.That(_viewModel.SaveImageToFile, Is.True);
            Assert.That(_viewModel.ShowUrlOptions, Is.True);
        });
    }

    [AvaloniaTest]
    public void CopyImage_AllowsOptionalSaveAndIgnoresUncheckingCopyImage()
    {
        _viewModel = Create(AfterCaptureTasks.CopyImageToClipboard, AfterUploadTasks.None);

        _viewModel.SaveImageToFile = true;
        _viewModel.CopyImageToClipboard = false;

        Assert.That(_viewModel.SaveImageToFile, Is.True);
        Assert.That(_viewModel.CopyImageToClipboard, Is.True);
    }

    [AvaloniaTest]
    public void CopyFilePath_IgnoresUncheckingSave()
    {
        _viewModel = Create(AfterCaptureTasks.CopyFilePathToClipboard, AfterUploadTasks.None);

        _viewModel.SaveImageToFile = false;

        Assert.That(_viewModel.SaveImageToFile, Is.True);
        Assert.That(_viewModel.CopyFilePathToClipboard, Is.True);
    }

    [AvaloniaTest]
    public void CopyUrl_IgnoresUncheckingUploadAndCopyUrl()
    {
        _viewModel = Create(
            AfterCaptureTasks.UploadImageToHost,
            AfterUploadTasks.CopyURLToClipboard);

        _viewModel.UploadImageToHost = false;
        _viewModel.CopyURLToClipboard = false;

        Assert.That(_viewModel.UploadImageToHost, Is.True);
        Assert.That(_viewModel.CopyURLToClipboard, Is.True);
    }

    [AvaloniaTest]
    public void OptionalExtras_PersistWhenSwitchingGoals()
    {
        _viewModel = Create(
            AfterCaptureTasks.UploadImageToHost | AfterCaptureTasks.AnnotateMedia,
            AfterUploadTasks.CopyURLToClipboard);

        _viewModel.CopyOcrTextToClipboard = true;
        _viewModel.UseURLShortener = true;
        _viewModel.SelectedGoal = AfterCaptureGoal.CopyImage;
        _viewModel.SelectedGoal = AfterCaptureGoal.CopyUrl;

        Assert.That(_viewModel.AnnotateMedia, Is.True);
        Assert.That(_viewModel.CopyOcrTextToClipboard, Is.True);
        Assert.That(_viewModel.UseURLShortener, Is.True);
        Assert.That(_viewModel.UploadImageToHost, Is.True);
        Assert.That(_viewModel.CopyURLToClipboard, Is.True);
    }

    [AvaloniaTest]
    public void Continue_KeepsCopyUrlFlagsAndDoesNotMarkQuickAction()
    {
        _viewModel = Create(
            AfterCaptureTasks.ShowAfterCaptureWindow | AfterCaptureTasks.UploadImageToHost,
            AfterUploadTasks.CopyURLToClipboard);
        bool closeRequested = false;
        _viewModel.RequestClose += () => closeRequested = true;

        _viewModel.ContinueCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.Cancelled, Is.False);
            Assert.That(_viewModel.QuickAction, Is.EqualTo(AfterCaptureQuickAction.None));
            Assert.That(_viewModel.AfterCaptureTasks.HasFlag(AfterCaptureTasks.UploadImageToHost), Is.True);
            Assert.That(_viewModel.AfterUploadTasks.HasFlag(AfterUploadTasks.CopyURLToClipboard), Is.True);
            Assert.That(closeRequested, Is.True);
        });
    }

    [AvaloniaTest]
    public void Cancel_LeavesCancelledTrue()
    {
        _viewModel = Create(AfterCaptureTasks.CopyImageToClipboard, AfterUploadTasks.None);
        bool closeRequested = false;
        _viewModel.RequestClose += () => closeRequested = true;

        _viewModel.CancelCommand.Execute(null);

        Assert.That(_viewModel.Cancelled, Is.True);
        Assert.That(closeRequested, Is.True);
    }

    private AfterCaptureViewModel Create(AfterCaptureTasks capture, AfterUploadTasks upload)
    {
        return new AfterCaptureViewModel(_image, capture, upload);
    }
}
