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

using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using XerahS.Core;
using XerahS.Core.Tasks;
using XerahS.Core.Tasks.Pipeline;
using XerahS.Core.Tasks.Processors;
using XerahS.History;
using XerahS.Platform.Abstractions;

namespace XerahS.Tests.Tasks;

[TestFixture]
public class CaptureAppMetadataTests
{
    [Test]
    public void CaptureSourceFlowsThroughHistoryToTopApps()
    {
        using var process = Process.GetCurrentProcess();
        var windows = DispatchProxy.Create<IWindowService, WindowServiceProxy>();
        using var metadata = new TaskMetadata();
        CaptureStage.CaptureWindowMetadata(metadata, windows);
        var info = new TaskInfo(new TaskSettings()) { Metadata = metadata, FilePath = "capture.png" };
        HistoryItem history = UploadJobProcessor.CreateHistoryItem(info, "");
        string directory = Path.Combine(Path.GetTempPath(), "xerahs-app-metadata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var manager = new HistoryManagerSQLite(Path.Combine(directory, "history.db"));
            Assert.That(manager.AppendHistoryItem(history), Is.True);
            HistoryStats stats = HistoryStats.Calculate(manager.GetStatisticsItems(), DateTime.Now, DayOfWeek.Monday);
            Assert.Multiple(() =>
            {
                Assert.That(metadata.WindowTitle, Is.EqualTo("Capture source"));
                Assert.That(history.TagsProcessName, Is.EqualTo(process.ProcessName));
                Assert.That(stats.Apps, Has.Count.EqualTo(1));
                Assert.That(stats.Apps[0].Name, Is.EqualTo(process.ProcessName));
                Assert.That(stats.Apps[0].Count, Is.EqualTo(1));
            });
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public void ExplicitCaptureWindowOverridesForegroundAndRecordingKeepsItsApp()
    {
        using var process = Process.GetCurrentProcess();
        var windows = DispatchProxy.Create<IWindowService, WindowServiceProxy>();
        ((WindowServiceProxy)(object)windows).ExpectedHandle = new IntPtr(42);
        using var metadata = new TaskMetadata();
        CaptureStage.CaptureWindowMetadata(metadata, windows, new IntPtr(42));
        var info = new TaskInfo(new TaskSettings()) { Metadata = metadata };
        var history = WorkerTask.CreateRecordingHistoryItem(info, "clip.mp4");
        Assert.That(history.TagsProcessName, Is.EqualTo(process.ProcessName));
    }

    [TestCase(0u)]
    [TestCase(uint.MaxValue)]
    [TestCase((uint)int.MaxValue)]
    public void UnavailableProcessDoesNotBreakCaptureOrRetainStaleApp(uint processId)
    {
        using var metadata = new TaskMetadata { ProcessName = "previous-app" };
        Assert.DoesNotThrow(() => metadata.UpdateInfo(new WindowInfo { ProcessId = processId, Title = "Capture source" }));
        Assert.That(metadata.ProcessName, Is.Null);
    }

    [Test]
    public void MissingWindowServiceLeavesExistingMetadataAlone()
    {
        using var metadata = new TaskMetadata { ProcessName = "provided-app" };
        Assert.DoesNotThrow(() => CaptureStage.CaptureWindowMetadata(metadata, null));
        Assert.That(metadata.ProcessName, Is.EqualTo("provided-app"));
    }

    public class WindowServiceProxy : DispatchProxy
    {
        public IntPtr ExpectedHandle = new(1);
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IWindowService.GetForegroundWindow))
            {
                Assert.That(ExpectedHandle, Is.EqualTo(new IntPtr(1)), "Explicit capture targets must not use the foreground window.");
                return ExpectedHandle;
            }
            Assert.That(args![0], Is.EqualTo(ExpectedHandle));
            return method.Name switch
            {
                nameof(IWindowService.GetWindowText) => "Capture source",
                nameof(IWindowService.GetWindowProcessId) => (uint)Environment.ProcessId,
                _ => throw new NotSupportedException(method.Name)
            };
        }
    }
}
