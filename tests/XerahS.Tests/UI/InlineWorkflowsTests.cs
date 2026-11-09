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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Reflection;
using NUnit.Framework;
using ShareX.ImageEditor.Core.ImageEffects.Filters;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Platform.Abstractions;
using XerahS.Tests.Xip0052;
using XerahS.UI.ViewModels;
using XerahS.UI.Views;
using XerahS.UI.Services;

namespace XerahS.Tests.UI;

[TestFixture, NonParallelizable]
public class InlineWorkflowsTests
{
    [AvaloniaTest]
    public async Task AutosaveWritesWorkflowAndNestedSettingsToDisk()
    {
        using var scope = new Scope();
        string folder = Path.Combine(Path.GetTempPath(), "xerahs-inline-workflows-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "WorkflowsConfig.json");
        using var vm = new WorkflowsViewModel(new FakeUiViewModelFactory(), null,
            () => new WorkflowsConfig { Hotkeys = SettingsManager.WorkflowsConfig.Hotkeys }.Save(path));
        try
        {
            string id = vm.SelectedWorkflow!.Model.Id;
            vm.SelectedEditor!.Description = "Saved inline capture";
            vm.SelectedEditor.TaskSettings.RegionCaptureDimAllExceptSelection = true;
            await Task.Delay(450);
            Dispatcher.UIThread.RunJobs();

            var saved = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            var workflows = (Newtonsoft.Json.Linq.JArray)saved["Hotkeys"]!;
            Assert.Multiple(() =>
            {
                Assert.That(vm.HasSaveError, Is.False);
                Assert.That(workflows.Count, Is.EqualTo(4));
                Assert.That((string?)workflows[0]["Id"], Is.EqualTo(id));
                Assert.That((string?)workflows[0]["TaskSettings"]!["Description"], Is.EqualTo("Saved inline capture"));
                Assert.That((bool?)workflows[0]["TaskSettings"]!["CaptureSettings"]!["RegionCaptureOptions"]!["DimAllExceptSelection"], Is.True);
                Assert.That((string?)workflows[1]["TaskSettings"]!["Description"], Is.EqualTo("Region capture 2"));
            });
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [AvaloniaTest]
    public void SwitchingWorkflowFlushesAllSettingsAndKeepsEditorsInline()
    {
        using var scope = new Scope();
        using var vm = scope.Create();
        var first = vm.SelectedWorkflow!.Model;
        var editor = vm.SelectedEditor!;
        editor.Description = "Window captures";
        editor.SelectedKey = global::Avalonia.Input.Key.K;
        editor.SelectedModifiers = global::Avalonia.Input.KeyModifiers.Control;
        editor.TaskSettings.RegionCaptureDimAllExceptSelection = true;
        editor.TaskSettings.AdvancedSettings.ProcessImagesDuringFileUpload = true;
        editor.TaskSettings.NotifyAdvancedSettingsChanged();
        vm.SelectedWorkflow = vm.Workflows[1];
        Assert.Multiple(() =>
        {
            Assert.That(first.TaskSettings.Description, Is.EqualTo("Window captures"));
            Assert.That(first.HotkeyInfo.Key, Is.EqualTo(global::Avalonia.Input.Key.K));
            Assert.That(first.HotkeyInfo.Modifiers, Is.EqualTo(global::Avalonia.Input.KeyModifiers.Control));
            Assert.That(first.TaskSettings.CaptureSettings.RegionCaptureOptions.DimAllExceptSelection, Is.True);
            Assert.That(first.TaskSettings.AdvancedSettings.ProcessImagesDuringFileUpload, Is.True);
            Assert.That(vm.SelectedEditor!.IsInline, Is.True);
            Assert.That(scope.SaveCount, Is.EqualTo(1));
        });
        vm.SelectedWorkflow = vm.Workflows[0];
        Assert.That(vm.SelectedEditor!.Description, Is.EqualTo("Window captures"));
    }

    [AvaloniaTest]
    public async Task AutosaveDebouncesAndRetainsFocusedEditor()
    {
        using var scope = new Scope();
        using var vm = scope.Create();
        var editor = vm.SelectedEditor!;
        editor.Description = "One";
        editor.Description = "Two";
        Assert.That(scope.SaveCount, Is.Zero);
        await Task.Delay(400);
        Dispatcher.UIThread.RunJobs();
        Assert.Multiple(() =>
        {
            Assert.That(scope.SaveCount, Is.EqualTo(1));
            Assert.That(vm.SelectedEditor, Is.SameAs(editor));
            Assert.That(vm.SelectedWorkflow!.Description, Is.EqualTo("Two"));
            Assert.That(vm.SaveStatus, Is.EqualTo("All changes saved"));
        });
    }

    [AvaloniaTest]
    public void EffectsAndAdvancedChangesSaveAndFailureCanRetry()
    {
        using var scope = new Scope();
        using var vm = scope.Create();
        scope.Succeed = false;
        var editor = vm.SelectedEditor!;
        editor.TaskSettings.ImageEffects.Name = "Custom preset";
        var blur = new BlurImageEffect { Radius = 12 };
        editor.TaskSettings.ImageEffects.Effects.Add(blur);
        blur.Radius = 17;
        editor.TaskSettings.ImageEffects.NotifyEffectSettingsChanged();
        editor.TaskSettings.NotifyAdvancedSettingsChanged();
        vm.FlushAutoSave();
        Assert.That(vm.HasSaveError, Is.True);
        Assert.That(vm.SelectedWorkflow!.Model.TaskSettings.ImageSettings.ImageEffectsPreset.Name, Is.EqualTo("Custom preset"));
        Assert.That(vm.SelectedWorkflow.Model.TaskSettings.ImageSettings.ImageEffectsPreset.Effects.OfType<BlurImageEffect>().Last().Radius, Is.EqualTo(17));
        scope.Succeed = true;
        vm.RetrySaveCommand.Execute(null);
        Assert.That(vm.HasSaveError, Is.False);
        Assert.That(scope.SaveCount, Is.EqualTo(2));
    }

    [AvaloniaTest]
    public void AddDuplicateMovePinAndRemoveStayInlineAndKeepModelIdentity()
    {
        using var scope = new Scope();
        using var vm = scope.Create();
        vm.AddWorkflowCommand.Execute(null);
        string addedId = vm.SelectedWorkflow!.Model.Id;
        Assert.That(vm.Workflows.Count, Is.EqualTo(5));
        vm.SelectedEditor!.Description = "New capture";
        vm.TogglePinCommand.Execute(null);
        vm.SelectedEditor.Description = "Pinned capture";
        vm.FlushAutoSave();
        Assert.That(vm.SelectedWorkflow.PinnedToTray, Is.True);
        vm.DuplicateCommand.Execute(null);
        var duplicate = vm.SelectedWorkflow!.Model;
        Assert.That(duplicate.Id, Is.Not.EqualTo(addedId));
        Assert.That(duplicate.TaskSettings.WorkflowId, Is.EqualTo(duplicate.Id));
        Assert.That(duplicate.TaskSettings.Description, Is.EqualTo("Pinned capture"));
        vm.MoveUpCommand.Execute(null);
        Assert.That(vm.SelectedWorkflow!.Model, Is.SameAs(duplicate));
        Assert.That(vm.Workflows.IndexOf(vm.SelectedWorkflow), Is.EqualTo(4));
        vm.RemoveWorkflowCommand.Execute(null);
        Assert.That(vm.Workflows.Any(w => w.Model.Id == duplicate.Id), Is.False);
        Assert.That(vm.SelectedEditor, Is.Not.Null);
    }

    [AvaloniaTest]
    public void AutosavePreservesRegistrationAndManagerEventsDoNotReplaceEditor()
    {
        using var scope = new Scope();
        var hotkeys = new XerahS.Tests.Platform.Linux.QuickSetup.FakeHotkeyService("Test");
        using var manager = new WorkflowManager(hotkeys);
        manager.UpdateHotkeys(SettingsManager.WorkflowsConfig.Hotkeys);
        using var vm = new WorkflowsViewModel(new FakeUiViewModelFactory(), manager, () => true);
        var editor = vm.SelectedEditor!;
        editor.SelectedKey = global::Avalonia.Input.Key.K;
        editor.SelectedModifiers = global::Avalonia.Input.KeyModifiers.Control;
        vm.FlushAutoSave();
        Assert.That(hotkeys.Registrations, Has.Count.EqualTo(1));
        var registered = vm.SelectedWorkflow!.Model.HotkeyInfo;
        editor.TaskSettings.RegionCaptureDimAllExceptSelection = true;
        manager.NotifyWorkflowsChanged();
        Dispatcher.UIThread.RunJobs();
        Assert.That(vm.SelectedEditor, Is.SameAs(editor));
        vm.FlushAutoSave();
        Assert.Multiple(() =>
        {
            Assert.That(hotkeys.Registrations, Has.Count.EqualTo(1), "Editing image/capture settings must not re-register platform hotkeys.");
            Assert.That(vm.SelectedWorkflow.Model.HotkeyInfo, Is.SameAs(registered));
            Assert.That(editor.Model.HotkeyInfo.Id, Is.EqualTo(registered.Id));
        });
        vm.Deactivate();
        manager.NotifyWorkflowsChanged();
        vm.Activate();
        editor.Description = "After returning";
        vm.FlushAutoSave();
        Assert.That(vm.SelectedWorkflow.Description, Is.EqualTo("After returning"));
    }

    [AvaloniaTest]
    public async Task FFmpegOptionsEditTheOwningDraftInsteadOfOverwritingSource()
    {
        using var scope = new Scope();
        var source = SettingsManager.WorkflowsConfig.Hotkeys[0];
        source.TaskSettings.CaptureSettings.FFmpegOptions.x264_CRF = 23;
        var draft = new TaskSettings { WorkflowId = source.Id };
        draft.CaptureSettings.FFmpegOptions.x264_CRF = 16;
        var dialog = DispatchProxy.Create<IViewDialogService, FFmpegDialogProxy>();
        var settings = new TaskSettingsViewModel(draft, dialog);
        int modelChanges = 0;
        settings.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TaskSettingsViewModel.Model)) modelChanges++; };
        await settings.OpenFFmpegOptionsCommand.ExecuteAsync(null);
        Assert.That(draft.CaptureSettings.FFmpegOptions.x264_CRF, Is.EqualTo(31));
        Assert.That(source.TaskSettings.CaptureSettings.FFmpegOptions.x264_CRF, Is.EqualTo(23));
        Assert.That(modelChanges, Is.EqualTo(1), "Codec-only dialog edits must notify the autosave owner.");
    }

    public class FFmpegDialogProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? arguments)
        {
            if (method!.Name == nameof(IViewDialogService.ShowFFmpegOptionsAsync))
            {
                ((FFmpegOptionsViewModel)arguments![0]!).X264CRF = 31;
                return Task.CompletedTask;
            }
            throw new InvalidOperationException(method.Name);
        }
    }

    [AvaloniaTest]
    public void PageRendersInlineSectionsInBothThemesAndNarrowViewport()
    {
        using var scope = new Scope();
        using var vm = scope.Create();
        var page = new WorkflowsView(vm);
        var window = new Window { Width = 1126, Height = 700, Content = page };
        foreach (string theme in new[] { "Typography", "ThemeResources" })
            window.Styles.Insert(0, new StyleInclude(new Uri("avares://XerahS.UI/"))
            { Source = new Uri($"avares://XerahS.UI/Themes/{theme}.axaml") });
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var editor = page.FindControl<WorkflowEditorView>("InlineWorkflowEditor")!;
            var sections = editor.FindControl<TabControl>("WorkflowSections")!;
            Assert.That(sections.TabStripPlacement, Is.EqualTo(Dock.Left));
            Assert.That(sections.Items.OfType<TabItem>().Where(t => t.IsVisible).Select(t => t.Header?.ToString()), Is.EqualTo(new[] { "Task", "Image", "Settings" }));
            Assert.That(editor.GetVisualDescendants().OfType<Button>().Any(b => b.IsEffectivelyVisible && Equals(b.Content, "OK")), Is.False);
            sections.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            editor.UpdateLayout();
            var imagePanel = editor.GetVisualDescendants().OfType<TaskImageSettingsPanel>().First();
            var imageTabs = imagePanel.GetVisualDescendants().OfType<TabControl>().First();
            foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
            {
                window.RequestedThemeVariant = theme;
                sections.SelectedIndex = 0;
                SavePreview(page, $"workflows-{theme}-task");
                sections.SelectedIndex = 1;
                imageTabs.SelectedIndex = 0;
                SavePreview(page, $"workflows-{theme}-image");
                imageTabs.SelectedIndex = 1;
                SavePreview(page, $"workflows-{theme}-effects");
            }
            window.Width = 800;
            window.Height = 600;
            sections.SelectedIndex = 0;
            SavePreview(page, "workflows-narrow-task");
            sections.SelectedIndex = 1;
            SavePreview(page, "workflows-narrow-effects");
            var sidebar = page.FindControl<ListBox>("WorkflowList")!;
            Assert.That(sidebar.Bounds.Width, Is.GreaterThan(150));
            Assert.That(sections.Bounds.Width, Is.GreaterThan(400));
            vm.SelectedWorkflow = vm.Workflows[1];
            Dispatcher.UIThread.RunJobs();
            Assert.That(sections.TabStripPlacement, Is.EqualTo(Dock.Left), "Changing workflow must retain inline mode.");
            Assert.That(editor.GetVisualDescendants().OfType<Button>().Any(b => b.IsEffectivelyVisible && Equals(b.Content, "OK")), Is.False);
            vm.SelectedEditor!.TaskSettings.RegionCaptureDimAllExceptSelection = true;
            window.Close();
            Assert.That(vm.SelectedWorkflow!.Model.TaskSettings.CaptureSettings.RegionCaptureOptions.DimAllExceptSelection, Is.True, "Leaving the page flushes the pending write.");
        }
        finally { window.Close(); }
    }

    private static void SavePreview(Control page, string name)
    {
        Dispatcher.UIThread.RunJobs();
        page.UpdateLayout();
        string? repo = TestContext.CurrentContext.TestDirectory;
        while (repo != null && !File.Exists(Path.Combine(repo, "Directory.Build.props"))) repo = Path.GetDirectoryName(repo);
        if (repo == null) return;
        string folder = Path.Combine(repo, "artifacts", "workflows-inline", "previews");
        Directory.CreateDirectory(folder);
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)page.Bounds.Width, (int)page.Bounds.Height), new Vector(96, 96));
        bitmap.Render(page);
        bitmap.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }

    private sealed class Scope : IDisposable
    {
        private readonly List<WorkflowSettings> _previous = SettingsManager.WorkflowsConfig.Hotkeys;
        public int SaveCount { get; private set; }
        public bool Succeed { get; set; } = true;
        public Scope()
        {
            SettingsManager.WorkflowsConfig.Hotkeys = Enumerable.Range(0, 4).Select(i =>
            {
                var workflow = new WorkflowSettings(WorkflowType.RectangleRegion, new HotkeyInfo());
                workflow.TaskSettings.Description = "Region capture " + (i + 1);
                workflow.EnsureId();
                return workflow;
            }).ToList();
        }
        public WorkflowsViewModel Create() => new(new FakeUiViewModelFactory(), null, () => { SaveCount++; return Succeed; });
        public void Dispose() => SettingsManager.WorkflowsConfig.Hotkeys = _previous;
    }
}
