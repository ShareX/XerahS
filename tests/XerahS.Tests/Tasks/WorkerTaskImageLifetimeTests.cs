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
using SkiaSharp;
using XerahS.Common;
using XerahS.Core;
using XerahS.Core.Tasks;

namespace XerahS.Tests.Tasks;

[TestFixture]
[NonParallelizable]
public sealed class WorkerTaskImageLifetimeTests
{
    private static TaskSettings CreateSettings() => new()
    {
        Job = WorkflowType.PrintScreen,
        AfterCaptureJob = AfterCaptureTasks.None,
        AfterUploadJob = AfterUploadTasks.None
    };

    [Test]
    public async Task CompletedHistory_ReleasesFullResolutionImagesAndPreservesSuccess()
    {
        var tasks = new List<WorkerTask>();
        var images = new List<SKBitmap>();
        long capturedBytes = 0;
        try
        {
            for (int i = 0; i < 4; i++)
            {
                var image = new SKBitmap(3840, 2160);
                image.Erase(SKColors.CornflowerBlue);
                capturedBytes += image.ByteCount;
                images.Add(image);
                var task = WorkerTask.Create(CreateSettings(), image);
                tasks.Add(task);
                SKBitmap? preview = null;
                task.TaskCompleted += (_, _) =>
                {
                    Assert.That(task.Info.Metadata.Image, Is.SameAs(image));
                    Assert.That(image.Handle, Is.Not.EqualTo(IntPtr.Zero));
                    preview = image.Copy();
                };

                await task.StartAsync();
                using (preview)
                {
                    Assert.That(preview, Is.Not.Null);
                    Assert.That(preview!.GetPixel(0, 0), Is.EqualTo(SKColors.CornflowerBlue));
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(capturedBytes, Is.EqualTo(132_710_400));
                Assert.That(tasks.All(task => task.Info.Metadata.Image == null), Is.True);
                Assert.That(images.All(image => image.Handle == IntPtr.Zero), Is.True);
                Assert.That(tasks.All(task => task.IsSuccessful), Is.True);
            });

            // Completed tasks cannot restart their pipeline or resurrect released buffers.
            await tasks[0].StartAsync();
            Assert.That(tasks[0].Info.Metadata.Image, Is.Null);
        }
        finally
        {
            foreach (var task in tasks) task.Dispose();
            foreach (var image in images) image.Dispose();
        }
    }

    [Test]
    public async Task ThrowingCompletionHandler_StillReleasesImage()
    {
        using var image = new SKBitmap(16, 16);
        using var task = WorkerTask.Create(CreateSettings(), image);
        task.TaskCompleted += (_, _) => throw new InvalidOperationException("Subscriber failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => task.StartAsync());

        Assert.That(task.Info.Metadata.Image, Is.Null);
        Assert.That(image.Handle, Is.EqualTo(IntPtr.Zero));
    }

    [Test]
    public async Task DisposeDuringCompletion_DefersReleaseUntilAllHandlersReturn()
    {
        using var image = new SKBitmap(16, 16);
        using var task = WorkerTask.Create(CreateSettings(), image);
        task.TaskCompleted += (_, _) => task.Dispose();
        task.TaskCompleted += (_, _) => Assert.That(image.Handle, Is.Not.EqualTo(IntPtr.Zero));

        await task.StartAsync();

        Assert.That(task.Info.Metadata.Image, Is.Null);
        Assert.That(image.Handle, Is.EqualTo(IntPtr.Zero));
    }

    [Test]
    public async Task DisposeBeforeStart_ReleasesOwnedImage()
    {
        using var image = new SKBitmap(16, 16);
        using var task = WorkerTask.Create(CreateSettings(), image);

        task.Dispose();

        Assert.That(task.Info.Metadata.Image, Is.Null);
        Assert.That(image.Handle, Is.EqualTo(IntPtr.Zero));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => task.StartAsync());
    }

    [Test]
    public async Task ThrowingInitialStatusHandler_ReleasesImageAndFailsTask()
    {
        using var image = new SKBitmap(16, 16);
        using var task = WorkerTask.Create(CreateSettings(), image);
        task.StatusChanged += (_, _) =>
        {
            if (task.Status == XerahS.Core.TaskStatus.Preparing)
            {
                task.Dispose();
                throw new InvalidOperationException("Status subscriber failed");
            }
        };

        await task.StartAsync();

        Assert.That(task.IsSuccessful, Is.False);
        Assert.That(task.Info.Metadata.Image, Is.Null);
        Assert.That(image.Handle, Is.EqualTo(IntPtr.Zero));
    }

    [Test]
    public async Task StoppedTask_ReleasesImageWithoutReportingSuccess()
    {
        using var image = new SKBitmap(16, 16);
        using var task = WorkerTask.Create(CreateSettings(), image);
        task.StatusChanged += (_, _) =>
        {
            if (task.Status == XerahS.Core.TaskStatus.Preparing) task.Stop();
        };

        await task.StartAsync();

        Assert.That(task.IsSuccessful, Is.False);
        Assert.That(task.Info.Metadata.Image, Is.Null);
        Assert.That(image.Handle, Is.EqualTo(IntPtr.Zero));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task HistoryPruning_DoesNotDisposeQueuedTasksOrCompletionCallbacks(bool completing)
    {
        var manager = (XerahS.Core.Managers.TaskManager)Activator.CreateInstance(
            typeof(XerahS.Core.Managers.TaskManager), nonPublic: true)!;
        using var image = new SKBitmap(16, 16);
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        WorkerTask? retainedTask = null;
        void HoldTask(object? sender, WorkerTask task)
        {
            if (!ReferenceEquals(task.Info.Metadata.Image, image)) return;
            retainedTask = task;
            entered.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException();
        }
        if (completing) manager.TaskCompleted += HoldTask;
        else manager.TaskStarted += HoldTask;

        var first = Task.Run(() => manager.StartTask(CreateSettings(), image));
        try
        {
            Assert.That(entered.Wait(TimeSpan.FromSeconds(30)), Is.True);
            for (int i = 0; i < 101; i++)
            {
                await manager.StartTask(CreateSettings(), new SKBitmap(1, 1));
            }

            Assert.That(image.Handle, Is.Not.EqualTo(IntPtr.Zero));
            Assert.That(manager.Tasks, Does.Contain(retainedTask));
        }
        finally
        {
            resume.Set();
            await first;
            foreach (var task in manager.Tasks) task.Dispose();
        }
    }
}
