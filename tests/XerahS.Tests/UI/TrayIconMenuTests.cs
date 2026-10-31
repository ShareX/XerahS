using System.Collections.Specialized;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NUnit.Framework;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.RegionCapture.ScreenRecording;
using XerahS.Tests.Xip0052;
using XerahS.UI;

namespace XerahS.Tests.UI;

[TestFixture, NonParallelizable]
public sealed class TrayIconMenuTests
{
    private TrayIconHelper _helper = null!;
    private WorkflowsConfig _originalWorkflows = null!;
    private bool _originalShowTray;

    [SetUp]
    public void SetUp()
    {
        _originalWorkflows = SettingsManager.WorkflowsConfig;
        _originalShowTray = SettingsManager.Settings.ShowTray;
        SettingsManager.WorkflowsConfig = new WorkflowsConfig { Hotkeys = [] };
    }

    private TrayIconHelper CreateHelper()
    {
        // Isolate the helper and its coordinator instead of mutating the application singleton.
        return _helper = (TrayIconHelper)Activator.CreateInstance(typeof(TrayIconHelper), nonPublic: true)!;
    }

    [TearDown]
    public void TearDown()
    {
        if (_helper != null)
        {
            var callback = typeof(TrayIconHelper).GetMethod("OnSettingsChanged", BindingFlags.NonPublic | BindingFlags.Instance)!;
            SettingsManager.SettingsChanged -= callback.CreateDelegate<EventHandler>(_helper);
        }
        SettingsManager.WorkflowsConfig = _originalWorkflows;
        SettingsManager.Settings.ShowTray = _originalShowTray;
    }

    [AvaloniaTest]
    public void Initialize_PreservesTheMenuAlreadyBoundToTheTrayIcon()
    {
        var helper = CreateHelper();
        var original = helper.TrayMenu;
        using var icon = new TrayIcon();
        using var binding = icon.Bind(TrayIcon.MenuProperty, new Binding(nameof(TrayIconHelper.TrayMenu)) { Source = helper });
        var coordinator = new FakeScreenRecordingCoordinator();

        helper.Initialize(coordinator);
        helper.Initialize(coordinator);
        helper.BuildTrayMenu();

        Assert.Multiple(() =>
        {
            Assert.That(helper.TrayMenu, Is.SameAs(original), "macOS keeps its native proxy tied to this menu.");
            Assert.That(icon.Menu, Is.SameAs(original));
            Assert.That(Headers(helper), Does.Contain("Exit"));
        });
    }

    [AvaloniaTest]
    public void WorkflowChanges_UpdateTheExportedMenuWithoutLeavingOldItemsOrParents()
    {
        var helper = CreateHelper();
        var original = helper.TrayMenu;
        var oldItems = original.Items.ToArray();
        SettingsManager.WorkflowsConfig.Hotkeys = Enumerable.Range(1, 4).Select(i =>
            new WorkflowSettings(WorkflowType.RectangleRegion, new HotkeyInfo()) { Name = $"Workflow {i}", PinnedToTray = true }).ToList();

        helper.BuildTrayMenu();
        Assert.That(Headers(helper).Take(4), Is.EqualTo(new[] { "Workflow 1", "Workflow 2", "Workflow 3", "Workflow 4" }));
        SettingsManager.WorkflowsConfig.Hotkeys[3].PinnedToTray = false;
        helper.BuildTrayMenu();

        Assert.Multiple(() =>
        {
            Assert.That(helper.TrayMenu, Is.SameAs(original));
            Assert.That(Headers(helper), Does.Not.Contain("Workflow 4"));
            Assert.That(Headers(helper).Count(h => h == "Exit"), Is.EqualTo(1));
            Assert.That(oldItems.All(item => item.Parent == null), Is.True);
            Assert.That(original.Items.All(item => ReferenceEquals(item.Parent, original)), Is.True);
        });
    }

    [AvaloniaTest]
    public void RecordingTransitions_RefreshControlsOnTheSameMenu()
    {
        var helper = CreateHelper();
        var coordinator = new FakeScreenRecordingCoordinator();
        helper.Initialize(coordinator);
        var original = helper.TrayMenu;

        coordinator.RaiseStatusChanged(RecordingStatus.Initializing, TimeSpan.Zero);
        Dispatcher.UIThread.RunJobs();
        Assert.That(Headers(helper), Does.Contain("Abort Recording").And.Not.Contain("Stop Recording"));
        coordinator.RaiseStatusChanged(RecordingStatus.Recording, TimeSpan.Zero);
        Dispatcher.UIThread.RunJobs();
        Assert.That(Headers(helper), Does.Contain("Pause Recording").And.Contain("Stop Recording"));
        original.Items.OfType<NativeMenuItem>().Single(item => (string?)item.Header == "Stop Recording").Command!.Execute(null);
        Assert.That(coordinator.StopCalls, Is.EqualTo(1));
        coordinator.RaiseStatusChanged(RecordingStatus.Paused, TimeSpan.Zero);
        Dispatcher.UIThread.RunJobs();
        Assert.That(Headers(helper), Does.Contain("Resume Recording").And.Not.Contain("Pause Recording"));
        coordinator.RaiseStatusChanged(RecordingStatus.Idle, TimeSpan.Zero);
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(helper.TrayMenu, Is.SameAs(original));
            Assert.That(Headers(helper), Does.Not.Contain("Stop Recording").And.Not.Contain("Abort Recording"));
            Assert.That(Headers(helper).Count(h => h == "Exit"), Is.EqualTo(1));
        });
    }

    [AvaloniaTest]
    [TestCase(false), TestCase(true)]
    public void WorkerRefresh_UpdatesTheExistingMenuAndBindingsOnlyOnTheUiThread(bool settingsEvent)
    {
        var helper = CreateHelper();
        var original = helper.TrayMenu;
        var mutationsOnUiThread = new List<bool>();
        var notificationsOnUiThread = new List<bool>();
        ((INotifyCollectionChanged)original.Items).CollectionChanged += (_, _) => mutationsOnUiThread.Add(Dispatcher.UIThread.CheckAccess());
        helper.PropertyChanged += (_, _) => notificationsOnUiThread.Add(Dispatcher.UIThread.CheckAccess());
        SettingsManager.WorkflowsConfig.Hotkeys = [new WorkflowSettings(WorkflowType.RectangleRegion, new HotkeyInfo()) { Name = "New workflow" }];
        SettingsManager.Settings.ShowTray = !_originalShowTray;

        Exception? workerError = null;
        var worker = new Thread(() =>
        {
            try
            {
                if (settingsEvent) SettingsManager.RaiseSettingsChanged();
                else helper.RefreshFromSettings();
            }
            catch (Exception error) { workerError = error; }
        });
        worker.Start();
        worker.Join();
        Assert.That(workerError, Is.Null);
        Assert.That(Headers(helper), Does.Not.Contain("New workflow"), "The worker must queue the update.");
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(helper.TrayMenu, Is.SameAs(original));
            Assert.That(Headers(helper), Does.Contain("New workflow"));
            Assert.That(helper.ShowTray, Is.EqualTo(!_originalShowTray));
            Assert.That(mutationsOnUiThread, Is.Not.Empty.And.All.True);
            Assert.That(notificationsOnUiThread, Is.Not.Empty.And.All.True);
        });
    }

    private static string?[] Headers(TrayIconHelper helper) => helper.TrayMenu.Items.OfType<NativeMenuItem>().Select(item => item.Header?.ToString()).ToArray();
}
